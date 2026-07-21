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

    Task PatchAsync<TRequest>(string relativePath, TRequest body, CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    Task DeleteAsync<TRequest>(string relativePath, TRequest body, CancellationToken cancellationToken = default);

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

    /// <summary>GET /passport/accounts/search?query=&amp;take=</summary>
    Task<List<SnAccount>> SearchAccountsAsync(string query, int take = 20, CancellationToken cancellationToken = default);

    // —— Messager ——

    Task<List<SnChatRoom>> GetChatRoomsAsync(CancellationToken cancellationToken = default);

    Task<SnChatRoom> GetChatRoomAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<SnChatRoom> CreateChatRoomAsync(ChatRoomRequest request, CancellationToken cancellationToken = default);

    Task<SnChatRoom> UpdateChatRoomAsync(Guid roomId, ChatRoomRequest request, CancellationToken cancellationToken = default);

    Task DeleteChatRoomAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<SnChatRoom> CreateDirectChatAsync(Guid relatedUserId, CancellationToken cancellationToken = default);

    Task<SnChatRoom?> GetDirectChatAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<List<SnChatMessage>> GetMessagesAsync(string roomId, int offset, int take, CancellationToken cancellationToken = default);

    Task<SnChatMessage> GetMessageAsync(Guid roomId, Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /messager/chat/summary — map of roomId → summary (OpenAPI additionalProperties).
    /// </summary>
    Task<Dictionary<string, ChatSummaryResponse>> GetChatSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /messager/chat/unread</summary>
    Task<int> GetChatUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>POST /messager/chat/read-all</summary>
    Task MarkAllChatRoomsReadAsync(CancellationToken cancellationToken = default);

    Task SendMessageAsync(string roomId, SendMessageRequest request, CancellationToken cancellationToken = default);

    Task EditMessageAsync(Guid roomId, Guid messageId, SendMessageRequest request, CancellationToken cancellationToken = default);

    Task DeleteMessageAsync(Guid roomId, Guid messageId, DeleteMessageRequest? request = null, CancellationToken cancellationToken = default);

    Task ModerateDeleteMessageAsync(Guid roomId, Guid messageId, string? reason = null, CancellationToken cancellationToken = default);

    Task<SnChatReaction> ReactToMessageAsync(Guid roomId, Guid messageId, MessageReactionRequest request, CancellationToken cancellationToken = default);

    Task RemoveMessageReactionAsync(Guid roomId, Guid messageId, string symbol, CancellationToken cancellationToken = default);

    Task<List<SnChatReaction>> GetMessageReactionsAsync(Guid roomId, Guid messageId, int offset = 0, int take = 20, CancellationToken cancellationToken = default);

    Task<SnChatMessagePin> PinMessageAsync(Guid roomId, Guid messageId, DateTimeOffset? expiresAt = null, CancellationToken cancellationToken = default);

    Task UnpinMessageAsync(Guid roomId, Guid pinId, CancellationToken cancellationToken = default);

    Task<List<SnChatMessagePin>> GetPinnedMessagesAsync(Guid roomId, bool includeExpired = false, CancellationToken cancellationToken = default);

    Task<SnChatMessage> SendPlaceholderMessageAsync(Guid roomId, string kind, CancellationToken cancellationToken = default);

    Task<SnChatMessage> RedirectMessagesAsync(Guid roomId, IEnumerable<Guid> messageIds, CancellationToken cancellationToken = default);

    Task<SnChatMessage> SendVoiceMessageAsync(
        Guid roomId,
        Stream file,
        string fileName,
        string contentType,
        int durationMs,
        string? nonce = null,
        CancellationToken cancellationToken = default);

    Task<SyncResponse> SyncRoomMessagesAsync(string roomId, SyncRequest request, CancellationToken cancellationToken = default);

    Task<GlobalSyncResponse> SyncAllChatMessagesAsync(SyncRequest request, CancellationToken cancellationToken = default);

    Task<ChatRoomSyncResponse> SyncChatRoomsAsync(long lastSyncTimestamp, CancellationToken cancellationToken = default);

    /// <summary>POST /messager/chat/{roomId}/read — mark room messages as read.</summary>
    Task MarkChatRoomReadAsync(string roomId, CancellationToken cancellationToken = default);

    Task<List<SnChatMember>> GetChatMembersAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<SnChatMember> GetMyChatMembershipAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<OnlineMembersResponse> GetOnlineMembersAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task LeaveChatRoomAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task RemoveChatMemberAsync(Guid roomId, Guid memberId, CancellationToken cancellationToken = default);

    Task TimeoutChatMemberAsync(Guid roomId, Guid memberId, ChatTimeoutRequest request, CancellationToken cancellationToken = default);

    Task ClearChatMemberTimeoutAsync(Guid roomId, Guid memberId, CancellationToken cancellationToken = default);

    Task TimeoutAccountInRoomAsync(Guid roomId, Guid accountId, ChatTimeoutRequest request, CancellationToken cancellationToken = default);

    Task<SnChatMember> UpdateMyChatNotifyAsync(Guid roomId, ChatMemberNotifyRequest request, CancellationToken cancellationToken = default);

    Task<SnChatMember> UpdateMyChatProfileAsync(Guid roomId, ChatMemberProfileRequest request, CancellationToken cancellationToken = default);

    Task InviteToChatRoomAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<List<SnChatRoom>> GetChatInvitesAsync(CancellationToken cancellationToken = default);

    Task AcceptChatInviteAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task DeclineChatInviteAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<List<RoomSubscriptionEntry>> GetRoomSubscriptionsAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<List<AccountSubscriptionEntry>> GetMyChatSubscriptionsAsync(CancellationToken cancellationToken = default);

    Task<ChatAccountStatusResponse> GetMyChatStatusAsync(CancellationToken cancellationToken = default);

    Task<List<SnChatGroup>> GetChatGroupsAsync(CancellationToken cancellationToken = default);

    Task<SnChatGroup> UpdateChatGroupAsync(Guid groupId, UpdateGroupRequest request, CancellationToken cancellationToken = default);

    Task DeleteChatGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task MoveRoomToGroupAsync(Guid roomId, Guid? groupId, CancellationToken cancellationToken = default);

    Task<List<Autocompletion>> AutocompleteChatAsync(Guid roomId, string content, CancellationToken cancellationToken = default);

    Task<List<ChatBotCommand>> GetBotCommandsAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task MarkDeviceJoinedRoomAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task EnableRoomE2eeAsync(Guid roomId, int encryptionMode = 3, CancellationToken cancellationToken = default);

    Task EnableRoomMlsAsync(Guid roomId, string? mlsGroupId = null, CancellationToken cancellationToken = default);

    Task<List<SnChatRoom>> GetRealmChatRoomsAsync(string slug, CancellationToken cancellationToken = default);

    Task<JoinCallResponse> JoinRealtimeCallAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<JoinCallResponse?> GetRealtimeCallAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task<List<CallParticipant>> GetRealtimeParticipantsAsync(Guid roomId, CancellationToken cancellationToken = default);

    Task InviteToRealtimeCallAsync(Guid roomId, Guid targetAccountId, CancellationToken cancellationToken = default);

    Task KickFromRealtimeCallAsync(Guid roomId, Guid targetAccountId, KickParticipantRequest? request = null, CancellationToken cancellationToken = default);

    Task MuteRealtimeParticipantAsync(Guid roomId, Guid targetAccountId, CancellationToken cancellationToken = default);

    Task UnmuteRealtimeParticipantAsync(Guid roomId, Guid targetAccountId, CancellationToken cancellationToken = default);

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
    Task<List<SnCloudFile>> GetMyFilesAsync(
        string? parentId,
        int offset,
        int take,
        bool recycled = false,
        CancellationToken cancellationToken = default);

    Task<SnCloudFile> CreateFolderAsync(string name, string? parentId, CancellationToken cancellationToken = default);

    Task<SnCloudFile> RenameFileAsync(string fileId, string newName, CancellationToken cancellationToken = default);

    Task RecycleFilesAsync(IEnumerable<string> fileIds, CancellationToken cancellationToken = default);

    Task RestoreFilesAsync(IEnumerable<string> fileIds, CancellationToken cancellationToken = default);

    Task DeleteFilesPermanentlyAsync(IEnumerable<string> fileIds, CancellationToken cancellationToken = default);

    /// <summary>POST /drive/files/move/batch — parentId null = root.</summary>
    Task MoveFilesAsync(IEnumerable<string> fileIds, string? parentId, bool? indexed = null, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Chunked upload: create → chunk × N → complete (default 5MB chunks).
    /// Falls back to direct upload when create is unavailable.
    /// </summary>
    Task<SnCloudFile> UploadFileChunkedAsync(
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

    /// <summary>
    /// GET /sphere/posts — public feed with optional filters (tags/categories/pub/query).
    /// </summary>
    Task<List<SnPost>> GetPostsAsync(
        int offset,
        int take,
        CancellationToken cancellationToken = default,
        string? tag = null,
        string? category = null,
        string? pub = null,
        string? query = null);

    /// <summary>GET /sphere/posts/{id}/subscription — null when not subscribed (404).</summary>
    Task<SnPostSubscription?> GetPostSubscriptionAsync(Guid postId, CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /sphere/timeline/home — home feed of posts (Solian official: offset/take, list of SnPost).
    /// </summary>
    Task<List<SnPost>> GetHomeTimelineAsync(
        int offset = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GET /sphere/timeline — cursor-based activity events (legacy/OpenAPI SnTimelinePage).
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

    // —— Sphere / Publishers management ——

    Task<List<SnPublisher>> GetMyPublishersAsync(CancellationToken cancellationToken = default);

    Task<SnPublisher> GetPublisherAsync(string name, CancellationToken cancellationToken = default);

    Task<List<SnPublisher>> SearchPublishersAsync(string query, int take = 20, CancellationToken cancellationToken = default);

    Task<SnPublisher> CreateIndividualPublisherAsync(PublisherRequest request, CancellationToken cancellationToken = default);

    Task<SnPublisher> UpdatePublisherAsync(string name, PublisherRequest request, CancellationToken cancellationToken = default);

    Task DeletePublisherAsync(string name, CancellationToken cancellationToken = default);

    Task<List<SnPublisherMember>> GetPublisherMembersAsync(string name, int offset = 0, int take = 50, CancellationToken cancellationToken = default);

    Task<SnPublisherMember?> GetMyPublisherMembershipAsync(string name, CancellationToken cancellationToken = default);

    Task RemovePublisherMemberAsync(string name, Guid memberId, CancellationToken cancellationToken = default);

    Task LeavePublisherAsync(string name, CancellationToken cancellationToken = default);

    Task UpdatePublisherMemberRoleAsync(string name, Guid memberId, PublisherMemberRole role, CancellationToken cancellationToken = default);

    Task InvitePublisherMemberAsync(string name, PublisherMemberRequest request, CancellationToken cancellationToken = default);

    Task<List<SnPublisherMember>> GetPublisherInvitesAsync(CancellationToken cancellationToken = default);

    Task AcceptPublisherInviteAsync(string name, CancellationToken cancellationToken = default);

    Task DeclinePublisherInviteAsync(string name, CancellationToken cancellationToken = default);

    Task<PublisherStats?> GetPublisherStatsAsync(string name, CancellationToken cancellationToken = default);

    Task<Dictionary<string, bool>> GetPublisherFeaturesAsync(string name, CancellationToken cancellationToken = default);

    Task<SnPublisherFeature> AddPublisherFeatureAsync(string name, PublisherFeatureRequest request, CancellationToken cancellationToken = default);

    // —— Sphere / Subscribe ——

    Task SubscribePublisherAsync(string name, CancellationToken cancellationToken = default);

    Task UnsubscribePublisherAsync(string name, CancellationToken cancellationToken = default);

    Task<SnPublisherSubscription?> GetPublisherSubscriptionAsync(string name, CancellationToken cancellationToken = default);

    Task<List<SnPublisherSubscription>> GetMyPublisherSubscriptionsAsync(CancellationToken cancellationToken = default);

    Task SubscribePostAsync(Guid postId, CancellationToken cancellationToken = default);

    Task UnsubscribePostAsync(Guid postId, CancellationToken cancellationToken = default);

    Task<List<SnPostSubscription>> GetMyPostSubscriptionsAsync(CancellationToken cancellationToken = default);

    Task SubscribeTagAsync(string slug, CancellationToken cancellationToken = default);

    Task UnsubscribeTagAsync(string slug, CancellationToken cancellationToken = default);

    Task SubscribeCategoryAsync(string slug, CancellationToken cancellationToken = default);

    Task UnsubscribeCategoryAsync(string slug, CancellationToken cancellationToken = default);

    // —— Sphere / Feed extras ——

    Task<List<SnPost>> GetBookmarkedPostsAsync(int offset = 0, int take = 20, CancellationToken cancellationToken = default);

    Task<List<SnPost>> GetDraftPostsAsync(int offset = 0, int take = 20, string? pub = null, CancellationToken cancellationToken = default);

    Task<List<SnPost>> GetFeaturedPostsAsync(CancellationToken cancellationToken = default);

    Task<List<SnPostTag>> GetPostTagsAsync(CancellationToken cancellationToken = default);

    Task<SnPostTag?> GetPostTagAsync(string slug, CancellationToken cancellationToken = default);

    Task<List<SnPostCategory>> GetPostCategoriesAsync(CancellationToken cancellationToken = default);

    Task<SnPostCategory?> GetPostCategoryAsync(string slug, CancellationToken cancellationToken = default);

    Task<List<SnPostCollection>> GetPublisherCollectionsAsync(string publisherName, CancellationToken cancellationToken = default);

    Task<SnPostCollection?> GetPublisherCollectionAsync(string publisherName, string slug, CancellationToken cancellationToken = default);

    Task<List<SnPost>> GetCollectionPostsAsync(string publisherName, string slug, CancellationToken cancellationToken = default);

    Task SubscribeCollectionAsync(string publisherName, string slug, CancellationToken cancellationToken = default);

    Task UnsubscribeCollectionAsync(string publisherName, string slug, CancellationToken cancellationToken = default);

    // —— Sphere / Stickers ——

    Task<List<StickerPackOwnership>> GetMyStickerPacksAsync(CancellationToken cancellationToken = default);

    /// <summary>List/search sticker packs (GET /sphere/stickers) — includes pack icon.</summary>
    Task<List<StickerPack>> SearchStickerPacksAsync(string query, int take = 20, CancellationToken cancellationToken = default);

    /// <summary>Search individual stickers (GET /sphere/stickers/search) — includes sticker image.</summary>
    Task<List<SnSticker>> SearchStickersAsync(string query, int take = 20, CancellationToken cancellationToken = default);

    Task<List<SnSticker>> GetStickerPackContentAsync(Guid packId, CancellationToken cancellationToken = default);

    Task OwnStickerPackAsync(Guid packId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve one sticker by identifier (typically <c>prefix+slug</c> without colons).
    /// GET /sphere/stickers/lookup/{identifier}
    /// </summary>
    Task<SnSticker?> LookupStickerAsync(string identifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch-resolve stickers from markdown placeholders <c>:prefix+slug:</c>.
    /// POST /sphere/stickers/lookup/batch
    /// </summary>
    Task<List<SnStickerBatchLookupItem>> LookupStickersBatchAsync(
        IEnumerable<string> placeholders,
        CancellationToken cancellationToken = default);

    // —— Sphere / Awards & Sponsor ——

    Task<List<SnPostAward>> GetPostAwardsAsync(Guid postId, int offset = 0, int take = 20, CancellationToken cancellationToken = default);

    Task AwardPostAsync(Guid postId, PostAwardRequest request, CancellationToken cancellationToken = default);

    Task SponsorPostAsync(Guid postId, PostSponsorRequest request, CancellationToken cancellationToken = default);

    // —— Passport / Social ——

    /// <summary>GET /passport/accounts/{name}</summary>
    Task<SnAccount> GetAccountByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<List<SnAccountBadge>> GetAccountBadgesAsync(string name, CancellationToken cancellationToken = default);

    Task<List<SnAccountBoardItem>> GetAccountBoardAsync(string name, CancellationToken cancellationToken = default);

    Task<List<PublicAccountConnectionResponse>> GetAccountConnectionsAsync(string name, CancellationToken cancellationToken = default);

    Task<SnAccountStatus?> GetAccountStatusAsync(string name, CancellationToken cancellationToken = default);

    Task<List<SnAccountBadge>> GetMyBadgesAsync(CancellationToken cancellationToken = default);

    Task ActivateMyBadgeAsync(Guid badgeId, CancellationToken cancellationToken = default);

    Task<List<SnAccountBoardItem>> GetMyBoardAsync(CancellationToken cancellationToken = default);

    Task<List<SnActionLog>> GetMyActionsAsync(int offset = 0, int take = 20, CancellationToken cancellationToken = default);

    Task<List<SnExperienceRecord>> GetMyLevelingAsync(int offset = 0, int take = 20, CancellationToken cancellationToken = default);

    Task<List<SnSocialCreditRecord>> GetMyCreditsHistoryAsync(int offset = 0, int take = 20, CancellationToken cancellationToken = default);

    Task<List<ProgressionAchievementState>> GetMyAchievementsAsync(CancellationToken cancellationToken = default);

    Task<ProgressionAchievementStats?> GetMyAchievementStatsAsync(CancellationToken cancellationToken = default);

    Task<List<ProgressionQuestState>> GetMyQuestsAsync(CancellationToken cancellationToken = default);

    Task<List<SnProgressRewardGrant>> GetMyProgressGrantsAsync(CancellationToken cancellationToken = default);

    // Relationships
    Task<List<SnAccountRelationship>> GetRelationshipsAsync(int offset = 0, int take = 50, CancellationToken cancellationToken = default);

    Task<List<SnAccountRelationship>> GetRelationshipRequestsAsync(CancellationToken cancellationToken = default);

    Task<List<FriendOverviewItem>> GetFriendsOverviewAsync(CancellationToken cancellationToken = default);

    Task<List<SnAccount>> GetCloseFriendsAsync(CancellationToken cancellationToken = default);

    Task<SnAccountRelationship?> GetRelationshipAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<InspectRelationshipResponse?> InspectRelationshipAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task<SnAccountRelationship> SendFriendRequestAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task CancelFriendRequestAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task AcceptFriendRequestAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task DeclineFriendRequestAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task BlockAccountAsync(Guid accountId, RelationshipActionRequest? request = null, CancellationToken cancellationToken = default);

    Task UnblockAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task MuteAccountAsync(Guid accountId, RelationshipActionRequest? request = null, CancellationToken cancellationToken = default);

    Task UnmuteAccountAsync(Guid accountId, CancellationToken cancellationToken = default);

    Task SetCloseFriendAsync(Guid accountId, bool isCloseFriend, CancellationToken cancellationToken = default);

    Task SetRelationshipAliasAsync(Guid accountId, string? alias, CancellationToken cancellationToken = default);

    Task RemoveRelationshipAsync(Guid accountId, CancellationToken cancellationToken = default);

    // Realms
    Task<List<SnRealm>> GetMyRealmsAsync(CancellationToken cancellationToken = default);

    Task<List<SnRealm>> GetPublicRealmsAsync(CancellationToken cancellationToken = default);

    Task<SnRealm> GetRealmAsync(string slug, CancellationToken cancellationToken = default);

    Task<SnRealm> CreateRealmAsync(RealmRequest request, CancellationToken cancellationToken = default);

    Task<List<SnRealmMember>> GetRealmMembersAsync(string slug, CancellationToken cancellationToken = default);

    Task<List<SnRealmMember>> GetRealmInvitesAsync(CancellationToken cancellationToken = default);

    Task InviteToRealmAsync(string slug, RealmMemberRequest request, CancellationToken cancellationToken = default);

    Task AcceptRealmInviteAsync(string slug, CancellationToken cancellationToken = default);

    Task DeclineRealmInviteAsync(string slug, CancellationToken cancellationToken = default);

    Task LeaveRealmAsync(string slug, CancellationToken cancellationToken = default);

    Task JoinRealmAsync(string slug, CancellationToken cancellationToken = default);

    Task<List<SnRealmRolePermission>> GetRealmRolePermissionsAsync(string slug, CancellationToken cancellationToken = default);

    Task<RealmQuotaResponse?> GetRealmQuotaAsync(CancellationToken cancellationToken = default);

    // Calendar
    Task<DailyEventResponse?> GetMyCalendarDayAsync(CancellationToken cancellationToken = default);

    Task<List<SnUserCalendarEvent>> GetMyCalendarEventsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    Task<List<EventCountdownItem>> GetMyCalendarCountdownAsync(CancellationToken cancellationToken = default);

    Task<SnUserCalendarEvent> CreateCalendarEventAsync(CreateCalendarEventRequest request, CancellationToken cancellationToken = default);

    Task DeleteCalendarEventAsync(Guid eventId, CancellationToken cancellationToken = default);

    // Tickets
    Task<List<SnTicket>> GetMyTicketsAsync(CancellationToken cancellationToken = default);

    Task<SnTicket> GetTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<SnTicket> CreateTicketAsync(CreateTicketRequest request, CancellationToken cancellationToken = default);

    Task<SnTicketMessage> AddTicketMessageAsync(Guid ticketId, AddTicketMessageRequest request, CancellationToken cancellationToken = default);

    Task<int> GetTicketCountAsync(CancellationToken cancellationToken = default);

    // Nearby / Pins / Meets / NFC
    Task<List<SnLocationPin>> GetMyPinsAsync(CancellationToken cancellationToken = default);

    Task<List<SnLocationPin>> GetNearbyPinsAsync(CancellationToken cancellationToken = default);

    Task<SnLocationPin> CreatePinAsync(CreatePinRequest request, CancellationToken cancellationToken = default);

    Task DeletePinAsync(Guid pinId, CancellationToken cancellationToken = default);

    Task<List<SnMeet>> GetMyMeetsAsync(CancellationToken cancellationToken = default);

    Task<List<SnMeet>> GetNearbyMeetsAsync(CancellationToken cancellationToken = default);

    Task<SnMeet> CreateMeetAsync(CreateMeetRequest request, CancellationToken cancellationToken = default);

    Task JoinMeetAsync(Guid meetId, CancellationToken cancellationToken = default);

    Task CompleteMeetAsync(Guid meetId, CancellationToken cancellationToken = default);

    Task DeleteMeetAsync(Guid meetId, CancellationToken cancellationToken = default);

    Task<List<NfcTagResponse>> GetMyNfcTagsAsync(CancellationToken cancellationToken = default);

    Task<NfcResolveResponse?> LookupNfcAsync(string query, CancellationToken cancellationToken = default);

    // —— Passport extras: fortune / IP / notable-days / rewind / spells ——

    Task<List<FortuneSaying>> GetFortuneSayingsAsync(CancellationToken cancellationToken = default);

    Task<List<FortuneSaying>> GetRandomFortuneAsync(string? language = "zh", CancellationToken cancellationToken = default);

    Task<IpCheckResponse?> GetIpCheckAsync(CancellationToken cancellationToken = default);

    Task<GeoIpResponse?> GetIpGeoAsync(CancellationToken cancellationToken = default);

    Task<List<SnNotableDay>> GetNotableDaysAsync(
        int? year = null,
        string region = "CN",
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<SnNotableDay> CreateNotableDayAsync(NotableDayRequest request, CancellationToken cancellationToken = default);

    Task DeleteNotableDayAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SnRewindPoint?> GetMyRewindAsync(CancellationToken cancellationToken = default);

    Task<SnRewindPoint?> GetRewindByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<SnRewindPoint> PublishRewindPublicAsync(int year, CancellationToken cancellationToken = default);

    Task<SnRewindPoint> PublishRewindPrivateAsync(int year, CancellationToken cancellationToken = default);

    Task<SnMagicSpell?> LookupSpellAsync(string spellWord, CancellationToken cancellationToken = default);

    Task ApplySpellAsync(string spellWord, MagicSpellApplyRequest? request = null, CancellationToken cancellationToken = default);

    Task ResendSpellActivationAsync(CancellationToken cancellationToken = default);

    Task ResendSpellAsync(Guid spellId, CancellationToken cancellationToken = default);

    // —— Personality / 寻思 (bots + models chat) ——

    Task<List<ThoughtAgent>> GetThoughtAgentsAsync(CancellationToken cancellationToken = default);

    Task<List<ThoughtConversation>> GetThoughtConversationsAsync(
        int offset = 0,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<ThoughtConversation> CreateThoughtConversationAsync(
        CreateThoughtConversationRequest request,
        CancellationToken cancellationToken = default);

    Task<List<ThoughtMessage>> GetThoughtMessagesAsync(
        string conversationId,
        int offset = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<ThoughtMessage> AddThoughtMessageAsync(
        string conversationId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /personality/conversations/{id}/runs.
    /// Prefer SSE stream (Solian official client); falls back to JSON / message reload.
    /// </summary>
    Task<ThoughtRunResult> RunThoughtAsync(
        string conversationId,
        ThoughtRunRequest request,
        CancellationToken cancellationToken = default);

    // —— Padlock / Security ——

    Task<List<SnAuthClientWithSessions>> GetDevicesAsync(int offset = 0, int take = 50, CancellationToken cancellationToken = default);

    Task DeleteDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);

    Task UpdateDeviceLabelAsync(Guid deviceId, string label, CancellationToken cancellationToken = default);

    Task UpdateCurrentDeviceLabelAsync(string label, CancellationToken cancellationToken = default);

    Task<List<SnAuthSession>> GetSessionsAsync(CancellationToken cancellationToken = default);

    Task<SnAuthSession?> GetCurrentSessionAsync(CancellationToken cancellationToken = default);

    Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<List<SnAuthSession>> GetSessionChildrenAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<List<SnAccountContact>> GetContactsAsync(CancellationToken cancellationToken = default);

    Task<SnAccountContact> CreateContactAsync(ContactRequest request, CancellationToken cancellationToken = default);

    Task DeleteContactAsync(Guid contactId, CancellationToken cancellationToken = default);

    Task SetContactPrimaryAsync(Guid contactId, CancellationToken cancellationToken = default);

    Task SetContactPublicAsync(Guid contactId, bool isPublic, CancellationToken cancellationToken = default);

    Task<SnAccountContact> VerifyContactAsync(Guid contactId, string code, CancellationToken cancellationToken = default);

    /// <summary>POST /padlock/contacts/{id}/verify without code — re-request delivery when supported.</summary>
    Task RequestContactVerificationAsync(Guid contactId, CancellationToken cancellationToken = default);

    Task<List<AuthorizedAppResponse>> GetAuthorizedAppsAsync(CancellationToken cancellationToken = default);

    Task RevokeAuthorizedAppAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<SnApiKey>> GetApiKeysAsync(CancellationToken cancellationToken = default);

    Task<SnApiKey> CreateApiKeyAsync(CreateApiKeyRequest request, CancellationToken cancellationToken = default);

    Task DeleteApiKeyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SnApiKey> RotateApiKeyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<SnAccountConnection>> GetConnectionsAsync(CancellationToken cancellationToken = default);

    Task DeleteConnectionAsync(Guid id, CancellationToken cancellationToken = default);

    Task SetConnectionVisibilityAsync(Guid id, bool isPublic, CancellationToken cancellationToken = default);

    Task<List<SnAccountAuthFactor>> GetAccountFactorsAsync(CancellationToken cancellationToken = default);

    Task EnableFactorAsync(Guid factorId, CancellationToken cancellationToken = default);

    Task DisableFactorAsync(Guid factorId, CancellationToken cancellationToken = default);

    Task DeleteFactorAsync(Guid factorId, CancellationToken cancellationToken = default);

    Task<string> StartPasskeyRegistrationAsync(CancellationToken cancellationToken = default);

    Task CompletePasskeyRegistrationAsync(string credentialJson, CancellationToken cancellationToken = default);

    Task<string> StartPasskeyAuthenticationAsync(Guid challengeId, CancellationToken cancellationToken = default);

    Task<SnAuthChallenge> CompletePasskeyAuthenticationAsync(Guid challengeId, string credentialJson, CancellationToken cancellationToken = default);

    Task<QrGenerateResponse> GenerateQrLoginAsync(CancellationToken cancellationToken = default);

    Task<QrStatusResponse> GetQrLoginStatusAsync(Guid qrChallengeId, CancellationToken cancellationToken = default);

    Task ScanQrLoginAsync(Guid qrChallengeId, CancellationToken cancellationToken = default);

    Task ApproveQrLoginAsync(Guid qrChallengeId, CancellationToken cancellationToken = default);

    Task DeclineQrLoginAsync(Guid qrChallengeId, CancellationToken cancellationToken = default);

    Task<List<SnAuthChallenge>> GetPendingChallengesAsync(CancellationToken cancellationToken = default);

    Task ApproveChallengeAsync(Guid challengeId, CancellationToken cancellationToken = default);

    Task DeclineChallengeAsync(Guid challengeId, CancellationToken cancellationToken = default);

    Task<SnAuthChallenge> GetAuthChallengeAsync(Guid challengeId, CancellationToken cancellationToken = default);

    Task<List<SnAccountAuthFactor>> GetChallengeFactorsAsync(Guid challengeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /padlock/auth/challenge/{id}/factors/{factorId} — request delivery of email/SMS codes, etc.
    /// </summary>
    Task RequestChallengeFactorAsync(Guid challengeId, Guid factorId, CancellationToken cancellationToken = default);

    Task<SnAuthChallenge> SubmitChallengeFactorAsync(Guid challengeId, Guid factorId, string secret, CancellationToken cancellationToken = default);

    // —— Account register / recover / captcha / passkey login / social ——

    Task<CaptchaConfigResponse> GetCaptchaConfigAsync(CancellationToken cancellationToken = default);

    Task VerifyCaptchaTokenAsync(string token, CancellationToken cancellationToken = default);

    Task<SnAccount> RegisterAccountAsync(AccountCreateRequest request, CancellationToken cancellationToken = default);

    Task<TokenExchangeResponse> RecoverAccountAsync(RecoveryRequest request, CancellationToken cancellationToken = default);

    Task<PasskeyLoginStartResponse> StartPasskeyLoginAsync(CancellationToken cancellationToken = default);

    Task<SnAuthChallenge> CompletePasskeyLoginAsync(
        Guid authChallengeId,
        PasskeyAuthenticationCompleteRequest request,
        CancellationToken cancellationToken = default);

    Task<string> StartPasskeyRegistrationWithDeviceAsync(CancellationToken cancellationToken = default);

    Task CompletePasskeyRegistrationDetailedAsync(
        PasskeyRegistrationCompleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Absolute URL for browser OIDC: GET /padlock/auth/login/{provider}.</summary>
    string BuildSocialLoginUrl(string provider, string returnUrl, string deviceId);

    // —— Sphere surveys ——
    Task<List<SnSurvey>> GetMySurveysAsync(CancellationToken cancellationToken = default);
    Task<List<SnSurvey>> GetSurveysAsync(int offset = 0, int take = 30, CancellationToken cancellationToken = default);
    Task<SnSurvey> GetSurveyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SnSurvey> CreateSurveyAsync(SurveyRequest request, CancellationToken cancellationToken = default);
    Task<SnSurvey> UpdateSurveyAsync(Guid id, SurveyRequest request, CancellationToken cancellationToken = default);
    Task DeleteSurveyAsync(Guid id, CancellationToken cancellationToken = default);
    Task PublishSurveyAsync(Guid id, CancellationToken cancellationToken = default);
    Task ArchiveSurveyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SnSurvey> CloneSurveyAsync(Guid id, CancellationToken cancellationToken = default);
    Task AnswerSurveyAsync(Guid id, SurveyAnswerRequest request, CancellationToken cancellationToken = default);
    Task SubscribeSurveyAsync(Guid id, CancellationToken cancellationToken = default);
    Task UnsubscribeSurveyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<SnSurveyAnswer>> GetSurveyFeedbackAsync(Guid id, int offset = 0, int take = 20, CancellationToken cancellationToken = default);

    // —— Scrap / translate ——
    Task<ScrapLinkResult> ScrapLinkAsync(string url, CancellationToken cancellationToken = default);
    Task ClearScrapLinkCacheAsync(CancellationToken cancellationToken = default);
    Task ClearAllScrapCacheAsync(CancellationToken cancellationToken = default);
    Task<TranslateResult> TranslateTextAsync(string text, string targetLang = "zh", string? sourceLang = null, CancellationToken cancellationToken = default);

    // —— Quote authorizations ——
    Task<QuoteAuthorizationItem> CreateQuoteAuthorizationAsync(CreateQuoteAuthorizationRequest request, CancellationToken cancellationToken = default);
    Task DeleteQuoteAuthorizationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<QuoteAuthorizationItem?> GetQuoteAuthorizationAsync(Guid id, CancellationToken cancellationToken = default);

    // —— Fediverse client ——
    Task<List<SnFediverseActor>> SearchFediverseActorsAsync(string query, int take = 20, CancellationToken cancellationToken = default);
    Task<SnFediverseActor> GetFediverseActorAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SnFediverseActor?> LookupFediverseActorAsync(string usernameAtInstance, CancellationToken cancellationToken = default);
    Task FollowFediverseActorAsync(Guid id, CancellationToken cancellationToken = default);
    Task UnfollowFediverseActorAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FediverseRelationship?> GetFediverseRelationshipAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<SnPost>> GetFediverseActorPostsAsync(Guid id, int offset = 0, int take = 20, CancellationToken cancellationToken = default);
    Task<List<FediverseModerationRule>> GetFediverseModerationRulesAsync(CancellationToken cancellationToken = default);
    Task ToggleFediverseModerationRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<string> CheckFediverseDomainAsync(string domain, CancellationToken cancellationToken = default);
    Task<string> CheckFediverseActorModerationAsync(string actorUri, CancellationToken cancellationToken = default);

    // —— ActivityPub discovery (JSON-LD opaque) ——
    Task<string> GetActivityPubActorAsync(CancellationToken cancellationToken = default);
    Task<string> GetActivityPubSearchAsync(string query, CancellationToken cancellationToken = default);
    Task<string> CheckActivityPubUsernameAsync(string username, CancellationToken cancellationToken = default);

    // —— Automod / Ads / Admin ——
    Task<List<SnAutomodRule>> GetAutomodRulesAsync(CancellationToken cancellationToken = default);
    Task<SnAutomodRule> CreateAutomodRuleAsync(AutomodRuleDto request, CancellationToken cancellationToken = default);
    Task DeleteAutomodRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<string> TestAutomodRuleAsync(Guid id, string sample, CancellationToken cancellationToken = default);
    Task<List<PublicAdvertisingPostStats>> GetPublisherAdsAsync(string name, int offset = 0, int take = 20, CancellationToken cancellationToken = default);
    Task<SphereAdminStats?> GetSphereAdminStatsAsync(CancellationToken cancellationToken = default);
    Task AdminLockPostAsync(Guid postId, CancellationToken cancellationToken = default);
    Task AdminShadowbanPostAsync(Guid postId, CancellationToken cancellationToken = default);
    Task AdminSetPostVisibilityAsync(Guid postId, int visibility, CancellationToken cancellationToken = default);
    Task AdminShadowbanPublisherAsync(string name, CancellationToken cancellationToken = default);
    Task AdminVerifyPublisherAsync(string name, CancellationToken cancellationToken = default);
    Task<List<SnPost>> AdminListPostsAsync(int offset = 0, int take = 20, CancellationToken cancellationToken = default);
}
