using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// Typed HTTP client for Solar Network gateway (https://api.solian.app).
/// Auto Bearer, 401 refresh, 429 Retry-After, 5xx retry (3 attempts).
/// </summary>
public interface ISolarApiClient
{
    Task SetBearerTokenAsync(string? accessToken, CancellationToken cancellationToken = default);

    Task<TResponse> GetAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default);

    Task<TResponse> PostAsync<TRequest, TResponse>(string relativePath, TRequest body, CancellationToken cancellationToken = default);

    Task PostAsync<TRequest>(string relativePath, TRequest body, CancellationToken cancellationToken = default);

    Task PostAsync(string relativePath, CancellationToken cancellationToken = default);

    Task<TResponse> PostAsync<TResponse>(string relativePath, CancellationToken cancellationToken = default);

    Task<TResponse> PatchAsync<TRequest, TResponse>(string relativePath, TRequest body, CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string relativePath,
        HttpContent? content = null,
        CancellationToken cancellationToken = default);

    // —— Business APIs ——

    Task<SnAccount> GetMeAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /passport/accounts/me — full account with profile.</summary>
    Task<SnAccount> GetPassportMeAsync(CancellationToken cancellationToken = default);

    Task<SnAccountProfile> GetMyProfileAsync(CancellationToken cancellationToken = default);

    Task<SnAccountProfile> UpdateMyProfileAsync(ProfileRequest request, CancellationToken cancellationToken = default);

    Task<SnAccountStatus> GetMyStatusAsync(CancellationToken cancellationToken = default);

    Task<SnAccountStatus> SetMyStatusAsync(StatusRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /passport/accounts/me/check-in — today's check-in if any.</summary>
    Task<SnCheckInResult?> GetCheckInAsync(CancellationToken cancellationToken = default);

    /// <summary>POST /passport/accounts/me/check-in — perform daily check-in.</summary>
    Task<SnCheckInResult> DoCheckInAsync(CancellationToken cancellationToken = default);

    Task<List<SnChatRoom>> GetChatRoomsAsync(CancellationToken cancellationToken = default);

    Task<List<SnChatMessage>> GetMessagesAsync(string roomId, int offset, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /messager/chat/summary — map of roomId → summary (OpenAPI additionalProperties).
    /// </summary>
    Task<Dictionary<string, ChatSummaryResponse>> GetChatSummaryAsync(CancellationToken cancellationToken = default);

    Task SendMessageAsync(string roomId, SendMessageRequest request, CancellationToken cancellationToken = default);

    Task<SyncResponse> SyncRoomMessagesAsync(string roomId, SyncRequest request, CancellationToken cancellationToken = default);

    /// <summary>POST /messager/chat/{roomId}/read — mark room messages as read.</summary>
    Task MarkChatRoomReadAsync(string roomId, CancellationToken cancellationToken = default);

    Task<List<SnNotification>> GetNotificationsAsync(int offset, int take, CancellationToken cancellationToken = default);

    /// <summary>GET /ring/notifications/count — unread count (typically).</summary>
    Task<int> GetNotificationCountAsync(CancellationToken cancellationToken = default);

    /// <summary>POST /ring/notifications/all/read</summary>
    Task MarkAllNotificationsReadAsync(CancellationToken cancellationToken = default);

    // —— Drive / DysonFS ——

    /// <summary>
    /// List files. Root uses GET /drive/files/me (gateway for /api/files/me);
    /// subfolder uses GET /drive/files/{id}/children.
    /// </summary>
    Task<List<SnCloudFile>> GetMyFilesAsync(string? parentId, int offset, int take, CancellationToken cancellationToken = default);

    Task<SnCloudFile> CreateFolderAsync(string name, string? parentId, CancellationToken cancellationToken = default);

    Task<SnCloudFile> RenameFileAsync(string fileId, string newName, CancellationToken cancellationToken = default);

    Task RecycleFilesAsync(IEnumerable<string> fileIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /drive/files/upload/direct multipart upload with progress (0..1).
    /// </summary>
    Task<SnCloudFile> UploadFileDirectAsync(
        Stream content,
        string fileName,
        string contentType,
        long fileSize,
        string? parentId,
        IProgress<double>? progress,
        CancellationToken cancellationToken = default);

    /// <summary>Download remote file bytes (authenticated when needed).</summary>
    Task DownloadToStreamAsync(string url, Stream destination, IProgress<double>? progress, CancellationToken cancellationToken = default);

    /// <summary>GET /wallet/wallets/all — all wallets available to the current account.</summary>
    Task<List<SnWallet>> GetWalletsAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /wallet/wallets — current default wallet.</summary>
    Task<SnWallet?> GetWalletAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /wallet/wallets/transactions — transaction history.</summary>
    Task<List<SnWalletTransaction>> GetTransactionsAsync(Guid walletId, int offset, int take, CancellationToken cancellationToken = default);

    // —— Sphere / Feed ——

    /// <summary>GET /sphere/posts — public feed, offset/take paging.</summary>
    Task<List<SnPost>> GetPostsAsync(int offset, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /sphere/timeline — cursor-based activity feed (OpenAPI SnTimelinePage).
    /// </summary>
    Task<SnTimelinePage> GetTimelineAsync(
        string? cursor = null,
        int take = 20,
        string? mode = null,
        string? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>GET /sphere/posts/{id} — full post detail.</summary>
    Task<SnPost> GetPostAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>GET /sphere/posts/{id}/replies — flat reply list.</summary>
    Task<List<SnPost>> GetPostRepliesAsync(Guid postId, int offset, int take, CancellationToken cancellationToken = default);

    /// <summary>GET /sphere/posts/{id}/thread — ancestors + descendants tree.</summary>
    Task<PostThreadResponse> GetPostThreadAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>POST /sphere/posts — create a post/reply; pub selects the publisher.</summary>
    Task<SnPost> CreatePostAsync(CreatePostRequest request, string? pub = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /sphere/posts/{id}/reactions.
    /// Returns the reaction when added; <c>null</c> when removed (HTTP 204).
    /// </summary>
    Task<SnPostReaction?> ReactToPostAsync(Guid postId, PostReactionRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /sphere/posts/{id}/reactions</summary>
    Task<List<SnPostReaction>> GetPostReactionsAsync(Guid postId, int offset, int take, CancellationToken cancellationToken = default);

    /// <summary>POST /sphere/posts/{id}/boost (ActivityPub boost; requires publisher + fediverse actor).</summary>
    Task BoostPostAsync(Guid postId, string? content = null, CancellationToken cancellationToken = default);

    /// <summary>DELETE /sphere/posts/{id}/boost</summary>
    Task UnboostPostAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>POST /sphere/posts/{id}/bookmark</summary>
    Task<SnPostBookmark> BookmarkPostAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>DELETE /sphere/posts/{id}/bookmark</summary>
    Task UnbookmarkPostAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /sphere/posts/{id}/bookmark — current bookmark if any.
    /// Returns null when not bookmarked (404).
    /// </summary>
    Task<SnPostBookmark?> GetPostBookmarkAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>GET /sphere/publishers/of/{accountId} — publishers owned by an account.</summary>
    Task<List<SnPublisher>> GetAccountPublishersAsync(Guid accountId, CancellationToken cancellationToken = default);
}
