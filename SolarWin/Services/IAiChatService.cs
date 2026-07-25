namespace SolarWin.Services;

/// <summary>OpenAI-compatible chat (user-configured base URL + key + model), streaming preferred.</summary>
public interface IAiChatService
{
    /// <summary>
    /// Stream chat completions (SSE). Invokes <paramref name="onDelta"/> on each content/reasoning chunk.
    /// Falls back to non-stream request if the server rejects stream.
    /// </summary>
    Task<AiChatResult> StreamCompleteAsync(
        AiChatRequest request,
        Func<AiChatDelta, Task> onDelta,
        CancellationToken cancellationToken = default);

    /// <summary>Non-streaming completion (legacy / fallback).</summary>
    Task<AiChatResult> CompleteAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class AiChatRequest
{
    public required string ApiUrl { get; init; }

    public required string ApiKey { get; init; }

    public required string Model { get; init; }

    public required IReadOnlyList<AiChatTurn> Messages { get; init; }

    public string? ReasoningEffort { get; init; }

    /// <summary>Prefer full history + long-context request hints (1M-class models).</summary>
    public bool Use1MContext { get; init; }

    public bool Stream { get; init; } = true;
}

public sealed class AiChatTurn
{
    public required string Role { get; init; }

    public required string Content { get; init; }
}

public sealed class AiChatDelta
{
    public string? Content { get; init; }

    public string? Reasoning { get; init; }
}

public sealed class AiChatResult
{
    public bool Ok { get; init; }

    public string? Content { get; init; }

    public string? ReasoningContent { get; init; }

    public string? Error { get; init; }

    public bool UsedStreaming { get; init; }

    public static AiChatResult Success(string content, string? reasoning = null, bool streamed = false) => new()
    {
        Ok = true,
        Content = content,
        ReasoningContent = reasoning,
        UsedStreaming = streamed,
    };

    public static AiChatResult Fail(string error) => new()
    {
        Ok = false,
        Error = error,
    };
}
