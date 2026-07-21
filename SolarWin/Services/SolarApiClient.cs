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

    public async Task PatchAsync<TRequest>(string relativePath, TRequest body, CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(
                HttpMethod.Patch,
                relativePath,
                JsonContent.Create(body, options: JsonDefaults.Options),
                allowRefresh: true,
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(HttpMethod.Delete, relativePath, content: null, allowRefresh: true, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task DeleteAsync<TRequest>(string relativePath, TRequest body, CancellationToken cancellationToken = default)
    {
        using var response = await SendCoreAsync(
                HttpMethod.Delete,
                relativePath,
                JsonContent.Create(body, options: JsonDefaults.Options),
                allowRefresh: true,
                cancellationToken)
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

    public async Task<List<SnAccount>> SearchAccountsAsync(
        string query,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        take = take <= 0 ? 20 : Math.Min(take, 50);
        var path =
            $"/passport/accounts/search?query={Uri.EscapeDataString(query.Trim())}&take={take}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAccount>(json);
    }

    public async Task<List<SnChatRoom>> GetChatRoomsAsync(CancellationToken cancellationToken = default)
    {
        // Official Solian web accepts array OR { data|items|rooms: [] }.
        var json = await GetStringAsync("/messager/chat", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnChatRoom>(json);
    }

    public Task<SnChatRoom> GetChatRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
        => GetAsync<SnChatRoom>($"/messager/chat/{roomId:D}", cancellationToken);

    public Task<SnChatRoom> CreateChatRoomAsync(ChatRoomRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<ChatRoomRequest, SnChatRoom>("/messager/chat", request, cancellationToken);
    }

    public Task<SnChatRoom> UpdateChatRoomAsync(Guid roomId, ChatRoomRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PatchAsync<ChatRoomRequest, SnChatRoom>($"/messager/chat/{roomId:D}", request, cancellationToken);
    }

    public Task DeleteChatRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/messager/chat/{roomId:D}", cancellationToken);

    public Task<SnChatRoom> CreateDirectChatAsync(Guid relatedUserId, CancellationToken cancellationToken = default)
    {
        var body = new DirectMessageRequest { RelatedUserId = relatedUserId };
        return PostAsync<DirectMessageRequest, SnChatRoom>("/messager/chat/direct", body, cancellationToken);
    }

    public async Task<SnChatRoom?> GetDirectChatAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SnChatRoom>($"/messager/chat/direct/{accountId:D}", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
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

    public Task<SnChatMessage> GetMessageAsync(Guid roomId, Guid messageId, CancellationToken cancellationToken = default)
        => GetAsync<SnChatMessage>($"/messager/chat/{roomId:D}/messages/{messageId:D}", cancellationToken);

    public async Task<Dictionary<string, ChatSummaryResponse>> GetChatSummaryAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/messager/chat/summary", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseStringKeyDictionary<ChatSummaryResponse>(json);
    }

    public async Task<int> GetChatUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<int>("/messager/chat/unread", cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            // Some deployments wrap: { "count": n } / { "data": n }
            var json = await GetStringAsync("/messager/chat/unread", cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Number && root.TryGetInt32(out var n))
            {
                return n;
            }

            foreach (var key in new[] { "count", "unread", "data", "total" })
            {
                if (root.TryGetProperty(key, out var p) && p.TryGetInt32(out var v))
                {
                    return v;
                }
            }

            return 0;
        }
    }

    public Task MarkAllChatRoomsReadAsync(CancellationToken cancellationToken = default)
        => PostAsync("/messager/chat/read-all", cancellationToken);

    public Task SendMessageAsync(string roomId, SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomId);
        ArgumentNullException.ThrowIfNull(request);
        var path = $"/messager/chat/{Uri.EscapeDataString(roomId)}/messages";
        return PostAsync(path, request, cancellationToken);
    }

    public Task EditMessageAsync(Guid roomId, Guid messageId, SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PatchAsync($"/messager/chat/{roomId:D}/messages/{messageId:D}", request, cancellationToken);
    }

    public Task DeleteMessageAsync(
        Guid roomId,
        Guid messageId,
        DeleteMessageRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"/messager/chat/{roomId:D}/messages/{messageId:D}";
        return request is null
            ? DeleteAsync(path, cancellationToken)
            : DeleteAsync(path, request, cancellationToken);
    }

    public Task ModerateDeleteMessageAsync(
        Guid roomId,
        Guid messageId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var body = new DeleteChatRoomMessageRequest { Reason = reason };
        return DeleteAsync($"/messager/chat/rooms/{roomId:D}/messages/{messageId:D}", body, cancellationToken);
    }

    public Task<SnChatReaction> ReactToMessageAsync(
        Guid roomId,
        Guid messageId,
        MessageReactionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<MessageReactionRequest, SnChatReaction>(
            $"/messager/chat/{roomId:D}/messages/{messageId:D}/reactions",
            request,
            cancellationToken);
    }

    public Task RemoveMessageReactionAsync(
        Guid roomId,
        Guid messageId,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        return DeleteAsync(
            $"/messager/chat/{roomId:D}/messages/{messageId:D}/reactions/{Uri.EscapeDataString(symbol)}",
            cancellationToken);
    }

    public async Task<List<SnChatReaction>> GetMessageReactionsAsync(
        Guid roomId,
        Guid messageId,
        int offset = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);
        var path = $"/messager/chat/{roomId:D}/messages/{messageId:D}/reactions?offset={offset}&take={take}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnChatReaction>(json);
    }

    public Task<SnChatMessagePin> PinMessageAsync(
        Guid roomId,
        Guid messageId,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var body = new PinMessageRequest { MessageId = messageId, ExpiresAt = expiresAt };
        return PostAsync<PinMessageRequest, SnChatMessagePin>($"/messager/chat/{roomId:D}/pins", body, cancellationToken);
    }

    public Task UnpinMessageAsync(Guid roomId, Guid pinId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/messager/chat/{roomId:D}/pins/{pinId:D}", cancellationToken);

    public async Task<List<SnChatMessagePin>> GetPinnedMessagesAsync(
        Guid roomId,
        bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        var path = $"/messager/chat/{roomId:D}/pins?includeExpired={(includeExpired ? "true" : "false")}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnChatMessagePin>(json);
    }

    public Task<SnChatMessage> SendPlaceholderMessageAsync(
        Guid roomId,
        string kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        var body = new SendPlaceholderMessageRequest { Kind = kind };
        return PostAsync<SendPlaceholderMessageRequest, SnChatMessage>(
            $"/messager/chat/{roomId:D}/messages/placeholder",
            body,
            cancellationToken);
    }

    public Task<SnChatMessage> RedirectMessagesAsync(
        Guid roomId,
        IEnumerable<Guid> messageIds,
        CancellationToken cancellationToken = default)
    {
        var body = new RedirectMessagesRequest { MessageIds = messageIds.ToList() };
        return PostAsync<RedirectMessagesRequest, SnChatMessage>(
            $"/messager/chat/{roomId:D}/messages/redirect",
            body,
            cancellationToken);
    }

    public async Task<SnChatMessage> SendVoiceMessageAsync(
        Guid roomId,
        Stream file,
        string fileName,
        string contentType,
        int durationMs,
        string? nonce = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        using var multipart = new MultipartFormDataContent();
        var streamContent = new StreamContent(file);
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        multipart.Add(streamContent, "File", fileName);
        multipart.Add(new StringContent(durationMs.ToString(System.Globalization.CultureInfo.InvariantCulture)), "DurationMs");
        if (!string.IsNullOrWhiteSpace(nonce))
        {
            multipart.Add(new StringContent(nonce), "Nonce");
        }

        return await SendAsync<SnChatMessage>(
                HttpMethod.Post,
                $"/messager/chat/{roomId:D}/messages/voice",
                multipart,
                cancellationToken)
            .ConfigureAwait(false);
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

    public Task<GlobalSyncResponse> SyncAllChatMessagesAsync(SyncRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<SyncRequest, GlobalSyncResponse>("/messager/chat/sync", request, cancellationToken);
    }

    public Task<ChatRoomSyncResponse> SyncChatRoomsAsync(long lastSyncTimestamp, CancellationToken cancellationToken = default)
    {
        var body = new ChatRoomSyncRequest { LastSyncTimestamp = lastSyncTimestamp };
        return PostAsync<ChatRoomSyncRequest, ChatRoomSyncResponse>("/messager/chat/rooms/sync", body, cancellationToken);
    }

    public async Task<List<SnChatMember>> GetChatMembersAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync($"/messager/chat/{roomId:D}/members", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnChatMember>(json);
    }

    public Task<SnChatMember> GetMyChatMembershipAsync(Guid roomId, CancellationToken cancellationToken = default)
        => GetAsync<SnChatMember>($"/messager/chat/{roomId:D}/members/me", cancellationToken);

    public Task<OnlineMembersResponse> GetOnlineMembersAsync(Guid roomId, CancellationToken cancellationToken = default)
        => GetAsync<OnlineMembersResponse>($"/messager/chat/{roomId:D}/members/online", cancellationToken);

    public Task LeaveChatRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/messager/chat/{roomId:D}/members/me/profile", cancellationToken);

    public Task RemoveChatMemberAsync(Guid roomId, Guid memberId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/messager/chat/{roomId:D}/members/{memberId:D}", cancellationToken);

    public Task TimeoutChatMemberAsync(
        Guid roomId,
        Guid memberId,
        ChatTimeoutRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync($"/messager/chat/{roomId:D}/members/{memberId:D}/timeout", request, cancellationToken);
    }

    public Task ClearChatMemberTimeoutAsync(Guid roomId, Guid memberId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/messager/chat/{roomId:D}/members/{memberId:D}/timeout", cancellationToken);

    public Task TimeoutAccountInRoomAsync(
        Guid roomId,
        Guid accountId,
        ChatTimeoutRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync($"/messager/chat/rooms/{roomId:D}/members/{accountId:D}/timeout", request, cancellationToken);
    }

    public Task<SnChatMember> UpdateMyChatNotifyAsync(
        Guid roomId,
        ChatMemberNotifyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PatchAsync<ChatMemberNotifyRequest, SnChatMember>(
            $"/messager/chat/{roomId:D}/members/me/notify",
            request,
            cancellationToken);
    }

    public Task<SnChatMember> UpdateMyChatProfileAsync(
        Guid roomId,
        ChatMemberProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PatchAsync<ChatMemberProfileRequest, SnChatMember>(
            $"/messager/chat/{roomId:D}/members/me/profile",
            request,
            cancellationToken);
    }

    public Task InviteToChatRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
        => PostAsync($"/messager/chat/invites/{roomId:D}", cancellationToken);

    public async Task<List<SnChatRoom>> GetChatInvitesAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/messager/chat/invites", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnChatRoom>(json);
    }

    public Task AcceptChatInviteAsync(Guid roomId, CancellationToken cancellationToken = default)
        => PostAsync($"/messager/chat/invites/{roomId:D}/accept", cancellationToken);

    public Task DeclineChatInviteAsync(Guid roomId, CancellationToken cancellationToken = default)
        => PostAsync($"/messager/chat/invites/{roomId:D}/decline", cancellationToken);

    public async Task<List<RoomSubscriptionEntry>> GetRoomSubscriptionsAsync(
        Guid roomId,
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync($"/messager/chat/{roomId:D}/subscriptions", cancellationToken)
            .ConfigureAwait(false);
        return JsonListParser.ParseList<RoomSubscriptionEntry>(json);
    }

    public async Task<List<AccountSubscriptionEntry>> GetMyChatSubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/messager/chat/accounts/me/subscriptions", cancellationToken)
            .ConfigureAwait(false);
        return JsonListParser.ParseList<AccountSubscriptionEntry>(json);
    }

    public Task<ChatAccountStatusResponse> GetMyChatStatusAsync(CancellationToken cancellationToken = default)
        => GetAsync<ChatAccountStatusResponse>("/messager/chat/accounts/me/status", cancellationToken);

    public async Task<List<SnChatGroup>> GetChatGroupsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/messager/chat/groups", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnChatGroup>(json);
    }

    public Task<SnChatGroup> UpdateChatGroupAsync(
        Guid groupId,
        UpdateGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PatchAsync<UpdateGroupRequest, SnChatGroup>(
            $"/messager/chat/groups/{groupId:D}",
            request,
            cancellationToken);
    }

    public Task DeleteChatGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/messager/chat/groups/{groupId:D}", cancellationToken);

    public Task MoveRoomToGroupAsync(Guid roomId, Guid? groupId, CancellationToken cancellationToken = default)
    {
        var body = new MoveToGroupRequest { GroupId = groupId };
        return PatchAsync($"/messager/chat/rooms/{roomId:D}/group", body, cancellationToken);
    }

    public async Task<List<Autocompletion>> AutocompleteChatAsync(
        Guid roomId,
        string content,
        CancellationToken cancellationToken = default)
    {
        var body = new AutocompletionRequest { Content = content };
        var json = await PostForStringAsync(
                $"/messager/chat/{roomId:D}/autocomplete",
                body,
                cancellationToken)
            .ConfigureAwait(false);
        return JsonListParser.ParseList<Autocompletion>(json);
    }

    public async Task<List<ChatBotCommand>> GetBotCommandsAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        // OpenAPI: object map { "botKey": [ SnBotCommand, ... ], ... } — sometimes a bare array.
        var json = await GetStringAsync($"/messager/chat/{roomId:D}/bots/commands", cancellationToken)
            .ConfigureAwait(false);
        return ParseBotCommandsMap(json);
    }

    private static List<ChatBotCommand> ParseBotCommandsMap(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var list = new List<ChatBotCommand>();

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in root.EnumerateArray())
                {
                    try
                    {
                        var cmd = el.Deserialize<ChatBotCommand>(JsonDefaults.Options);
                        if (cmd is not null)
                        {
                            list.Add(cmd);
                        }
                    }
                    catch (JsonException)
                    {
                        // skip
                    }
                }

                return list;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                // Prefer nested data if present
                if (root.TryGetProperty("data", out var data) && data.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    return ParseBotCommandsMap(data.GetRawText());
                }

                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in prop.Value.EnumerateArray())
                        {
                            try
                            {
                                var cmd = el.Deserialize<ChatBotCommand>(JsonDefaults.Options);
                                if (cmd is null)
                                {
                                    continue;
                                }

                                cmd.BotKey = prop.Name;
                                list.Add(cmd);
                            }
                            catch (JsonException)
                            {
                                // skip
                            }
                        }
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        try
                        {
                            var cmd = prop.Value.Deserialize<ChatBotCommand>(JsonDefaults.Options);
                            if (cmd is not null)
                            {
                                cmd.BotKey ??= prop.Name;
                                list.Add(cmd);
                            }
                        }
                        catch (JsonException)
                        {
                            // skip
                        }
                    }
                }
            }

            return list;
        }
        catch (JsonException)
        {
            return JsonListParser.ParseList<ChatBotCommand>(json);
        }
    }

    public Task MarkDeviceJoinedRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
        => PostAsync($"/messager/chat/{roomId:D}/devices/me/joined", cancellationToken);

    public Task EnableRoomE2eeAsync(Guid roomId, int encryptionMode = 3, CancellationToken cancellationToken = default)
    {
        var body = new EnableE2eeRequest { EncryptionMode = encryptionMode };
        return PostAsync($"/messager/chat/{roomId:D}/e2ee/enable", body, cancellationToken);
    }

    public Task EnableRoomMlsAsync(Guid roomId, string? mlsGroupId = null, CancellationToken cancellationToken = default)
    {
        var body = new EnableMlsRequest { MlsGroupId = mlsGroupId };
        return PostAsync($"/messager/chat/{roomId:D}/mls/enable", body, cancellationToken);
    }

    public async Task<List<SnChatRoom>> GetRealmChatRoomsAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var json = await GetStringAsync($"/messager/realms/{Uri.EscapeDataString(slug)}/chat", cancellationToken)
            .ConfigureAwait(false);
        return JsonListParser.ParseList<SnChatRoom>(json);
    }

    public Task<JoinCallResponse> JoinRealtimeCallAsync(Guid roomId, CancellationToken cancellationToken = default)
        => GetAsync<JoinCallResponse>($"/messager/chat/realtime/{roomId:D}/join", cancellationToken);

    public async Task<JoinCallResponse?> GetRealtimeCallAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<JoinCallResponse>($"/messager/chat/realtime/{roomId:D}", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<CallParticipant>> GetRealtimeParticipantsAsync(
        Guid roomId,
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync($"/messager/chat/realtime/{roomId:D}/participants", cancellationToken)
            .ConfigureAwait(false);
        return JsonListParser.ParseList<CallParticipant>(json);
    }

    public Task InviteToRealtimeCallAsync(Guid roomId, Guid targetAccountId, CancellationToken cancellationToken = default)
        => PostAsync($"/messager/chat/realtime/{roomId:D}/invite/{targetAccountId:D}", cancellationToken);

    public Task KickFromRealtimeCallAsync(
        Guid roomId,
        Guid targetAccountId,
        KickParticipantRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"/messager/chat/realtime/{roomId:D}/kick/{targetAccountId:D}";
        return request is null
            ? PostAsync(path, cancellationToken)
            : PostAsync(path, request, cancellationToken);
    }

    public Task MuteRealtimeParticipantAsync(Guid roomId, Guid targetAccountId, CancellationToken cancellationToken = default)
        => PostAsync($"/messager/chat/realtime/{roomId:D}/mute/{targetAccountId:D}", cancellationToken);

    public Task UnmuteRealtimeParticipantAsync(Guid roomId, Guid targetAccountId, CancellationToken cancellationToken = default)
        => PostAsync($"/messager/chat/realtime/{roomId:D}/unmute/{targetAccountId:D}", cancellationToken);

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
        bool recycled = false,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 50 : take;
        offset = Math.Max(0, offset);
        var recycledFlag = recycled ? "true" : "false";
        var query = $"offset={offset}&take={take}&order=date&orderDesc=true&recycled={recycledFlag}";

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
        var ids = NormalizeFileIds(fileIds);
        var body = new FileBatchIdsRequest { FileIds = ids };
        return PostWithDriveFallbackAsync("/drive/files/recycle/batch", "/api/files/recycle/batch", body, cancellationToken);
    }

    public Task RestoreFilesAsync(IEnumerable<string> fileIds, CancellationToken cancellationToken = default)
    {
        var ids = NormalizeFileIds(fileIds);
        var body = new FileBatchIdsRequest { FileIds = ids };
        return PostWithDriveFallbackAsync("/drive/files/restore/batch", "/api/files/restore/batch", body, cancellationToken);
    }

    public Task DeleteFilesPermanentlyAsync(IEnumerable<string> fileIds, CancellationToken cancellationToken = default)
    {
        var ids = NormalizeFileIds(fileIds);
        var body = new FileBatchIdsRequest { FileIds = ids };
        return PostWithDriveFallbackAsync("/drive/files/delete/batch", "/api/files/delete/batch", body, cancellationToken);
    }

    public Task MoveFilesAsync(
        IEnumerable<string> fileIds,
        string? parentId,
        bool? indexed = null,
        CancellationToken cancellationToken = default)
    {
        var ids = NormalizeFileIds(fileIds);
        var body = new MoveFilesRequest
        {
            FileIds = ids,
            ParentId = string.IsNullOrWhiteSpace(parentId) ? null : parentId,
            Indexed = indexed,
        };
        return PostWithDriveFallbackAsync("/drive/files/move/batch", "/api/files/move/batch", body, cancellationToken);
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

    public async Task<SnCloudFile> UploadFileChunkedAsync(
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

        // Materialize bytes for hash + chunking + retry.
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

        progress?.Report(0.02);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

        const long defaultChunk = 5 * 1024 * 1024;
        var createBody = new CreateUploadTaskRequest
        {
            Hash = hash,
            FileName = fileName,
            FileSize = fileSize,
            ContentType = contentType,
            ChunkSize = defaultChunk,
            ParentId = string.IsNullOrWhiteSpace(parentId) ? null : parentId,
            Index = true,
        };

        CreateUploadTaskResponse task;
        try
        {
            task = await PostAsync<CreateUploadTaskRequest, CreateUploadTaskResponse>(
                    "/drive/files/upload/create",
                    createBody,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            try
            {
                task = await PostAsync<CreateUploadTaskRequest, CreateUploadTaskResponse>(
                        "/api/files/upload/create",
                        createBody,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (SolarApiException)
            {
                // Gateway may not expose chunked create — fall back to direct.
                await using var ms = new MemoryStream(bytes);
                return await UploadFileDirectAsync(ms, fileName, contentType, fileSize, parentId, progress, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // Instant dedupe hit: server returned existing CloudFile-shaped payload.
        if (task.IsExistingFile || (string.IsNullOrWhiteSpace(task.TaskId) && !string.IsNullOrWhiteSpace(task.Id)))
        {
            progress?.Report(1);
            try
            {
                return await GetAsync<SnCloudFile>(
                        $"/drive/files/{Uri.EscapeDataString(task.Id!)}",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (SolarApiException)
            {
                return new SnCloudFile
                {
                    Id = task.Id,
                    Name = task.Name ?? fileName,
                    Size = fileSize,
                    MimeType = contentType,
                };
            }
        }

        if (string.IsNullOrWhiteSpace(task.TaskId))
        {
            // Ambiguous response — try deserialize as SnCloudFile via re-post complete path or direct.
            await using var ms = new MemoryStream(bytes);
            return await UploadFileDirectAsync(ms, fileName, contentType, fileSize, parentId, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        var chunkSize = task.ChunkSize > 0 ? task.ChunkSize : defaultChunk;
        var chunksCount = task.ChunksCount > 0
            ? task.ChunksCount
            : (int)Math.Ceiling(bytes.LongLength / (double)chunkSize);

        for (var i = 0; i < chunksCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var offset = (int)(i * chunkSize);
            if (offset >= bytes.Length)
            {
                break;
            }

            var length = (int)Math.Min(chunkSize, bytes.LongLength - offset);
            var slice = new byte[length];
            Buffer.BlockCopy(bytes, offset, slice, 0, length);

            await UploadChunkAsync(task.TaskId!, i, slice, cancellationToken).ConfigureAwait(false);
            // Progress: 5% create + 90% chunks + 5% complete
            progress?.Report(0.05 + 0.90 * ((i + 1) / (double)chunksCount));
        }

        progress?.Report(0.96);
        try
        {
            return await PostAsync<SnCloudFile>(
                    $"/drive/files/upload/complete/{Uri.EscapeDataString(task.TaskId)}",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            return await PostAsync<SnCloudFile>(
                    $"/api/files/upload/complete/{Uri.EscapeDataString(task.TaskId)}",
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task UploadChunkAsync(string taskId, int chunkIndex, byte[] chunkBytes, CancellationToken cancellationToken)
    {
        using var multipart = new MultipartFormDataContent();
        var part = new ByteArrayContent(chunkBytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(part, "chunk", $"chunk_{chunkIndex}");

        var drivePath = $"/drive/files/upload/chunk/{Uri.EscapeDataString(taskId)}/{chunkIndex}";
        try
        {
            using var response = await SendCoreAsync(HttpMethod.Post, drivePath, multipart, allowRefresh: true, cancellationToken)
                .ConfigureAwait(false);
            await EnsureSuccessAsync(response).ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            using var multipart2 = new MultipartFormDataContent();
            var part2 = new ByteArrayContent(chunkBytes);
            part2.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            multipart2.Add(part2, "chunk", $"chunk_{chunkIndex}");
            var apiPath = $"/api/files/upload/chunk/{Uri.EscapeDataString(taskId)}/{chunkIndex}";
            using var response = await SendCoreAsync(HttpMethod.Post, apiPath, multipart2, allowRefresh: true, cancellationToken)
                .ConfigureAwait(false);
            await EnsureSuccessAsync(response).ConfigureAwait(false);
        }
    }

    private async Task PostWithDriveFallbackAsync<TBody>(
        string drivePath,
        string apiPath,
        TBody body,
        CancellationToken cancellationToken)
    {
        try
        {
            await PostAsync(drivePath, body, cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            await PostAsync(apiPath, body, cancellationToken).ConfigureAwait(false);
        }
    }

    private static List<string> NormalizeFileIds(IEnumerable<string> fileIds)
    {
        var ids = fileIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            throw new ArgumentException("At least one file id is required.", nameof(fileIds));
        }

        return ids;
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

    public async Task<SnWallet?> GetWalletAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await GetStringAsync("/wallet/wallets", cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<SnWallet>(json, JsonDefaults.Options);
        }
        catch (SolarApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<SnWallet>> GetWalletsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/wallet/wallets/all", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnWallet>(json);
    }

    public async Task<List<SnWalletTransaction>> GetTransactionsAsync(Guid walletId, int offset, int take, CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);
        var path = $"/wallet/wallets/transactions?wallet={walletId:D}&offset={offset}&take={take}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnWalletTransaction>(json);
    }

    // —— Sphere / Feed ——

    public async Task<List<SnPost>> GetPostsAsync(
        int offset,
        int take,
        CancellationToken cancellationToken = default,
        string? tag = null,
        string? category = null,
        string? pub = null,
        string? query = null)
    {
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);
        var qs = new List<string> { $"offset={offset}", $"take={take}" };
        if (!string.IsNullOrWhiteSpace(tag))
        {
            qs.Add($"tags={Uri.EscapeDataString(tag.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            qs.Add($"categories={Uri.EscapeDataString(category.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(pub))
        {
            qs.Add($"pub={Uri.EscapeDataString(pub.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            qs.Add($"query={Uri.EscapeDataString(query.Trim())}");
        }

        var path = $"/sphere/posts?{string.Join('&', qs)}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPost>(json);
    }

    public async Task<SnPostSubscription?> GetPostSubscriptionAsync(
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SnPostSubscription>(
                $"/sphere/posts/{postId:D}/subscription",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<SnPost>> GetHomeTimelineAsync(
        int offset = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        // Live gateway (2026):
        // - /sphere/timeline returns { items: [ { type: "posts.new", data: SnPost } ] }
        // - /sphere/timeline/home and /me are often 404 on api.solian.app
        // - /sphere/posts is the reliable public list (offset/take)
        // Prefer public posts first so the feed is never empty due to event-shape quirks;
        // still merge timeline events when public returns nothing (e.g. filtered home).
        take = take <= 0 ? 40 : take;
        offset = Math.Max(0, offset);

        try
        {
            var publicPosts = await GetPostsAsync(offset, take, cancellationToken).ConfigureAwait(false);
            if (publicPosts.Count > 0)
            {
                return publicPosts;
            }
        }
        catch (SolarApiException)
        {
            // try timeline paths below
        }

        foreach (var path in new[]
                 {
                     $"/sphere/timeline/home?offset={offset}&take={take}",
                     $"/sphere/timeline/me?offset={offset}&take={take}",
                 })
        {
            try
            {
                var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
                var list = JsonListParser.ParseList<SnPost>(json);
                if (list.Count > 0)
                {
                    return list;
                }
            }
            catch (SolarApiException)
            {
                // try next
            }
        }

        // Event feed: GET /sphere/timeline?take=
        try
        {
            var qs = new List<string> { $"take={take}" };
            if (offset > 0)
            {
                qs.Add($"offset={offset}");
            }

            var pageJson = await GetStringAsync(
                $"/sphere/timeline?{string.Join('&', qs)}",
                cancellationToken).ConfigureAwait(false);
            var page = JsonSerializer.Deserialize<SnTimelinePage>(pageJson, JsonDefaults.Options);
            var fromEvents = TimelinePostExtractor.ExtractPosts(page);
            if (fromEvents.Count > 0)
            {
                return fromEvents;
            }

            var bare = JsonListParser.ParseList<SnPost>(pageJson);
            if (bare.Count > 0)
            {
                return bare;
            }
        }
        catch (SolarApiException)
        {
            // fall through
        }

        return [];
    }

    public async Task<SnTimelinePage> GetTimelineAsync(
        string? cursor = null,
        int take = 20,
        string? mode = null,
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 40 : take;
        var qs = new List<string> { $"take={take}" };
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            qs.Add($"cursor={Uri.EscapeDataString(cursor)}");
        }

        if (!string.IsNullOrWhiteSpace(mode))
        {
            qs.Add($"mode={Uri.EscapeDataString(mode)}");
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            qs.Add($"filter={Uri.EscapeDataString(filter)}");
        }

        var path = $"/sphere/timeline?{string.Join('&', qs)}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<SnTimelinePage>(json, JsonDefaults.Options)
               ?? new SnTimelinePage();
    }

    public Task<SnPost> GetPostAsync(Guid postId, CancellationToken cancellationToken = default)
        => GetAsync<SnPost>($"/sphere/posts/{postId:D}", cancellationToken);

    public async Task<List<SnPost>> GetPostRepliesAsync(
        Guid postId,
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);
        var path = $"/sphere/posts/{postId:D}/replies?offset={offset}&take={take}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPost>(json);
    }

    public Task<PostThreadResponse> GetPostThreadAsync(Guid postId, CancellationToken cancellationToken = default)
        => GetAsync<PostThreadResponse>($"/sphere/posts/{postId:D}/thread?ancestors=true&take=20", cancellationToken);

    public Task<SnPost> CreatePostAsync(CreatePostRequest request, string? pub = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = "/sphere/posts";
        if (!string.IsNullOrWhiteSpace(pub))
        {
            path += $"?pub={Uri.EscapeDataString(pub)}";
        }

        return PostAsync<CreatePostRequest, SnPost>(path, request, cancellationToken);
    }

    public async Task<SnPostReaction?> ReactToPostAsync(
        Guid postId,
        PostReactionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Symbol))
        {
            throw new ArgumentException("Reaction symbol is required.", nameof(request));
        }

        // Toggle: add returns 200 + body; remove returns 204 No Content.
        using var response = await SendCoreAsync(
                HttpMethod.Post,
                $"/sphere/posts/{postId:D}/reactions",
                JsonContent.Create(request, options: JsonDefaults.Options),
                allowRefresh: true,
                cancellationToken)
            .ConfigureAwait(false);

        await EnsureSuccessAsync(response).ConfigureAwait(false);

        if (response.StatusCode is System.Net.HttpStatusCode.NoContent
            or System.Net.HttpStatusCode.ResetContent)
        {
            return null;
        }

        // Some gateways may return empty 200 on remove.
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text) || text is "null" or "{}")
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<SnPostReaction>(text, JsonDefaults.Options);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new SolarApiException("Failed to deserialize reaction response.", response.StatusCode, text, ex);
        }
    }

    public async Task<List<SnPostReaction>> GetPostReactionsAsync(
        Guid postId,
        int offset,
        int take,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);
        var path = $"/sphere/posts/{postId:D}/reactions?offset={offset}&take={take}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPostReaction>(json);
    }

    public async Task BoostPostAsync(Guid postId, string? content = null, CancellationToken cancellationToken = default)
    {
        // Body is optional; avoid failing on response shape (SnBoost / Instant variance).
        var body = new BoostRequest { Content = content };
        using var response = await SendCoreAsync(
                HttpMethod.Post,
                $"/sphere/posts/{postId:D}/boost",
                JsonContent.Create(body, options: JsonDefaults.Options),
                allowRefresh: true,
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public Task UnboostPostAsync(Guid postId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/sphere/posts/{postId:D}/boost", cancellationToken);

    public Task<SnPostBookmark> BookmarkPostAsync(Guid postId, CancellationToken cancellationToken = default)
        => PostAsync<SnPostBookmark>($"/sphere/posts/{postId:D}/bookmark", cancellationToken);

    public Task UnbookmarkPostAsync(Guid postId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/sphere/posts/{postId:D}/bookmark", cancellationToken);

    public async Task<SnPostBookmark?> GetPostBookmarkAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SnPostBookmark>($"/sphere/posts/{postId:D}/bookmark", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<SnPublisher>> GetAccountPublishersAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var path = $"/sphere/publishers/of/{accountId:D}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPublisher>(json);
    }

    public async Task<List<SnPublisher>> GetMyPublishersAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/sphere/publishers", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPublisher>(json);
    }

    public Task<SnPublisher> GetPublisherAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Publisher name is required.", nameof(name));
        }

        return GetAsync<SnPublisher>(
            $"/sphere/publishers/{Uri.EscapeDataString(name.Trim().TrimStart('@'))}",
            cancellationToken);
    }

    public async Task<List<SnPublisher>> SearchPublishersAsync(
        string query,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        take = take <= 0 ? 20 : Math.Min(take, 50);
        var path =
            $"/sphere/publishers/search?query={Uri.EscapeDataString(query.Trim())}&take={take}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPublisher>(json);
    }

    public Task<SnPublisher> CreateIndividualPublisherAsync(
        PublisherRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<PublisherRequest, SnPublisher>("/sphere/publishers/individual", request, cancellationToken);
    }

    public Task<SnPublisher> UpdatePublisherAsync(
        string name,
        PublisherRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PatchAsync<PublisherRequest, SnPublisher>(
            $"/sphere/publishers/{Uri.EscapeDataString(name.Trim())}",
            request,
            cancellationToken);
    }

    public Task DeletePublisherAsync(string name, CancellationToken cancellationToken = default)
        => DeleteAsync($"/sphere/publishers/{Uri.EscapeDataString(name.Trim())}", cancellationToken);

    public async Task<List<SnPublisherMember>> GetPublisherMembersAsync(
        string name,
        int offset = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 50 : take;
        offset = Math.Max(0, offset);
        var path =
            $"/sphere/publishers/{Uri.EscapeDataString(name.Trim())}/members?offset={offset}&take={take}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPublisherMember>(json);
    }

    public async Task<SnPublisherMember?> GetMyPublisherMembershipAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SnPublisherMember>(
                $"/sphere/publishers/{Uri.EscapeDataString(name.Trim())}/members/me",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task RemovePublisherMemberAsync(string name, Guid memberId, CancellationToken cancellationToken = default)
        => DeleteAsync(
            $"/sphere/publishers/{Uri.EscapeDataString(name.Trim())}/members/{memberId:D}",
            cancellationToken);

    public Task LeavePublisherAsync(string name, CancellationToken cancellationToken = default)
        => DeleteAsync(
            $"/sphere/publishers/{Uri.EscapeDataString(name.Trim())}/members/me",
            cancellationToken);

    public Task UpdatePublisherMemberRoleAsync(
        string name,
        Guid memberId,
        PublisherMemberRole role,
        CancellationToken cancellationToken = default)
        => PatchAsync(
            $"/sphere/publishers/{Uri.EscapeDataString(name.Trim())}/members/{memberId:D}/role",
            (int)role,
            cancellationToken);

    public Task InvitePublisherMemberAsync(
        string name,
        PublisherMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync(
            $"/sphere/publishers/invites/{Uri.EscapeDataString(name.Trim())}",
            request,
            cancellationToken);
    }

    public async Task<List<SnPublisherMember>> GetPublisherInvitesAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/sphere/publishers/invites", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPublisherMember>(json);
    }

    public Task AcceptPublisherInviteAsync(string name, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/publishers/invites/{Uri.EscapeDataString(name.Trim())}/accept", cancellationToken);

    public Task DeclinePublisherInviteAsync(string name, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/publishers/invites/{Uri.EscapeDataString(name.Trim())}/decline", cancellationToken);

    public async Task<PublisherStats?> GetPublisherStatsAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<PublisherStats>(
                $"/sphere/publishers/{Uri.EscapeDataString(name.Trim())}/stats",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Dictionary<string, bool>> GetPublisherFeaturesAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await GetStringAsync(
                $"/sphere/publishers/{Uri.EscapeDataString(name.Trim())}/features",
                cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            }

            return JsonSerializer.Deserialize<Dictionary<string, bool>>(json, JsonDefaults.Options)
                   ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
        catch (SolarApiException)
        {
            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public Task<SnPublisherFeature> AddPublisherFeatureAsync(
        string name,
        PublisherFeatureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<PublisherFeatureRequest, SnPublisherFeature>(
            $"/sphere/publishers/{Uri.EscapeDataString(name.Trim())}/features",
            request,
            cancellationToken);
    }

    public Task SubscribePublisherAsync(string name, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/publishers/{Uri.EscapeDataString(name.Trim())}/subscribe", cancellationToken);

    public Task UnsubscribePublisherAsync(string name, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/publishers/{Uri.EscapeDataString(name.Trim())}/unsubscribe", cancellationToken);

    public async Task<SnPublisherSubscription?> GetPublisherSubscriptionAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SnPublisherSubscription>(
                $"/sphere/publishers/{Uri.EscapeDataString(name.Trim())}/subscription",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<SnPublisherSubscription>> GetMyPublisherSubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/sphere/publishers/subscriptions", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPublisherSubscription>(json);
    }

    public Task SubscribePostAsync(Guid postId, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/posts/{postId:D}/subscribe", cancellationToken);

    public Task UnsubscribePostAsync(Guid postId, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/posts/{postId:D}/unsubscribe", cancellationToken);

    public async Task<List<SnPostSubscription>> GetMyPostSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/sphere/posts/subscriptions", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPostSubscription>(json);
    }

    public Task SubscribeTagAsync(string slug, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/posts/tags/{Uri.EscapeDataString(slug.Trim())}/subscribe", cancellationToken);

    public Task UnsubscribeTagAsync(string slug, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/posts/tags/{Uri.EscapeDataString(slug.Trim())}/unsubscribe", cancellationToken);

    public Task SubscribeCategoryAsync(string slug, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/posts/categories/{Uri.EscapeDataString(slug.Trim())}/subscribe", cancellationToken);

    public Task UnsubscribeCategoryAsync(string slug, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/posts/categories/{Uri.EscapeDataString(slug.Trim())}/unsubscribe", cancellationToken);

    public async Task<List<SnPost>> GetBookmarkedPostsAsync(
        int offset = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);
        var json = await GetStringAsync(
            $"/sphere/posts/bookmarks?offset={offset}&take={take}",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPost>(json);
    }

    public async Task<List<SnPost>> GetDraftPostsAsync(
        int offset = 0,
        int take = 20,
        string? pub = null,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);
        var path = $"/sphere/posts/drafts?offset={offset}&take={take}";
        if (!string.IsNullOrWhiteSpace(pub))
        {
            path += $"&pub={Uri.EscapeDataString(pub.Trim())}";
        }

        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPost>(json);
    }

    public async Task<List<SnPost>> GetFeaturedPostsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/sphere/posts/featured", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPost>(json);
    }

    public async Task<List<SnPostTag>> GetPostTagsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/sphere/posts/tags", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPostTag>(json);
    }

    public async Task<SnPostTag?> GetPostTagAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SnPostTag>(
                $"/sphere/posts/tags/{Uri.EscapeDataString(slug.Trim())}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<SnPostCategory>> GetPostCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/sphere/posts/categories", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPostCategory>(json);
    }

    public async Task<SnPostCategory?> GetPostCategoryAsync(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SnPostCategory>(
                $"/sphere/posts/categories/{Uri.EscapeDataString(slug.Trim())}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<SnPostCollection>> GetPublisherCollectionsAsync(
        string publisherName,
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(
            $"/sphere/publishers/{Uri.EscapeDataString(publisherName.Trim())}/collections",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPostCollection>(json);
    }

    public async Task<SnPostCollection?> GetPublisherCollectionAsync(
        string publisherName,
        string slug,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SnPostCollection>(
                $"/sphere/publishers/{Uri.EscapeDataString(publisherName.Trim())}/collections/{Uri.EscapeDataString(slug.Trim())}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<SnPost>> GetCollectionPostsAsync(
        string publisherName,
        string slug,
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(
            $"/sphere/publishers/{Uri.EscapeDataString(publisherName.Trim())}/collections/{Uri.EscapeDataString(slug.Trim())}/posts",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPost>(json);
    }

    public Task SubscribeCollectionAsync(string publisherName, string slug, CancellationToken cancellationToken = default)
        => PostAsync(
            $"/sphere/publishers/{Uri.EscapeDataString(publisherName.Trim())}/collections/{Uri.EscapeDataString(slug.Trim())}/subscribe",
            cancellationToken);

    public Task UnsubscribeCollectionAsync(string publisherName, string slug, CancellationToken cancellationToken = default)
        => PostAsync(
            $"/sphere/publishers/{Uri.EscapeDataString(publisherName.Trim())}/collections/{Uri.EscapeDataString(slug.Trim())}/unsubscribe",
            cancellationToken);

    public async Task<List<StickerPackOwnership>> GetMyStickerPacksAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/sphere/stickers/me", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<StickerPackOwnership>(json);
    }

    public async Task<List<StickerPack>> SearchStickerPacksAsync(
        string query,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        // GET /sphere/stickers returns StickerPack[] (with icon).
        // GET /sphere/stickers/search returns SnSticker[] (individual stickers) — different shape.
        take = take <= 0 ? 20 : take;
        var path = string.IsNullOrWhiteSpace(query)
            ? $"/sphere/stickers?take={take}"
            : $"/sphere/stickers?query={Uri.EscapeDataString(query.Trim())}&take={take}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<StickerPack>(json);
    }

    public async Task<List<SnSticker>> SearchStickersAsync(
        string query,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 20 : take;
        var path = string.IsNullOrWhiteSpace(query)
            ? $"/sphere/stickers/search?take={take}"
            : $"/sphere/stickers/search?query={Uri.EscapeDataString(query.Trim())}&take={take}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnSticker>(json);
    }

    public async Task<List<SnSticker>> GetStickerPackContentAsync(Guid packId, CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(
            $"/sphere/stickers/{packId:D}/content",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnSticker>(json);
    }

    public Task OwnStickerPackAsync(Guid packId, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/stickers/{packId:D}/own", cancellationToken);

    public async Task<SnSticker?> LookupStickerAsync(string identifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        // Solian stores stickers as :prefix+slug: — lookup path wants the inner identifier.
        var key = identifier.Trim();
        if (key.StartsWith(':') && key.EndsWith(':') && key.Length > 2)
        {
            key = key[1..^1];
        }

        try
        {
            return await GetAsync<SnSticker>(
                    $"/sphere/stickers/lookup/{Uri.EscapeDataString(key)}",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            return null;
        }
    }

    public async Task<List<SnStickerBatchLookupItem>> LookupStickersBatchAsync(
        IEnumerable<string> placeholders,
        CancellationToken cancellationToken = default)
    {
        var list = placeholders
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (list.Count == 0)
        {
            return [];
        }

        try
        {
            using var response = await SendCoreAsync(
                    HttpMethod.Post,
                    "/sphere/stickers/lookup/batch",
                    JsonContent.Create(new BatchStickerLookupRequest { Placeholders = list }, options: JsonDefaults.Options),
                    allowRefresh: true,
                    cancellationToken)
                .ConfigureAwait(false);
            await EnsureSuccessAsync(response).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonListParser.ParseList<SnStickerBatchLookupItem>(json);
        }
        catch (SolarApiException)
        {
            // Fall back to single lookups when batch is unavailable.
            var results = new List<SnStickerBatchLookupItem>();
            foreach (var ph in list)
            {
                var sticker = await LookupStickerAsync(ph, cancellationToken).ConfigureAwait(false);
                if (sticker is not null)
                {
                    results.Add(new SnStickerBatchLookupItem { Placeholder = ph, Sticker = sticker });
                }
            }

            return results;
        }
    }

    public async Task<List<SnPostAward>> GetPostAwardsAsync(
        Guid postId,
        int offset = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);
        var json = await GetStringAsync(
            $"/sphere/posts/{postId:D}/awards?offset={offset}&take={take}",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPostAward>(json);
    }

    public Task AwardPostAsync(Guid postId, PostAwardRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync($"/sphere/posts/{postId:D}/awards", request, cancellationToken);
    }

    public Task SponsorPostAsync(Guid postId, PostSponsorRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync($"/sphere/posts/{postId:D}/sponsor", request, cancellationToken);
    }

    // —— Passport / Social ——

    public Task<SnAccount> GetAccountByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Account name is required.", nameof(name));
        }

        return GetAsync<SnAccount>($"/passport/accounts/{Uri.EscapeDataString(name.Trim())}", cancellationToken);
    }

    public async Task<List<SnAccountBadge>> GetAccountBadgesAsync(string name, CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(
            $"/passport/accounts/{Uri.EscapeDataString(name.Trim())}/badges",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAccountBadge>(json);
    }

    public async Task<List<SnAccountBoardItem>> GetAccountBoardAsync(string name, CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(
            $"/passport/accounts/{Uri.EscapeDataString(name.Trim())}/board",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAccountBoardItem>(json);
    }

    public async Task<List<PublicAccountConnectionResponse>> GetAccountConnectionsAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(
            $"/passport/accounts/{Uri.EscapeDataString(name.Trim())}/connections",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<PublicAccountConnectionResponse>(json);
    }

    public async Task<SnAccountStatus?> GetAccountStatusAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SnAccountStatus>(
                $"/passport/accounts/{Uri.EscapeDataString(name.Trim())}/statuses",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<SnAccountBadge>> GetMyBadgesAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/accounts/me/badges", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAccountBadge>(json);
    }

    public Task ActivateMyBadgeAsync(Guid badgeId, CancellationToken cancellationToken = default)
        => PostAsync($"/passport/accounts/me/badges/{badgeId:D}/active", cancellationToken);

    public async Task<List<SnAccountBoardItem>> GetMyBoardAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/accounts/me/board", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAccountBoardItem>(json);
    }

    public async Task<List<SnActionLog>> GetMyActionsAsync(int offset = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);
        var json = await GetStringAsync(
            $"/passport/accounts/me/actions?offset={offset}&take={take}",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnActionLog>(json);
    }

    public async Task<List<SnExperienceRecord>> GetMyLevelingAsync(int offset = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);
        var json = await GetStringAsync(
            $"/passport/accounts/me/leveling?offset={offset}&take={take}",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnExperienceRecord>(json);
    }

    public async Task<List<SnSocialCreditRecord>> GetMyCreditsHistoryAsync(
        int offset = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);
        var json = await GetStringAsync(
            $"/passport/accounts/me/credits/history?offset={offset}&take={take}",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnSocialCreditRecord>(json);
    }

    public async Task<List<ProgressionAchievementState>> GetMyAchievementsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/accounts/me/progression/achievements", cancellationToken)
            .ConfigureAwait(false);
        return JsonListParser.ParseList<ProgressionAchievementState>(json);
    }

    public async Task<ProgressionAchievementStats?> GetMyAchievementStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<ProgressionAchievementStats>(
                "/passport/accounts/me/progression/achievements/stats",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<ProgressionQuestState>> GetMyQuestsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/accounts/me/progression/quests", cancellationToken)
            .ConfigureAwait(false);
        return JsonListParser.ParseList<ProgressionQuestState>(json);
    }

    public async Task<List<SnProgressRewardGrant>> GetMyProgressGrantsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/accounts/me/progression/grants", cancellationToken)
            .ConfigureAwait(false);
        return JsonListParser.ParseList<SnProgressRewardGrant>(json);
    }

    public async Task<List<SnAccountRelationship>> GetRelationshipsAsync(
        int offset = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 50 : take;
        offset = Math.Max(0, offset);
        var json = await GetStringAsync(
            $"/passport/relationships?offset={offset}&take={take}",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAccountRelationship>(json);
    }

    public async Task<List<SnAccountRelationship>> GetRelationshipRequestsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/relationships/requests", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAccountRelationship>(json);
    }

    public async Task<List<FriendOverviewItem>> GetFriendsOverviewAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/friends/overview", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<FriendOverviewItem>(json);
    }

    public async Task<List<SnAccount>> GetCloseFriendsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/relationships/close-friends", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAccount>(json);
    }

    public async Task<SnAccountRelationship?> GetRelationshipAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SnAccountRelationship>(
                $"/passport/relationships/{accountId:D}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<InspectRelationshipResponse?> InspectRelationshipAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<InspectRelationshipResponse>(
                $"/passport/relationships/inspect/{accountId:D}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<SnAccountRelationship> SendFriendRequestAsync(Guid accountId, CancellationToken cancellationToken = default)
        => PostAsync<SnAccountRelationship>($"/passport/relationships/{accountId:D}/friends", cancellationToken);

    public Task CancelFriendRequestAsync(Guid accountId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/passport/relationships/{accountId:D}/friends", cancellationToken);

    public Task AcceptFriendRequestAsync(Guid accountId, CancellationToken cancellationToken = default)
        => PostAsync($"/passport/relationships/{accountId:D}/friends/accept", cancellationToken);

    public Task DeclineFriendRequestAsync(Guid accountId, CancellationToken cancellationToken = default)
        => PostAsync($"/passport/relationships/{accountId:D}/friends/decline", cancellationToken);

    public Task BlockAccountAsync(
        Guid accountId,
        RelationshipActionRequest? request = null,
        CancellationToken cancellationToken = default)
        => PostAsync(
            $"/passport/relationships/{accountId:D}/block",
            request ?? new RelationshipActionRequest(),
            cancellationToken);

    public Task UnblockAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/passport/relationships/{accountId:D}/block", cancellationToken);

    public Task MuteAccountAsync(
        Guid accountId,
        RelationshipActionRequest? request = null,
        CancellationToken cancellationToken = default)
        => PostAsync(
            $"/passport/relationships/{accountId:D}/mute",
            request ?? new RelationshipActionRequest(),
            cancellationToken);

    public Task UnmuteAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/passport/relationships/{accountId:D}/mute", cancellationToken);

    public Task SetCloseFriendAsync(Guid accountId, bool isCloseFriend, CancellationToken cancellationToken = default)
        => isCloseFriend
            ? PostAsync($"/passport/relationships/{accountId:D}/close-friend", cancellationToken)
            : DeleteAsync($"/passport/relationships/{accountId:D}/close-friend", cancellationToken);

    public Task SetRelationshipAliasAsync(Guid accountId, string? alias, CancellationToken cancellationToken = default)
        => PatchAsync(
            $"/passport/relationships/{accountId:D}/alias",
            new AliasRequest { Alias = alias },
            cancellationToken);

    public Task RemoveRelationshipAsync(Guid accountId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/passport/relationships/{accountId:D}", cancellationToken);

    public async Task<List<SnRealm>> GetMyRealmsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/realms", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnRealm>(json);
    }

    public async Task<List<SnRealm>> GetPublicRealmsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/realms/public", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnRealm>(json);
    }

    public Task<SnRealm> GetRealmAsync(string slug, CancellationToken cancellationToken = default)
        => GetAsync<SnRealm>($"/passport/realms/{Uri.EscapeDataString(slug.Trim())}", cancellationToken);

    public Task<SnRealm> CreateRealmAsync(RealmRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<RealmRequest, SnRealm>("/passport/realms", request, cancellationToken);
    }

    public async Task<List<SnRealmMember>> GetRealmMembersAsync(string slug, CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(
            $"/passport/realms/{Uri.EscapeDataString(slug.Trim())}/members",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnRealmMember>(json);
    }

    public async Task<List<SnRealmMember>> GetRealmInvitesAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/realms/invites", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnRealmMember>(json);
    }

    public Task InviteToRealmAsync(string slug, RealmMemberRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync(
            $"/passport/realms/invites/{Uri.EscapeDataString(slug.Trim())}",
            request,
            cancellationToken);
    }

    public Task AcceptRealmInviteAsync(string slug, CancellationToken cancellationToken = default)
        => PostAsync($"/passport/realms/invites/{Uri.EscapeDataString(slug.Trim())}/accept", cancellationToken);

    public Task DeclineRealmInviteAsync(string slug, CancellationToken cancellationToken = default)
        => PostAsync($"/passport/realms/invites/{Uri.EscapeDataString(slug.Trim())}/decline", cancellationToken);

    public Task LeaveRealmAsync(string slug, CancellationToken cancellationToken = default)
        => DeleteAsync($"/passport/realms/{Uri.EscapeDataString(slug.Trim())}/members/me", cancellationToken);

    public Task JoinRealmAsync(string slug, CancellationToken cancellationToken = default)
        => PostAsync($"/passport/realms/{Uri.EscapeDataString(slug.Trim())}/members/me", cancellationToken);

    public async Task<List<SnRealmRolePermission>> GetRealmRolePermissionsAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(
            $"/passport/realms/{Uri.EscapeDataString(slug.Trim())}/permissions/roles",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnRealmRolePermission>(json);
    }

    public async Task<RealmQuotaResponse?> GetRealmQuotaAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<RealmQuotaResponse>("/passport/realms/quota", cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<DailyEventResponse?> GetMyCalendarDayAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<DailyEventResponse>("/passport/accounts/me/calendar", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<SnUserCalendarEvent>> GetMyCalendarEventsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var qs = new List<string>();
        if (from is not null)
        {
            qs.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        }

        if (to is not null)
        {
            qs.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        }

        var path = "/passport/accounts/me/calendar/events";
        if (qs.Count > 0)
        {
            path += "?" + string.Join("&", qs);
        }

        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnUserCalendarEvent>(json);
    }

    public async Task<List<EventCountdownItem>> GetMyCalendarCountdownAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/accounts/me/calendar/countdown", cancellationToken)
            .ConfigureAwait(false);
        return JsonListParser.ParseList<EventCountdownItem>(json);
    }

    public Task<SnUserCalendarEvent> CreateCalendarEventAsync(
        CreateCalendarEventRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<CreateCalendarEventRequest, SnUserCalendarEvent>(
            "/passport/accounts/me/calendar/events",
            request,
            cancellationToken);
    }

    public Task DeleteCalendarEventAsync(Guid eventId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/passport/accounts/me/calendar/events/{eventId:D}", cancellationToken);

    public async Task<List<SnTicket>> GetMyTicketsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/tickets/me", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnTicket>(json);
    }

    public Task<SnTicket> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
        => GetAsync<SnTicket>($"/passport/tickets/{ticketId:D}", cancellationToken);

    public Task<SnTicket> CreateTicketAsync(CreateTicketRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<CreateTicketRequest, SnTicket>("/passport/tickets", request, cancellationToken);
    }

    public Task<SnTicketMessage> AddTicketMessageAsync(
        Guid ticketId,
        AddTicketMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<AddTicketMessageRequest, SnTicketMessage>(
            $"/passport/tickets/{ticketId:D}/messages",
            request,
            cancellationToken);
    }

    public async Task<int> GetTicketCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<int>("/passport/tickets/count", cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            return 0;
        }
    }

    public async Task<List<SnLocationPin>> GetMyPinsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/pins/me", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnLocationPin>(json);
    }

    public async Task<List<SnLocationPin>> GetNearbyPinsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/pins/nearby", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnLocationPin>(json);
    }

    public Task<SnLocationPin> CreatePinAsync(CreatePinRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<CreatePinRequest, SnLocationPin>("/passport/pins", request, cancellationToken);
    }

    public Task DeletePinAsync(Guid pinId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/passport/pins/{pinId:D}", cancellationToken);

    public async Task<List<SnMeet>> GetMyMeetsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/meets", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnMeet>(json);
    }

    public async Task<List<SnMeet>> GetNearbyMeetsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/meets/nearby", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnMeet>(json);
    }

    public Task<SnMeet> CreateMeetAsync(CreateMeetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<CreateMeetRequest, SnMeet>("/passport/meets", request, cancellationToken);
    }

    public Task JoinMeetAsync(Guid meetId, CancellationToken cancellationToken = default)
        => PostAsync($"/passport/meets/{meetId:D}/join", cancellationToken);

    public Task CompleteMeetAsync(Guid meetId, CancellationToken cancellationToken = default)
        => PostAsync($"/passport/meets/{meetId:D}/complete", cancellationToken);

    public Task DeleteMeetAsync(Guid meetId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/passport/meets/{meetId:D}", cancellationToken);

    public async Task<List<NfcTagResponse>> GetMyNfcTagsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/nfc/tags", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<NfcTagResponse>(json);
    }

    public async Task<NfcResolveResponse?> LookupNfcAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        try
        {
            return await GetAsync<NfcResolveResponse>(
                $"/passport/nfc/lookup?q={Uri.EscapeDataString(query.Trim())}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // —— Passport extras: fortune / IP / notable-days / rewind / spells ——

    public async Task<List<FortuneSaying>> GetFortuneSayingsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/passport/fortune", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<FortuneSaying>(json);
    }

    public async Task<List<FortuneSaying>> GetRandomFortuneAsync(
        string? language = "zh",
        CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(language)
            ? "/passport/fortune/random"
            : $"/passport/fortune/random?language={Uri.EscapeDataString(language.Trim())}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<FortuneSaying>(json);
    }

    public async Task<IpCheckResponse?> GetIpCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<IpCheckResponse>("/passport/ip-check", cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<GeoIpResponse?> GetIpGeoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<GeoIpResponse>("/passport/ip-check/geo", cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<SnNotableDay>> GetNotableDaysAsync(
        int? year = null,
        string region = "CN",
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 50 : take;
        var y = year ?? DateTime.Now.Year;
        var path =
            $"/passport/notable-days?year={y}&region={Uri.EscapeDataString(region)}&take={take}";
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnNotableDay>(json);
    }

    public Task<SnNotableDay> CreateNotableDayAsync(
        NotableDayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<NotableDayRequest, SnNotableDay>("/passport/notable-days", request, cancellationToken);
    }

    public Task DeleteNotableDayAsync(Guid id, CancellationToken cancellationToken = default)
        => DeleteAsync($"/passport/notable-days/{id:D}", cancellationToken);

    public async Task<SnRewindPoint?> GetMyRewindAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SnRewindPoint>("/passport/rewind/me", cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<SnRewindPoint?> GetRewindByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        try
        {
            return await GetAsync<SnRewindPoint>(
                $"/passport/rewind/{Uri.EscapeDataString(code.Trim())}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<SnRewindPoint> PublishRewindPublicAsync(int year, CancellationToken cancellationToken = default)
        => PostAsync<SnRewindPoint>($"/passport/rewind/me/{year}/public", cancellationToken);

    public Task<SnRewindPoint> PublishRewindPrivateAsync(int year, CancellationToken cancellationToken = default)
        => PostAsync<SnRewindPoint>($"/passport/rewind/me/{year}/private", cancellationToken);

    public async Task<SnMagicSpell?> LookupSpellAsync(string spellWord, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(spellWord))
        {
            return null;
        }

        try
        {
            return await GetAsync<SnMagicSpell>(
                $"/passport/spells/{Uri.EscapeDataString(spellWord.Trim())}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task ApplySpellAsync(
        string spellWord,
        MagicSpellApplyRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(spellWord);
        var body = request ?? new MagicSpellApplyRequest();
        return PostAsync(
            $"/passport/spells/{Uri.EscapeDataString(spellWord.Trim())}/apply",
            body,
            cancellationToken);
    }

    public Task ResendSpellActivationAsync(CancellationToken cancellationToken = default)
        => PostAsync("/passport/spells/activation/resend", cancellationToken);

    public Task ResendSpellAsync(Guid spellId, CancellationToken cancellationToken = default)
        => PostAsync($"/passport/spells/{spellId:D}/resend", cancellationToken);

    // —— Personality / 寻思 ——

    public async Task<List<ThoughtAgent>> GetThoughtAgentsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/personality/agents", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<ThoughtAgent>(json);
    }

    public async Task<List<ThoughtConversation>> GetThoughtConversationsAsync(
        int offset = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 20 : take;
        offset = Math.Max(0, offset);
        var json = await GetStringAsync(
            $"/personality/conversations?offset={offset}&take={take}",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<ThoughtConversation>(json);
    }

    public Task<ThoughtConversation> CreateThoughtConversationAsync(
        CreateThoughtConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<CreateThoughtConversationRequest, ThoughtConversation>(
            "/personality/conversations",
            request,
            cancellationToken);
    }

    public async Task<List<ThoughtMessage>> GetThoughtMessagesAsync(
        string conversationId,
        int offset = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        take = take <= 0 ? 50 : take;
        offset = Math.Max(0, offset);
        var json = await GetStringAsync(
            $"/personality/conversations/{Uri.EscapeDataString(conversationId.Trim())}/messages?offset={offset}&take={take}",
            cancellationToken).ConfigureAwait(false);
        var list = JsonListParser.ParseList<ThoughtMessage>(json);
        // Solian SDK reverses to chronological; ensure oldest → newest for UI.
        return list.OrderBy(m => m.Sequence).ThenBy(m => m.CreatedAt).ToList();
    }

    public Task<ThoughtMessage> AddThoughtMessageAsync(
        string conversationId,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        return PostAsync<AddThoughtMessageRequest, ThoughtMessage>(
            $"/personality/conversations/{Uri.EscapeDataString(conversationId.Trim())}/messages",
            new AddThoughtMessageRequest { Content = content },
            cancellationToken);
    }

    public async Task<ThoughtRunResult> RunThoughtAsync(
        string conversationId,
        ThoughtRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        ArgumentNullException.ThrowIfNull(request);

        // Official Solian client uses SSE stream=true for assistant replies.
        request.Stream = true;
        var path = $"/personality/conversations/{Uri.EscapeDataString(conversationId.Trim())}/runs";

        using var response = await SendCoreAsync(
                HttpMethod.Post,
                path,
                JsonContent.Create(request, options: JsonDefaults.Options),
                allowRefresh: true,
                cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var isJson = mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);

        // Prefer SSE (Solian default). JSON only when Content-Type is clearly JSON.
        if (!isJson)
        {
            return await ReadThoughtSseAsync(response, conversationId, request.Model, cancellationToken)
                .ConfigureAwait(false);
        }

        // JSON body (stream=false style responses)
        try
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(json) && json.TrimStart().StartsWith('{'))
            {
                var parsed = JsonSerializer.Deserialize<ThoughtRunResponse>(json, JsonDefaults.Options);
                if (parsed is not null)
                {
                    return new ThoughtRunResult
                    {
                        Content = parsed.Content
                            ?? parsed.ResponseMessage?.Content
                            ?? string.Empty,
                        Model = parsed.Run?.Model ?? request.Model,
                        ConversationId = parsed.Thread?.Id ?? conversationId,
                        ConversationTitle = parsed.Thread?.Title,
                        UsedStream = false,
                    };
                }
            }
        }
        catch
        {
            // caller may reload messages
        }

        return new ThoughtRunResult
        {
            Content = string.Empty,
            Model = request.Model,
            ConversationId = conversationId,
            UsedStream = false,
        };
    }

    /// <summary>
    /// Parse Solian personality run SSE:
    /// event: message.delta / message.completed / run.failed / …
    /// </summary>
    private static async Task<ThoughtRunResult> ReadThoughtSseAsync(
        HttpResponseMessage response,
        string conversationId,
        string? fallbackModel,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        var text = new System.Text.StringBuilder();
        string? eventName = null;
        string? model = null;
        string? title = null;
        string? convId = conversationId;
        var sawSse = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                eventName = null;
                continue;
            }

            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                eventName = line["event:".Length..].Trim();
                sawSse = true;
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            sawSse = true;
            var payload = line["data:".Length..].Trim();
            if (payload is "" or "[DONE]")
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                var ev = eventName ?? string.Empty;

                if (ev is "message.delta" or "text.delta" or "content.delta")
                {
                    if (root.TryGetProperty("delta", out var delta))
                    {
                        text.Append(delta.GetString());
                    }
                    else if (root.TryGetProperty("content", out var c))
                    {
                        text.Append(c.GetString());
                    }
                    else if (root.TryGetProperty("text", out var t))
                    {
                        text.Append(t.GetString());
                    }
                }
                else if (ev is "message.completed" or "message.complete" or "run.completed")
                {
                    if (root.TryGetProperty("content", out var content))
                    {
                        var full = content.GetString();
                        if (!string.IsNullOrEmpty(full))
                        {
                            text.Clear();
                            text.Append(full);
                        }
                    }

                    if (root.TryGetProperty("model", out var m))
                    {
                        model = m.GetString();
                    }
                }
                else if (ev is "run.started")
                {
                    if (root.TryGetProperty("conversation_id", out var cid))
                    {
                        convId = cid.GetString() ?? convId;
                    }

                    if (root.TryGetProperty("model", out var m2))
                    {
                        model = m2.GetString() ?? model;
                    }
                }
                else if (ev is "run.failed" or "error")
                {
                    var err = root.TryGetProperty("error", out var e)
                        ? e.GetString()
                        : root.TryGetProperty("message", out var msg)
                            ? msg.GetString()
                            : "Run failed";
                    throw new SolarApiException(err ?? "Run failed", HttpStatusCode.BadGateway);
                }
                else if (string.IsNullOrEmpty(ev))
                {
                    // Some gateways omit event: and only send data JSON with type/content
                    if (root.TryGetProperty("type", out var typeEl))
                    {
                        var type = typeEl.GetString() ?? string.Empty;
                        if (type.Contains("delta", StringComparison.OrdinalIgnoreCase)
                            && root.TryGetProperty("delta", out var d2))
                        {
                            text.Append(d2.GetString());
                        }
                        else if (type.Contains("completed", StringComparison.OrdinalIgnoreCase)
                                 && root.TryGetProperty("content", out var c2))
                        {
                            var full = c2.GetString();
                            if (!string.IsNullOrEmpty(full))
                            {
                                text.Clear();
                                text.Append(full);
                            }
                        }
                    }
                    else if (root.TryGetProperty("content", out var onlyContent)
                             && text.Length == 0)
                    {
                        text.Append(onlyContent.GetString());
                    }
                }

                if (root.TryGetProperty("title", out var titleEl))
                {
                    title = titleEl.GetString() ?? title;
                }
            }
            catch (SolarApiException)
            {
                throw;
            }
            catch (JsonException)
            {
                // non-JSON data line — ignore
            }
        }

        return new ThoughtRunResult
        {
            Content = text.ToString(),
            Model = model ?? fallbackModel,
            ConversationId = convId,
            ConversationTitle = title,
            UsedStream = sawSse,
        };
    }

    // —— Padlock / Security ——

    public async Task<List<SnAuthClientWithSessions>> GetDevicesAsync(
        int offset = 0,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 50 : take;
        offset = Math.Max(0, offset);
        var json = await GetStringAsync(
            $"/padlock/devices?offset={offset}&take={take}",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAuthClientWithSessions>(json);
    }

    public Task DeleteDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/padlock/devices/{deviceId:D}", cancellationToken);

    public Task UpdateDeviceLabelAsync(Guid deviceId, string label, CancellationToken cancellationToken = default)
        => PatchAsync(
            $"/padlock/devices/{deviceId:D}/label",
            new DeviceLabelRequest { Label = label },
            cancellationToken);

    public Task UpdateCurrentDeviceLabelAsync(string label, CancellationToken cancellationToken = default)
        => PatchAsync(
            "/padlock/devices/current/label",
            new DeviceLabelRequest { Label = label },
            cancellationToken);

    public async Task<List<SnAuthSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/padlock/sessions", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAuthSession>(json);
    }

    public async Task<SnAuthSession?> GetCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SnAuthSession>("/padlock/sessions/current", cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/padlock/sessions/{sessionId:D}", cancellationToken);

    public async Task<List<SnAuthSession>> GetSessionChildrenAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(
            $"/padlock/sessions/{sessionId:D}/children",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAuthSession>(json);
    }

    public async Task<List<SnAccountContact>> GetContactsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/padlock/contacts", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAccountContact>(json);
    }

    public Task<SnAccountContact> CreateContactAsync(ContactRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<ContactRequest, SnAccountContact>("/padlock/contacts", request, cancellationToken);
    }

    public Task DeleteContactAsync(Guid contactId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/padlock/contacts/{contactId:D}", cancellationToken);

    public Task SetContactPrimaryAsync(Guid contactId, CancellationToken cancellationToken = default)
        => PostAsync($"/padlock/contacts/{contactId:D}/primary", cancellationToken);

    public Task SetContactPublicAsync(Guid contactId, bool isPublic, CancellationToken cancellationToken = default)
        => PatchAsync(
            $"/padlock/contacts/{contactId:D}/public",
            new { is_public = isPublic },
            cancellationToken);

    public Task<SnAccountContact> VerifyContactAsync(Guid contactId, string code, CancellationToken cancellationToken = default)
        => PostAsync<ContactVerifyRequest, SnAccountContact>(
            $"/padlock/contacts/{contactId:D}/verify",
            new ContactVerifyRequest { Code = code },
            cancellationToken);

    public Task RequestContactVerificationAsync(Guid contactId, CancellationToken cancellationToken = default)
        => PostAsync($"/padlock/contacts/{contactId:D}/verify", cancellationToken);

    public async Task<List<AuthorizedAppResponse>> GetAuthorizedAppsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/padlock/authorized-apps", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<AuthorizedAppResponse>(json);
    }

    public Task RevokeAuthorizedAppAsync(Guid id, CancellationToken cancellationToken = default)
        => DeleteAsync($"/padlock/authorized-apps/{id:D}", cancellationToken);

    public async Task<List<SnApiKey>> GetApiKeysAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/padlock/api-keys", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnApiKey>(json);
    }

    public Task<SnApiKey> CreateApiKeyAsync(CreateApiKeyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<CreateApiKeyRequest, SnApiKey>("/padlock/api-keys", request, cancellationToken);
    }

    public Task DeleteApiKeyAsync(Guid id, CancellationToken cancellationToken = default)
        => DeleteAsync($"/padlock/api-keys/{id:D}", cancellationToken);

    public Task<SnApiKey> RotateApiKeyAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync<SnApiKey>($"/padlock/api-keys/{id:D}/rotate", cancellationToken);

    public async Task<List<SnAccountConnection>> GetConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/padlock/connections", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAccountConnection>(json);
    }

    public Task DeleteConnectionAsync(Guid id, CancellationToken cancellationToken = default)
        => DeleteAsync($"/padlock/connections/{id:D}", cancellationToken);

    public Task SetConnectionVisibilityAsync(Guid id, bool isPublic, CancellationToken cancellationToken = default)
        => PatchAsync(
            $"/padlock/connections/{id:D}/visibility",
            new ConnectionVisibilityRequest { IsPublic = isPublic },
            cancellationToken);

    public async Task<List<SnAccountAuthFactor>> GetAccountFactorsAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/padlock/factors", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAccountAuthFactor>(json);
    }

    public Task EnableFactorAsync(Guid factorId, CancellationToken cancellationToken = default)
        => PostAsync($"/padlock/factors/{factorId:D}/enable", cancellationToken);

    public Task DisableFactorAsync(Guid factorId, CancellationToken cancellationToken = default)
        => PostAsync($"/padlock/factors/{factorId:D}/disable", cancellationToken);

    public Task DeleteFactorAsync(Guid factorId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/padlock/factors/{factorId:D}", cancellationToken);

    public async Task<string> StartPasskeyRegistrationAsync(CancellationToken cancellationToken = default)
    {
        // POST /padlock/factors/passkey/start → WebAuthn creation options JSON
        return await GetStringViaPostAsync("/padlock/factors/passkey/start", cancellationToken).ConfigureAwait(false);
    }

    public Task CompletePasskeyRegistrationAsync(string credentialJson, CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(credentialJson) ? "{}" : credentialJson);
        return PostAsync("/padlock/factors/passkey/complete", doc.RootElement.Clone(), cancellationToken);
    }

    public async Task<string> StartPasskeyAuthenticationAsync(
        Guid challengeId,
        CancellationToken cancellationToken = default)
    {
        return await GetStringViaPostAsync(
            $"/padlock/auth/challenge/{challengeId:D}/passkey/start",
            cancellationToken).ConfigureAwait(false);
    }

    public Task<SnAuthChallenge> CompletePasskeyAuthenticationAsync(
        Guid challengeId,
        string credentialJson,
        CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(credentialJson) ? "{}" : credentialJson);
        return PostAsync<JsonElement, SnAuthChallenge>(
            $"/padlock/auth/challenge/{challengeId:D}/passkey/complete",
            doc.RootElement.Clone(),
            cancellationToken);
    }

    public Task<QrGenerateResponse> GenerateQrLoginAsync(CancellationToken cancellationToken = default)
    {
        var body = new QrGenerateRequest
        {
            DeviceId = DeviceInfoHelper.GetDeviceId(),
            DeviceName = DeviceInfoHelper.GetDeviceName(),
            Platform = ClientPlatform.Windows,
        };
        return PostAsync<QrGenerateRequest, QrGenerateResponse>("/padlock/auth/qr/generate", body, cancellationToken);
    }

    public Task<QrStatusResponse> GetQrLoginStatusAsync(Guid qrChallengeId, CancellationToken cancellationToken = default)
        => GetAsync<QrStatusResponse>($"/padlock/auth/qr/{qrChallengeId:D}", cancellationToken);

    public Task ScanQrLoginAsync(Guid qrChallengeId, CancellationToken cancellationToken = default)
        => PostAsync($"/padlock/auth/qr/{qrChallengeId:D}/scan", cancellationToken);

    public Task ApproveQrLoginAsync(Guid qrChallengeId, CancellationToken cancellationToken = default)
        => PostAsync($"/padlock/auth/qr/{qrChallengeId:D}/approve", cancellationToken);

    public Task DeclineQrLoginAsync(Guid qrChallengeId, CancellationToken cancellationToken = default)
        => PostAsync($"/padlock/auth/qr/{qrChallengeId:D}/decline", cancellationToken);

    public async Task<List<SnAuthChallenge>> GetPendingChallengesAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/padlock/auth/challenge/pending", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAuthChallenge>(json);
    }

    public Task ApproveChallengeAsync(Guid challengeId, CancellationToken cancellationToken = default)
        => PostAsync($"/padlock/auth/challenge/{challengeId:D}/approve", cancellationToken);

    public Task DeclineChallengeAsync(Guid challengeId, CancellationToken cancellationToken = default)
        => PostAsync($"/padlock/auth/challenge/{challengeId:D}/decline", cancellationToken);

    public Task<SnAuthChallenge> GetAuthChallengeAsync(Guid challengeId, CancellationToken cancellationToken = default)
        => GetAsync<SnAuthChallenge>($"/padlock/auth/challenge/{challengeId:D}", cancellationToken);

    public async Task<List<SnAccountAuthFactor>> GetChallengeFactorsAsync(
        Guid challengeId,
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(
            $"/padlock/auth/challenge/{challengeId:D}/factors",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAccountAuthFactor>(json);
    }

    public Task RequestChallengeFactorAsync(
        Guid challengeId,
        Guid factorId,
        CancellationToken cancellationToken = default)
        => PostAsync($"/padlock/auth/challenge/{challengeId:D}/factors/{factorId:D}", cancellationToken);

    public Task<SnAuthChallenge> SubmitChallengeFactorAsync(
        Guid challengeId,
        Guid factorId,
        string secret,
        CancellationToken cancellationToken = default)
    {
        var request = new PerformChallengeRequest
        {
            FactorId = factorId,
            Password = secret,
        };
        return PatchAsync<PerformChallengeRequest, SnAuthChallenge>(
            $"/padlock/auth/challenge/{challengeId:D}",
            request,
            cancellationToken);
    }

    public Task<CaptchaConfigResponse> GetCaptchaConfigAsync(CancellationToken cancellationToken = default)
        => GetAsync<CaptchaConfigResponse>("/padlock/auth/captcha", cancellationToken);

    public Task VerifyCaptchaTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return PostAsync(
            "/padlock/auth/captcha/verify",
            new CaptchaVerifyRequest { Token = token.Trim() },
            cancellationToken);
    }

    public Task<SnAccount> RegisterAccountAsync(AccountCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<AccountCreateRequest, SnAccount>("/padlock/accounts", request, cancellationToken);
    }

    public Task<TokenExchangeResponse> RecoverAccountAsync(RecoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<RecoveryRequest, TokenExchangeResponse>("/padlock/auth/recover", request, cancellationToken);
    }

    public Task<PasskeyLoginStartResponse> StartPasskeyLoginAsync(CancellationToken cancellationToken = default)
    {
        var body = new PasskeyLoginStartRequest
        {
            DeviceId = DeviceInfoHelper.GetDeviceId(),
            DeviceName = DeviceInfoHelper.GetDeviceName(),
            Platform = ClientPlatform.Windows,
        };
        return PostAsync<PasskeyLoginStartRequest, PasskeyLoginStartResponse>(
            "/padlock/auth/passkey/start",
            body,
            cancellationToken);
    }

    public Task<SnAuthChallenge> CompletePasskeyLoginAsync(
        Guid authChallengeId,
        PasskeyAuthenticationCompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<PasskeyAuthenticationCompleteRequest, SnAuthChallenge>(
            $"/padlock/auth/passkey/{authChallengeId:D}/complete",
            request,
            cancellationToken);
    }

    public async Task<string> StartPasskeyRegistrationWithDeviceAsync(CancellationToken cancellationToken = default)
    {
        var body = new PasskeyRegistrationStartRequest
        {
            DeviceId = DeviceInfoHelper.GetDeviceId(),
            DeviceName = DeviceInfoHelper.GetDeviceName(),
        };
        using var response = await SendCoreAsync(
            HttpMethod.Post,
            "/padlock/factors/passkey/start",
            JsonContent.Create(body, options: JsonDefaults.Options),
            allowRefresh: true,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task CompletePasskeyRegistrationDetailedAsync(
        PasskeyRegistrationCompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync("/padlock/factors/passkey/complete", request, cancellationToken);
    }

    public string BuildSocialLoginUrl(string provider, string returnUrl, string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        var p = Uri.EscapeDataString(provider.Trim().ToLowerInvariant());
        var q =
            $"returnUrl={Uri.EscapeDataString(returnUrl)}"
            + $"&deviceId={Uri.EscapeDataString(deviceId)}"
            + "&flow=login";
        return $"{BaseUrl.TrimEnd('/')}/padlock/auth/login/{p}?{q}";
    }

    // —— Sphere surveys ——

    public async Task<List<SnSurvey>> GetMySurveysAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/sphere/surveys/me", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnSurvey>(json);
    }

    public async Task<List<SnSurvey>> GetSurveysAsync(int offset = 0, int take = 30, CancellationToken cancellationToken = default)
    {
        take = take <= 0 ? 30 : take;
        offset = Math.Max(0, offset);
        var json = await GetStringAsync($"/sphere/surveys?offset={offset}&take={take}", cancellationToken)
            .ConfigureAwait(false);
        return JsonListParser.ParseList<SnSurvey>(json);
    }

    public Task<SnSurvey> GetSurveyAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<SnSurvey>($"/sphere/surveys/{id:D}", cancellationToken);

    public Task<SnSurvey> CreateSurveyAsync(SurveyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<SurveyRequest, SnSurvey>("/sphere/surveys", request, cancellationToken);
    }

    public Task<SnSurvey> UpdateSurveyAsync(Guid id, SurveyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PatchAsync<SurveyRequest, SnSurvey>($"/sphere/surveys/{id:D}", request, cancellationToken);
    }

    public Task DeleteSurveyAsync(Guid id, CancellationToken cancellationToken = default)
        => DeleteAsync($"/sphere/surveys/{id:D}", cancellationToken);

    public Task PublishSurveyAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/surveys/{id:D}/publish", cancellationToken);

    public Task ArchiveSurveyAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/surveys/{id:D}/archive", cancellationToken);

    public Task<SnSurvey> CloneSurveyAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync<SnSurvey>($"/sphere/surveys/{id:D}/clone", cancellationToken);

    public Task AnswerSurveyAsync(Guid id, SurveyAnswerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync($"/sphere/surveys/{id:D}/answer", request, cancellationToken);
    }

    public Task SubscribeSurveyAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/surveys/{id:D}/subscribe", cancellationToken);

    public Task UnsubscribeSurveyAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/surveys/{id:D}/unsubscribe", cancellationToken);

    public async Task<List<SnSurveyAnswer>> GetSurveyFeedbackAsync(
        Guid id, int offset = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(
            $"/sphere/surveys/{id:D}/feedback?offset={Math.Max(0, offset)}&take={(take <= 0 ? 20 : take)}",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnSurveyAnswer>(json);
    }

    // —— Scrap / translate ——

    public Task<ScrapLinkResult> ScrapLinkAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return PostAsync<ScrapLinkRequest, ScrapLinkResult>(
            "/sphere/scrap/link",
            new ScrapLinkRequest { Url = url.Trim() },
            cancellationToken);
    }

    public Task ClearScrapLinkCacheAsync(CancellationToken cancellationToken = default)
        => DeleteAsync("/sphere/scrap/link/cache", cancellationToken);

    public Task ClearAllScrapCacheAsync(CancellationToken cancellationToken = default)
        => DeleteAsync("/sphere/scrap/cache/all", cancellationToken);

    public async Task<TranslateResult> TranslateTextAsync(
        string text,
        string targetLang = "zh",
        string? sourceLang = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var body = new TranslateRequest
        {
            Text = text,
            Content = text,
            TargetLang = targetLang,
            To = targetLang,
            SourceLang = sourceLang,
        };
        try
        {
            return await PostAsync<TranslateRequest, TranslateResult>("/sphere/translate", body, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            // Some deployments return plain string
            var json = await PostForStringAsync("/sphere/translate", body, cancellationToken).ConfigureAwait(false);
            return new TranslateResult { Text = json, Translated = json };
        }
    }

    // —— Quote authorizations ——

    public Task<QuoteAuthorizationItem> CreateQuoteAuthorizationAsync(
        CreateQuoteAuthorizationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<CreateQuoteAuthorizationRequest, QuoteAuthorizationItem>(
            "/sphere/quote-authorizations", request, cancellationToken);
    }

    public Task DeleteQuoteAuthorizationAsync(Guid id, CancellationToken cancellationToken = default)
        => DeleteAsync($"/sphere/quote-authorizations/{id:D}", cancellationToken);

    public async Task<QuoteAuthorizationItem?> GetQuoteAuthorizationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<QuoteAuthorizationItem>($"/sphere/quote-authorizations/{id:D}", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    // —— Fediverse ——

    public async Task<List<SnFediverseActor>> SearchFediverseActorsAsync(
        string query, int take = 20, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        take = take <= 0 ? 20 : take;
        var json = await GetStringAsync(
            $"/sphere/fediverse/actors/search?query={Uri.EscapeDataString(query.Trim())}&take={take}",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnFediverseActor>(json);
    }

    public Task<SnFediverseActor> GetFediverseActorAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<SnFediverseActor>($"/sphere/fediverse/actors/{id:D}", cancellationToken);

    public async Task<SnFediverseActor?> LookupFediverseActorAsync(
        string usernameAtInstance, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usernameAtInstance);
        try
        {
            return await GetAsync<SnFediverseActor>(
                $"/sphere/fediverse/actors/{Uri.EscapeDataString(usernameAtInstance.Trim())}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            return null;
        }
    }

    public Task FollowFediverseActorAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/fediverse/actors/{id:D}/follow", cancellationToken);

    public Task UnfollowFediverseActorAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/fediverse/actors/{id:D}/unfollow", cancellationToken);

    public async Task<FediverseRelationship?> GetFediverseRelationshipAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<FediverseRelationship>(
                $"/sphere/fediverse/actors/{id:D}/relationship", cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<SnPost>> GetFediverseActorPostsAsync(
        Guid id, int offset = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(
            $"/sphere/fediverse/actors/{id:D}/posts?offset={Math.Max(0, offset)}&take={(take <= 0 ? 20 : take)}",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPost>(json);
    }

    public async Task<List<FediverseModerationRule>> GetFediverseModerationRulesAsync(
        CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/sphere/fediverse/moderation/rules", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<FediverseModerationRule>(json);
    }

    public Task ToggleFediverseModerationRuleAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/fediverse/moderation/rules/{id:D}/toggle", cancellationToken);

    public async Task<string> CheckFediverseDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return await PostForStringAsync(
            "/sphere/fediverse/moderation/check-domain",
            new { domain = domain.Trim() },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> CheckFediverseActorModerationAsync(
        string actorUri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUri);
        return await PostForStringAsync(
            "/sphere/fediverse/moderation/check-actor",
            new { actor = actorUri.Trim(), uri = actorUri.Trim() },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<string> GetActivityPubActorAsync(CancellationToken cancellationToken = default)
        => GetStringAsync("/sphere/activitypub/actor", cancellationToken);

    public Task<string> GetActivityPubSearchAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        return GetStringAsync(
            $"/sphere/activitypub/search?query={Uri.EscapeDataString(query.Trim())}",
            cancellationToken);
    }

    public Task<string> CheckActivityPubUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        return GetStringAsync(
            $"/sphere/activitypub/check/{Uri.EscapeDataString(username.Trim())}",
            cancellationToken);
    }

    // —— Automod / Ads / Admin ——

    public async Task<List<SnAutomodRule>> GetAutomodRulesAsync(CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync("/sphere/automod/rules", cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnAutomodRule>(json);
    }

    public Task<SnAutomodRule> CreateAutomodRuleAsync(AutomodRuleDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<AutomodRuleDto, SnAutomodRule>("/sphere/automod/rules", request, cancellationToken);
    }

    public Task DeleteAutomodRuleAsync(Guid id, CancellationToken cancellationToken = default)
        => DeleteAsync($"/sphere/automod/rules/{id:D}", cancellationToken);

    public Task<string> TestAutomodRuleAsync(Guid id, string sample, CancellationToken cancellationToken = default)
        => PostForStringAsync(
            $"/sphere/automod/rules/{id:D}/test",
            new { text = sample, content = sample, sample },
            cancellationToken);

    public async Task<List<PublicAdvertisingPostStats>> GetPublisherAdsAsync(
        string name, int offset = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var json = await GetStringAsync(
            $"/sphere/ads/{Uri.EscapeDataString(name.Trim())}?offset={Math.Max(0, offset)}&take={(take <= 0 ? 20 : take)}",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<PublicAdvertisingPostStats>(json);
    }

    public async Task<SphereAdminStats?> GetSphereAdminStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<SphereAdminStats>("/sphere/admin/stats", cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            return null;
        }
    }

    public Task AdminLockPostAsync(Guid postId, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/admin/posts/{postId:D}/lock", cancellationToken);

    public Task AdminShadowbanPostAsync(Guid postId, CancellationToken cancellationToken = default)
        => PostAsync($"/sphere/admin/posts/{postId:D}/shadowban", cancellationToken);

    public Task AdminSetPostVisibilityAsync(Guid postId, int visibility, CancellationToken cancellationToken = default)
        => PatchAsync($"/sphere/admin/posts/{postId:D}/visibility", new { visibility }, cancellationToken);

    public Task AdminShadowbanPublisherAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return PostAsync($"/sphere/admin/publishers/{Uri.EscapeDataString(name.Trim())}/shadowban", cancellationToken);
    }

    public Task AdminVerifyPublisherAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return PostAsync($"/sphere/admin/publishers/{Uri.EscapeDataString(name.Trim())}/verification", cancellationToken);
    }

    public async Task<List<SnPost>> AdminListPostsAsync(
        int offset = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        var json = await GetStringAsync(
            $"/sphere/admin/posts?offset={Math.Max(0, offset)}&take={(take <= 0 ? 20 : take)}",
            cancellationToken).ConfigureAwait(false);
        return JsonListParser.ParseList<SnPost>(json);
    }

    private async Task<string> GetStringViaPostAsync(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(
            HttpMethod.Post,
            relativePath,
            content: null,
            allowRefresh: true,
            cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
