using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// Solar Network HTTP client: Bearer auth, 401→refresh, 429→Retry-After, 5xx→retry (3).
/// </summary>
public sealed class SolarApiClient : ISolarApiClient
{
    public const string HttpClientName = "SolarApi";
    public const string BaseUrl = "https://api.solian.app";

    private const int MaxAttempts = 3;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenStorage _tokenStorage;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string? _overrideBearer;
    private bool _refreshing;

    public SolarApiClient(IHttpClientFactory httpClientFactory, ITokenStorage tokenStorage)
    {
        _httpClientFactory = httpClientFactory;
        _tokenStorage = tokenStorage;
    }

    public Task SetBearerTokenAsync(string? accessToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _overrideBearer = accessToken;
        return Task.CompletedTask;
    }

    public Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default)
        => SendAsync<TResponse>(HttpMethod.Get, relativePath, content: null, cancellationToken);

    public Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest body, CancellationToken cancellationToken = default)
        => SendAsync<TResponse>(HttpMethod.Post, relativePath, JsonContent.Create(body, options: JsonDefaults.Options), cancellationToken);

    public async Task PostAsync<TRequest>(string relativePath, TRequest body, CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(
                HttpMethod.Post,
                relativePath,
                JsonContent.Create(body, options: JsonDefaults.Options),
                allowRefresh: true,
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task PostAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(HttpMethod.Post, relativePath, content: null, allowRefresh: true, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public Task<TResponse> PostAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default)
        => SendAsync<TResponse>(HttpMethod.Post, relativePath, content: null, cancellationToken);

    public Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest body, CancellationToken cancellationToken = default)
        => SendAsync<TResponse>(HttpMethod.Patch, relativePath, JsonContent.Create(body, options: JsonDefaults.Options), cancellationToken);

    public async Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(HttpMethod.Delete, relativePath, content: null, allowRefresh: true, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string relativePath,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(method, relativePath, content, allowRefresh: true, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await ReadResponseAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    // —— Business ——

    public Task<SnAccount> GetMeAsync(CancellationToken cancellationToken = default)
        => GetAsync<SnAccount>("/padlock/auth/me", cancellationToken);

    public Task<SnAccount> GetPassportMeAsync(CancellationToken cancellationToken = default)
        => GetAsync<SnAccount>("/passport/accounts/me", cancellationToken);

    public async Task<SnAccountProfile> GetMyProfileAsync(CancellationToken cancellationToken = default)
    {
        // Prefer Passport account which embeds profile.
        try
        {
            var passport = await GetPassportMeAsync(cancellationToken).ConfigureAwait(false);
            if (passport.Profile is not null)
            {
                return passport.Profile;
            }
        }
        catch (SolarApiException)
        {
            // fall through
        }

        try
        {
            var padlock = await GetAsync<SnAccount>("/padlock/accounts/me", cancellationToken).ConfigureAwait(false);
            if (padlock.Profile is not null)
            {
                return padlock.Profile;
            }
        }
        catch (SolarApiException)
        {
            // fall through
        }

        var me = await GetMeAsync(cancellationToken).ConfigureAwait(false);
        return me.Profile
            ?? throw new SolarApiException("当前账户没有可用的 profile 数据。");
    }

    public Task<SnAccountProfile> UpdateMyProfileAsync(ProfileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PatchAsync<ProfileRequest, SnAccountProfile>("/passport/accounts/me/profile", request, cancellationToken);
    }

    public Task<SnAccountStatus> GetMyStatusAsync(CancellationToken cancellationToken = default)
        => GetAsync<SnAccountStatus>("/passport/accounts/me/statuses", cancellationToken);

    public Task<SnAccountStatus> SetMyStatusAsync(StatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<StatusRequest, SnAccountStatus>("/passport/accounts/me/statuses", request, cancellationToken);
    }

    public async Task<SnCheckInResult?> GetCheckInAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SnCheckInResult>("/passport/accounts/me/check-in?version=1", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<SnCheckInResult> DoCheckInAsync(CancellationToken cancellationToken = default)
        => PostAsync<SnCheckInResult>("/passport/accounts/me/check-in?version=1", cancellationToken);

    public async Task<List<SnChatRoom>> GetChatRoomsAsync(CancellationToken cancellationToken = default)
    {
        // Official Solian web accepts array OR { data|items|rooms: [] }.
        var json = await GetStringAsync("/messager/chat", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnChatRoom>(json);
    }

    public async Task<List<SnChatMessage>> GetMessagesAsync(
        string roomId,
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);

        var path = $"/messager/chat/{Uri.EscapeDataString(roomId)}/messages?offset={offset}&take={take}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnChatMessage>(json);
    }

    public async Task<Dictionary<string, ChatSummaryResponse>> GetChatSummaryAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/messager/chat/summary", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseStringKeyDictionary<ChatSummaryResponse>(json);
    }

    public Task SendMessageAsync(string roomId, SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(request);
        var path = $"/messager/chat/{Uri.EscapeDataString(roomId)}/messages";
        return PostAsync(path, request, cancellationToken);
    }

    public async Task MarkChatRoomReadAsync(string roomId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        var id = Uri.EscapeDataString(roomId);
        try
        {
            // Official Solian web: POST /messager/chat/{id}/read
            await PostAsync($"/messager/chat/{id}/read", cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            // Fallback some deployments use
            try
            {
                await PostAsync($"/messager/chat/{id}/messages/read", cancellationToken).ConfigureAwait(false);
            }
            catch (SolarApiException)
            {
                // Best-effort; local UI still clears unread.
            }
        }
    }

    public async Task<SyncResponse> SyncRoomMessagesAsync(
        string roomId,
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(request);
        var path = $"/messager/chat/{Uri.EscapeDataString(roomId)}/sync";
        try
        {
            return await PostAsync<SyncRequest, SyncResponse>(path, request, cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.Message.Contains("deserialize", StringComparison.OrdinalIgnoreCase))
        {
            // Lenient: pull raw JSON and only keep messages array.
            var json = await PostForStringAsync(path, request, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var messages = new List<SnChatMessage>();
            if (root.TryGetProperty("messages", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                messages = JsonListParser.DeserializeArrayLenient<SnChatMessage>(arr);
            }

            return new SyncResponse { Messages = messages, TotalCount = messages.Count };
        }
    }

    private async Task<string> GetStringAsync(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(HttpMethod.Get, relativePath, content: null, allowRefresh: true, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> PostForStringAsync<TRequest>(string relativePath, TRequest body, CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(
                HttpMethod.Post,
                relativePath,
                JsonContent.Create(body, options: JsonDefaults.Options),
                allowRefresh: true,
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<SnNotification>> GetNotificationsAsync(
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);
        // unmark=false: listing must not auto-mark as read
        var path = $"/ring/notifications?offset={offset}&take={take}&unmark=false";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnNotification>(json);
    }

    public async Task<int> GetNotificationCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<int>("/ring/notifications/count", cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            // Fallback: count unviewed from first page
            var list = await GetNotificationsAsync(0, 50, cancellationToken).ConfigureAwait(false);
            return list.Count(n => n.ViewedAt is null);
        }
    }

    public Task MarkAllNotificationsReadAsync(CancellationToken cancellationToken = default)
        => PostAsync("/ring/notifications/all/read", cancellationToken);

    // —— Drive ——

    public async Task<List<SnCloudFile>> GetMyFilesAsync(
        string? parentId,
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 50 : take;
        offset = Math.Max(0, offset);
        var query = $"offset={offset}&take={take}&order=date&orderDesc=true&recycled=false";

        // Gateway: /api → /drive
        string path;
        if (string.IsNullOrWhiteSpace(parentId))
        {
            path = $"/drive/files/me?{query}";
        }
        else
        {
            path = $"/drive/files/{Uri.EscapeDataString(parentId)}/children?{query}";
        }

        try
        {
            return await GetFileListFlexibleAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            // Fallback to documented /api prefix (some gateways still accept it).
            var fallback = string.IsNullOrWhiteSpace(parentId)
                ? $"/api/files/me?{query}"
                : $"/api/files/{Uri.EscapeDataString(parentId!)}/children?{query}";
            return await GetFileListFlexibleAsync(fallback, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<SnCloudFile> CreateFolderAsync(string name, string? parentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var request = new CreateFolderRequest
        {
            Name = name.Trim(),
            ParentId = string.IsNullOrWhiteSpace(parentId) ? null : parentId,
        };

        try
        {
            return await PostAsync<CreateFolderRequest, SnCloudFile>("/drive/folders", request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            return await PostAsync<CreateFolderRequest, SnCloudFile>("/folders", request, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public Task<SnCloudFile> RenameFileAsync(string fileId, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        var body = new RenameFileRequest { Name = newName.Trim() };
        var path = $"/drive/files/{Uri.EscapeDataString(fileId)}";
        return PatchAsync<RenameFileRequest, SnCloudFile>(path, body, cancellationToken);
    }

    public Task RecycleFilesAsync(IEnumerable<string> fileIds, CancellationToken cancellationToken = default)
    {
        var ids = fileIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            throw new ArgumentException("At least one file id is required.", nameof(fileIds));
        }

        var body = new FileBatchIdsRequest { FileIds = ids };
        return PostAsync("/drive/files/recycle/batch", body, cancellationToken);
    }

    public async Task<SnCloudFile> UploadFileDirectAsync(
        Stream content,
        string fileName,
        string contentType,
        long fileSize,
        string? parentId,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        contentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        progress?.Report(0);

        // Buffer stream so retries are possible (Node 6: typical desktop files).
        await using var buffer = new MemoryStream();
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var bytes = buffer.ToArray();
        if (fileSize <= 0)
        {
            fileSize = bytes.LongLength;
        }

        using var multipart = BuildDirectUploadContent(bytes, fileName, contentType, parentId, progress);

        try
        {
            return await SendAsync<SnCloudFile>(HttpMethod.Post, "/drive/files/upload/direct", multipart, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            using var multipart2 = BuildDirectUploadContent(bytes, fileName, contentType, parentId, progress);
            return await SendAsync<SnCloudFile>(HttpMethod.Post, "/api/files/upload/direct", multipart2, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task DownloadToStreamAsync(
        string url,
        Stream destination,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(destination);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);

        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);

        var total = response.Content.Headers.ContentLength ?? -1;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            readTotal += read;
            if (total > 0)
            {
                progress?.Report(Math.Clamp((double)readTotal / total, 0, 1));
            }
        }

        progress?.Report(1);
    }

    private static MultipartFormDataContent BuildDirectUploadContent(
        byte[] bytes,
        string fileName,
        string contentType,
        string? parentId,
        IProgress<double>? progress)
    {
        var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(fileName), "file_name");
        multipart.Add(new StringContent(contentType), "content_type");
        multipart.Add(new StringContent(bytes.LongLength.ToString(CultureInfo.InvariantCulture)), "file_size");
        multipart.Add(new StringContent("true"), "index");
        if (!string.IsNullOrWhiteSpace(parentId))
        {
            multipart.Add(new StringContent(parentId), "parent_id");
        }

        var progressStream = new ProgressStream(new MemoryStream(bytes), bytes.LongLength, progress);
        var fileContent = new StreamContent(progressStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "file", fileName);
        return multipart;
    }

    private async Task<List<SnCloudFile>> GetFileListFlexibleAsync(string path, CancellationToken cancellationToken)
    {
        // Response may be a bare array or a paged object with items/data/files.
        using var response = await SendCoreAsync(HttpMethod.Get, path, content: null, allowRefresh: true, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return JsonListParser.DeserializeArrayLenient<SnCloudFile>(root);
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in new[] { "items", "data", "files", "results", "content" })
            {
                if (root.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    return JsonListParser.DeserializeArrayLenient<SnCloudFile>(arr);
                }
            }
        }

        throw new SolarApiException("Unexpected file list response shape.", response.StatusCode);
    }

    // —— Transport ——

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpMethod method,
        string relativePath,
        HttpContent? content,
        bool allowRefresh,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        byte[]? contentBytes = null;
        MediaTypeHeaderValue? contentType = null;
        List<KeyValuePair<string, IEnumerable<string>>>? contentHeaders = null;

        if (content is not null)
        {
            contentBytes = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            contentType = content.Headers.ContentType;
            contentHeaders = content.Headers
                .Where(h => !string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                .Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value))
                .ToList();
            content.Dispose();
        }

        var didRefresh = false;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(method, NormalizePath(relativePath));
            if (contentBytes is not null)
            {
                request.Content = CreateBody(contentBytes, contentType, contentHeaders);
            }

            await AttachBearerAsync(request, cancellationToken).ConfigureAwait(false);

            try
            {
                var response = await client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                // 401 → refresh once, then retry same attempt budget
                if (response.StatusCode == HttpStatusCode.Unauthorized && allowRefresh && !didRefresh && !_refreshing)
                {
                    response.Dispose();
                    var refreshed = await TryRefreshAccessTokenAsync(cancellationToken).ConfigureAwait(false);
                    if (refreshed)
                    {
                        didRefresh = true;
                        attempt--; // retry this logical attempt with new token
                        continue;
                    }

                    throw new SolarApiException("未授权，且刷新 token 失败。", HttpStatusCode.Unauthorized);
                }

                // 429 → wait Retry-After
                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxAttempts)
                {
                    var delay = GetRetryAfterDelay(response) ?? TimeSpan.FromSeconds(attempt);
                    response.Dispose();
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // 5xx → retry
                if (IsServerError(response.StatusCode) && attempt < MaxAttempts)
                {
                    response.Dispose();
                    await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return response;
            }
            catch (SolarApiException)
            {
                throw;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = new SolarApiException("Request timed out.");
                if (attempt >= MaxAttempts)
                {
                    break;
                }

                await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                lastException = new SolarApiException("Network error while calling Solar API.", inner: ex);
                if (attempt >= MaxAttempts)
                {
                    break;
                }

                await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastException ?? new SolarApiException("Request failed after retries.");
    }

    /// <summary>
    /// Refresh using vault refresh_token without re-entering 401 handling (allowRefresh=false).
    /// Does not log token values.
    /// </summary>
    private async Task<bool> TryRefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_refreshing)
            {
                return false;
            }

            _refreshing = true;
            var refresh = await _tokenStorage.GetRefreshTokenAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(refresh))
            {
                return false;
            }

            var body = new TokenExchangeRequest
            {
                GrantType = "refresh_token",
                RefreshToken = refresh,
            };

            try
            {
                TokenExchangeResponse tokens;
                try
                {
                    using var response = await SendCoreAsync(
                            HttpMethod.Post,
                            "/padlock/auth/refresh",
                            JsonContent.Create(body, options: JsonDefaults.Options),
                            allowRefresh: false,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await EnsureSuccessAsync(response).ConfigureAwait(false);
                    tokens = await ReadResponseAsync<TokenExchangeResponse>(response, cancellationToken).ConfigureAwait(false);
                }
                catch (SolarApiException)
                {
                    using var response = await SendCoreAsync(
                            HttpMethod.Post,
                            "/padlock/auth/token",
                            JsonContent.Create(body, options: JsonDefaults.Options),
                            allowRefresh: false,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await EnsureSuccessAsync(response).ConfigureAwait(false);
                    tokens = await ReadResponseAsync<TokenExchangeResponse>(response, cancellationToken).ConfigureAwait(false);
                }

                if (string.IsNullOrWhiteSpace(tokens.Token))
                {
                    return false;
                }

                DateTimeOffset? expiresAt = tokens.ExpiresIn > 0
                    ? DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn)
                    : null;

                await _tokenStorage
                    .SaveTokensAsync(tokens.Token, tokens.RefreshToken ?? refresh, expiresAt, cancellationToken)
                    .ConfigureAwait(false);
                _overrideBearer = tokens.Token;
                return true;
            }
            catch (SolarApiException)
            {
                return false;
            }
        }
        finally
        {
            _refreshing = false;
            _refreshLock.Release();
        }
    }

    private async Task AttachBearerAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _overrideBearer ?? await _tokenStorage.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private static ByteArrayContent CreateBody(
        byte[] contentBytes,
        MediaTypeHeaderValue? contentType,
        List<KeyValuePair<string, IEnumerable<string>>>? contentHeaders)
    {
        var body = new ByteArrayContent(contentBytes);
        if (contentType is not null)
        {
            body.Headers.ContentType = contentType;
        }

        if (contentHeaders is not null)
        {
            foreach (var header in contentHeaders)
            {
                body.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return body;
    }

    private static async Task<TResponse> ReadResponseAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (typeof(TResponse) == typeof(string))
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return (TResponse)(object)text;
        }

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return default!;
        }

        try
        {
            var payload = await response.Content
                .ReadFromJsonAsync<TResponse>(JsonDefaults.Options, cancellationToken)
                .ConfigureAwait(false);

            if (payload is null)
            {
                throw new SolarApiException("API returned an empty JSON body.", response.StatusCode);
            }

            return payload;
        }
        catch (JsonException ex)
        {
            var body = await SafeReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            throw new SolarApiException("Failed to deserialize API response.", response.StatusCode, body, ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await SafeReadBodyAsync(response, CancellationToken.None).ConfigureAwait(false);
        throw new SolarApiException(
            $"API request failed with {(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode,
            body);
    }

    private static async Task<string?> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsServerError(HttpStatusCode statusCode)
        => (int)statusCode >= 500
           || statusCode is HttpStatusCode.RequestTimeout
               or HttpStatusCode.BadGateway
               or HttpStatusCode.ServiceUnavailable
               or HttpStatusCode.GatewayTimeout;

    private static TimeSpan? GetRetryAfterDelay(HttpResponseMessage response)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        // Some servers send raw seconds without parsing into RetryAfter
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var raw = values.FirstOrDefault();
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            {
                return TimeSpan.FromSeconds(Math.Max(0, seconds));
            }
        }

        return null;
    }

    private static Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var delayMs = 250 * attempt * attempt;
        return Task.Delay(delayMs, cancellationToken);
    }

    private static string NormalizePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Path is required.", nameof(relativePath));
        }

        return relativePath.StartsWith('/') ? relativePath[1..] : relativePath;
    }
}
