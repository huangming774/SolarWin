using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Services;

/// <summary>
/// OpenAI-compatible client with broad gateway support:
/// chat/completions (+ SSE stream), optional responses path, Azure api-key header,
/// multi-shape message/delta parsing, reasoning_effort / reasoning.effort.
/// </summary>
public sealed class AiChatService : IAiChatService
{
    public const string HttpClientName = "AiChat";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public AiChatService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AiChatResult> StreamCompleteAsync(
        AiChatRequest request,
        Func<AiChatDelta, Task> onDelta,
        CancellationToken cancellationToken = default)
    {
        var validated = Validate(request);
        if (validated is not null)
        {
            return validated;
        }

        var messages = PrepareMessages(request);
        var endpoints = BuildEndpointCandidates(request.ApiUrl);

        Exception? lastError = null;
        foreach (var endpoint in endpoints)
        {
            // Prefer stream; if 400/unsupported, try non-stream on same endpoint
            var streamResult = await TryStreamOnceAsync(
                endpoint, request, messages, onDelta, cancellationToken).ConfigureAwait(false);
            if (streamResult.Ok || IsCancel(streamResult))
            {
                return streamResult;
            }

            // Non-stream fallback for this endpoint
            var plain = await TryCompleteOnceAsync(
                endpoint, request, messages, stream: false, cancellationToken).ConfigureAwait(false);
            if (plain.Ok)
            {
                if (!string.IsNullOrEmpty(plain.Content) && onDelta is not null)
                {
                    await onDelta(new AiChatDelta { Content = plain.Content }).ConfigureAwait(false);
                }

                if (!string.IsNullOrEmpty(plain.ReasoningContent) && onDelta is not null)
                {
                    await onDelta(new AiChatDelta { Reasoning = plain.ReasoningContent }).ConfigureAwait(false);
                }

                return AiChatResult.Success(
                    plain.Content ?? string.Empty,
                    plain.ReasoningContent,
                    streamed: false);
            }

            lastError = new InvalidOperationException(plain.Error ?? streamResult.Error ?? "未知错误");
            // try next endpoint candidate
        }

        return AiChatResult.Fail(lastError?.Message ?? "所有兼容端点均失败");
    }

    public async Task<AiChatResult> CompleteAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var validated = Validate(request);
        if (validated is not null)
        {
            return validated;
        }

        var messages = PrepareMessages(request);
        var endpoints = BuildEndpointCandidates(request.ApiUrl);
        string? lastErr = null;
        foreach (var endpoint in endpoints)
        {
            var result = await TryCompleteOnceAsync(
                endpoint, request, messages, stream: false, cancellationToken).ConfigureAwait(false);
            if (result.Ok || IsCancel(result))
            {
                return result;
            }

            lastErr = result.Error;
        }

        return AiChatResult.Fail(lastErr ?? "请求失败");
    }

    private async Task<AiChatResult> TryStreamOnceAsync(
        string endpoint,
        AiChatRequest request,
        IReadOnlyList<AiChatTurn> messages,
        Func<AiChatDelta, Task> onDelta,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = BuildHttpRequest(endpoint, request, messages, stream: true);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AiChatResult.Fail("已取消");
        }
        catch (TaskCanceledException)
        {
            return AiChatResult.Fail("请求超时");
        }
        catch (HttpRequestException ex)
        {
            return AiChatResult.Fail("网络错误：" + ex.Message);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return AiChatResult.Fail(
                    TryParseError(errBody) ?? $"{(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            // Some servers ignore stream=true and return JSON
            if (mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
                && !mediaType.Contains("event-stream", StringComparison.OrdinalIgnoreCase))
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var parsed = ParseCompletionJson(body);
                if (parsed.Ok && onDelta is not null)
                {
                    if (!string.IsNullOrEmpty(parsed.Content))
                    {
                        await onDelta(new AiChatDelta { Content = parsed.Content }).ConfigureAwait(false);
                    }

                    if (!string.IsNullOrEmpty(parsed.ReasoningContent))
                    {
                        await onDelta(new AiChatDelta { Reasoning = parsed.ReasoningContent }).ConfigureAwait(false);
                    }
                }

                return AiChatResult.Success(
                    parsed.Content ?? string.Empty,
                    parsed.ReasoningContent,
                    streamed: false);
            }

            var receivedContent = false;
            var receivedReasoning = false;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                line = line.Trim();
                if (line.Length == 0 || line.StartsWith(':'))
                {
                    continue; // SSE comment / keep-alive
                }

                string payload;
                if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    payload = line["data:".Length..].Trim();
                }
                else if (line.StartsWith('{') || line.StartsWith('['))
                {
                    // Some gateways emit raw JSON lines
                    payload = line;
                }
                else
                {
                    continue;
                }

                if (payload is "[DONE]" or "DONE" or "\"[DONE]\"")
                {
                    break;
                }

                if (!TryParseStreamChunk(payload, out var contentDelta, out var reasoningDelta))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(contentDelta))
                {
                    receivedContent = true;
                    await onDelta(new AiChatDelta { Content = contentDelta }).ConfigureAwait(false);
                }

                if (!string.IsNullOrEmpty(reasoningDelta))
                {
                    receivedReasoning = true;
                    await onDelta(new AiChatDelta { Reasoning = reasoningDelta }).ConfigureAwait(false);
                }
            }

            if (!receivedContent && !receivedReasoning)
            {
                return AiChatResult.Fail("流式响应为空或无法解析（可检查 API 是否支持 stream）");
            }

            return AiChatResult.Success(string.Empty, streamed: true);
        }
    }

    private async Task<AiChatResult> TryCompleteOnceAsync(
        string endpoint,
        AiChatRequest request,
        IReadOnlyList<AiChatTurn> messages,
        bool stream,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var httpRequest = BuildHttpRequest(endpoint, request, messages, stream);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AiChatResult.Fail("已取消");
        }
        catch (TaskCanceledException)
        {
            return AiChatResult.Fail("请求超时");
        }
        catch (HttpRequestException ex)
        {
            return AiChatResult.Fail("网络错误：" + ex.Message);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return AiChatResult.Fail(
                TryParseError(body) ?? $"{(int)response.StatusCode} {response.ReasonPhrase}");
        }

        return ParseCompletionJson(body);
    }

    private static HttpRequestMessage BuildHttpRequest(
        string endpoint,
        AiChatRequest request,
        IReadOnlyList<AiChatTurn> messages,
        bool stream)
    {
        var payload = BuildPayload(request, messages, stream);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        var key = request.ApiKey.Trim();

        // OpenAI / most proxies
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        // Azure OpenAI style
        httpRequest.Headers.TryAddWithoutValidation("api-key", key);
        // Some Chinese gateways
        httpRequest.Headers.TryAddWithoutValidation("x-api-key", key);

        if (stream)
        {
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        }

        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");
        return httpRequest;
    }

    private static Dictionary<string, object?> BuildPayload(
        AiChatRequest request,
        IReadOnlyList<AiChatTurn> messages,
        bool stream)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model.Trim(),
            ["messages"] = messages.Select(m => new Dictionary<string, object?>
            {
                ["role"] = m.Role,
                ["content"] = m.Content,
            }).ToList(),
            ["stream"] = stream,
        };

        if (stream)
        {
            // OpenAI: include usage in final chunk when supported
            payload["stream_options"] = new Dictionary<string, object?> { ["include_usage"] = true };
        }

        var effort = NormalizeEffort(request.ReasoningEffort);
        if (effort is not null)
        {
            payload["reasoning_effort"] = effort;
            // xAI / some OpenAI-compatible responses-style clients
            payload["reasoning"] = new Dictionary<string, object?> { ["effort"] = effort };
        }

        if (request.Use1MContext)
        {
            // Hints only — ignored by APIs that don't support them
            payload["max_tokens"] = 32768;
            payload["max_completion_tokens"] = 32768;
            // OpenRouter / some long-context gateways
            payload["provider"] = new Dictionary<string, object?>
            {
                ["allow_fallbacks"] = true,
            };
        }

        return payload;
    }

    private static IReadOnlyList<AiChatTurn> PrepareMessages(AiChatRequest request)
    {
        var list = request.Messages.ToList();
        if (request.Use1MContext)
        {
            // Keep full history for 1M-class models
            return list;
        }

        // Default: soft trim to last ~40 turns / ~120k chars to avoid blowing small-context APIs
        const int maxTurns = 40;
        const int maxChars = 120_000;
        if (list.Count > maxTurns)
        {
            list = list.TakeLast(maxTurns).ToList();
        }

        var total = list.Sum(m => m.Content?.Length ?? 0);
        while (total > maxChars && list.Count > 2)
        {
            total -= list[0].Content?.Length ?? 0;
            list.RemoveAt(0);
        }

        return list;
    }

    private static AiChatResult? Validate(AiChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ApiUrl))
        {
            return AiChatResult.Fail("请先配置 API URL");
        }

        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return AiChatResult.Fail("请先配置 API Key");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return AiChatResult.Fail("请先填写模型名称（无默认模型）");
        }

        if (request.Messages.Count == 0)
        {
            return AiChatResult.Fail("没有可发送的消息");
        }

        return null;
    }

    /// <summary>
    /// Build endpoint candidates for maximum OpenAI-compatible coverage.
    /// User may paste base host, /v1, full chat/completions, or Azure deployment URL.
    /// </summary>
    internal static IReadOnlyList<string> BuildEndpointCandidates(string apiUrl)
    {
        var u = apiUrl.Trim().TrimEnd('/');
        var list = new List<string>();

        void Add(string s)
        {
            if (!string.IsNullOrWhiteSpace(s) && !list.Contains(s, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(s);
            }
        }

        if (u.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            || u.Contains("/chat/completions?", StringComparison.OrdinalIgnoreCase)
            || u.EndsWith("/completions", StringComparison.OrdinalIgnoreCase)
            || u.Contains("/openai/deployments/", StringComparison.OrdinalIgnoreCase))
        {
            Add(u);
            return list;
        }

        // Bare host or path
        Add(u + "/chat/completions");
        Add(u + "/v1/chat/completions");
        if (!u.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            Add(u + "/v1/chat/completions");
        }
        else
        {
            // already .../v1
            Add(u + "/chat/completions");
        }

        // Some gateways only expose /completions
        Add(u + "/completions");
        Add(u + "/v1/completions");

        return list;
    }

    private static bool TryParseStreamChunk(string payload, out string? content, out string? reasoning)
    {
        content = null;
        reasoning = null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            // Standard chat.completion.chunk
            if (root.TryGetProperty("choices", out var choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0)
            {
                var c0 = choices[0];
                if (c0.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object)
                {
                    content = ExtractText(delta, "content")
                              ?? ExtractText(delta, "text");
                    reasoning = ExtractText(delta, "reasoning_content")
                                ?? ExtractText(delta, "reasoning")
                                ?? ExtractNestedReasoning(delta);
                    return content is not null || reasoning is not null;
                }

                // Non-delta chunk message (some proxies)
                if (c0.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object)
                {
                    content = ExtractText(msg, "content");
                    reasoning = ExtractText(msg, "reasoning_content") ?? ExtractText(msg, "reasoning");
                    return content is not null || reasoning is not null;
                }

                if (c0.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                {
                    content = text.GetString();
                    return !string.IsNullOrEmpty(content);
                }
            }

            // Responses API stream-ish
            if (root.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
            {
                var type = typeEl.GetString();
                if (type is "response.output_text.delta" or "response.reasoning_text.delta"
                    or "response.reasoning_summary_text.delta")
                {
                    if (root.TryGetProperty("delta", out var d) && d.ValueKind == JsonValueKind.String)
                    {
                        if (type.Contains("reasoning", StringComparison.Ordinal))
                        {
                            reasoning = d.GetString();
                        }
                        else
                        {
                            content = d.GetString();
                        }

                        return true;
                    }
                }
            }

            // Top-level content (rare)
            content = ExtractText(root, "content") ?? ExtractText(root, "text");
            return !string.IsNullOrEmpty(content);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static AiChatResult ParseCompletionJson(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0)
            {
                var c0 = choices[0];
                if (c0.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
                {
                    var content = ExtractText(message, "content");
                    var reasoning = ExtractText(message, "reasoning_content")
                                    ?? ExtractText(message, "reasoning")
                                    ?? ExtractNestedReasoning(message);
                    if (!string.IsNullOrWhiteSpace(content) || !string.IsNullOrWhiteSpace(reasoning))
                    {
                        return AiChatResult.Success(
                            string.IsNullOrWhiteSpace(content) ? "(无文本回复)" : content!,
                            reasoning);
                    }
                }

                if (c0.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                {
                    return AiChatResult.Success(textEl.GetString() ?? string.Empty);
                }

                // completions legacy: choices[].text
                if (c0.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object)
                {
                    var content = ExtractText(delta, "content");
                    if (!string.IsNullOrEmpty(content))
                    {
                        return AiChatResult.Success(content!);
                    }
                }
            }

            // Responses API
            if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
            {
                var texts = new List<string>();
                var reasons = new List<string>();
                foreach (var item in output.EnumerateArray())
                {
                    CollectOutputTexts(item, texts, reasons);
                }

                if (texts.Count > 0 || reasons.Count > 0)
                {
                    return AiChatResult.Success(
                        texts.Count > 0 ? string.Join("\n", texts.Where(s => !string.IsNullOrWhiteSpace(s))) : "(无文本回复)",
                        reasons.Count > 0 ? string.Join("\n", reasons) : null);
                }
            }

            // output_text convenience field
            if (root.TryGetProperty("output_text", out var ot) && ot.ValueKind == JsonValueKind.String)
            {
                return AiChatResult.Success(ot.GetString() ?? string.Empty);
            }

            return AiChatResult.Fail("响应格式无法解析，请确认兼容 OpenAI Chat Completions / Responses");
        }
        catch (JsonException ex)
        {
            return AiChatResult.Fail("JSON 解析失败：" + ex.Message);
        }
    }

    private static void CollectOutputTexts(JsonElement item, List<string> texts, List<string> reasons)
    {
        if (item.TryGetProperty("content", out var contentArr) && contentArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in contentArr.EnumerateArray())
            {
                var type = c.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (c.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String)
                {
                    var s = tx.GetString() ?? string.Empty;
                    if (type is "reasoning" or "reasoning_text")
                    {
                        reasons.Add(s);
                    }
                    else
                    {
                        texts.Add(s);
                    }
                }
            }
        }

        if (item.TryGetProperty("type", out var itemType)
            && itemType.GetString() is "message"
            && item.TryGetProperty("content", out var msgContent)
            && msgContent.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in msgContent.EnumerateArray())
            {
                if (c.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String)
                {
                    texts.Add(tx.GetString() ?? string.Empty);
                }
            }
        }
    }

    private static string? ExtractNestedReasoning(JsonElement el)
    {
        if (el.TryGetProperty("reasoning", out var r))
        {
            if (r.ValueKind == JsonValueKind.String)
            {
                return r.GetString();
            }

            if (r.ValueKind == JsonValueKind.Object && r.TryGetProperty("content", out var c))
            {
                return ExtractText(r, "content") ?? (c.ValueKind == JsonValueKind.String ? c.GetString() : null);
            }
        }

        return null;
    }

    private static string? NormalizeEffort(string? effort)
    {
        if (string.IsNullOrWhiteSpace(effort))
        {
            return null;
        }

        var e = effort.Trim().ToLowerInvariant();
        return e switch
        {
            "low" or "medium" or "high" or "xhigh" => e,
            "off" or "none" or "default" or "auto" => null,
            _ => null,
        };
    }

    private static string? ExtractText(JsonElement message, string propertyName)
    {
        if (!message.TryGetProperty(propertyName, out var el))
        {
            return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Array => string.Join(
                string.Empty,
                el.EnumerateArray()
                    .Select(p =>
                    {
                        if (p.ValueKind == JsonValueKind.String)
                        {
                            return p.GetString() ?? string.Empty;
                        }

                        if (p.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                        {
                            return t.GetString() ?? string.Empty;
                        }

                        if (p.TryGetProperty("type", out var ty)
                            && ty.GetString() is "text" or "output_text"
                            && p.TryGetProperty("text", out var t2)
                            && t2.ValueKind == JsonValueKind.String)
                        {
                            return t2.GetString() ?? string.Empty;
                        }

                        return string.Empty;
                    })),
            JsonValueKind.Object when el.TryGetProperty("text", out var nested)
                && nested.ValueKind == JsonValueKind.String => nested.GetString(),
            _ => null,
        };
    }

    private static string? TryParseError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err))
            {
                if (err.ValueKind == JsonValueKind.String)
                {
                    return err.GetString();
                }

                if (err.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                {
                    return msg.GetString();
                }
            }

            if (root.TryGetProperty("message", out var top) && top.ValueKind == JsonValueKind.String)
            {
                return top.GetString();
            }
        }
        catch (JsonException)
        {
            // fall through
        }

        var trimmed = body.Trim();
        return trimmed.Length > 400 ? trimmed[..400] + "…" : trimmed;
    }

    private static bool IsCancel(AiChatResult r) =>
        r.Error is "已取消";
}
