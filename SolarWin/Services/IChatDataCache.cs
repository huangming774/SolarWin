using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// Process-wide singleton cache for Messager / chat API payloads.
/// Holds rooms, summary, invites, groups, and per-room messages/members.
/// </summary>
public interface IChatDataCache
{
    // —— Account scope ——
    Guid? BoundAccountId { get; }

    /// <summary>Bind cache to the signed-in account; clears if account changes.</summary>
    void BindAccount(Guid? accountId);

    void ClearAll();

    // —— Room list (Phase 6: SQLite authority; L1 is projection cache) ——
    bool TryGetRooms(out IReadOnlyList<SnChatRoom> rooms);

    /// <summary>
    /// Update L1 room list. When <paramref name="persistSqlite"/>, enqueue full/partial room upsert
    /// (no Offline JSON rewrite).
    /// </summary>
    void SetRooms(
        IReadOnlyList<SnChatRoom> rooms,
        bool persistSqlite = true,
        bool removeMissing = false);

    /// <summary>
    /// Persist rooms + summary rows to SQLite via write pump (full pull merge).
    /// </summary>
    void PersistRoomsFull(
        IReadOnlyList<SnChatRoom> rooms,
        IReadOnlyDictionary<string, ChatSummaryResponse>? summary,
        bool removeMissing);

    /// <summary>Local read mark → single-row SQLite UPDATE.</summary>
    void MarkRoomReadPersisted(Guid roomId, long lastReadSequence = 0);

    /// <summary>Preview / unread point update (socket or outbound message).</summary>
    void UpdateRoomPreviewPersisted(Guid roomId, SnChatMessage? lastMessage, int? unreadCount);

    bool IsRoomsFresh(TimeSpan? ttl = null);

    DateTimeOffset? RoomsLoadedAt { get; }

    // —— Summary / unread ——
    bool TryGetSummary(out IReadOnlyDictionary<string, ChatSummaryResponse> summary);

    void SetSummary(IReadOnlyDictionary<string, ChatSummaryResponse> summary);

    int? TotalUnread { get; set; }

    // —— Invites ——
    bool TryGetInvites(out IReadOnlyList<SnChatRoom> invites);

    void SetInvites(IReadOnlyList<SnChatRoom> invites);

    // —— Groups ——
    bool TryGetGroups(out IReadOnlyList<SnChatGroup> groups);

    void SetGroups(IReadOnlyList<SnChatGroup> groups);

    // —— Per-room messages ——
    bool TryGetRoomMessages(Guid roomId, out ChatRoomMessageCacheEntry entry);

    /// <summary>
    /// Replace the in-memory message window for a room (L1 only — no SQLite dual-write).
    /// Persistence is incremental via <see cref="UpsertRoomMessage"/>.
    /// </summary>
    void SetRoomMessages(
        Guid roomId,
        IReadOnlyList<SnChatMessage> messages,
        long lastSyncTimestamp,
        Guid? lastSyncMessageId,
        bool hasMore,
        int offset);

    /// <summary>Upsert a single message (WS / send / API). Dual-writes to SQLite. Returns true if newly added.</summary>
    bool UpsertRoomMessage(Guid roomId, SnChatMessage message);

    /// <summary>Clear all L1 per-room message windows (Phase 7 clear local chat).</summary>
    void ClearMessageWindows();

    void UpdateRoomSyncCursor(Guid roomId, long lastSyncTimestamp, Guid? lastSyncMessageId);

    void InvalidateRoomMessages(Guid roomId);

    /// <summary>Remove one message from the in-memory window and dual-write delete to SQLite.</summary>
    void RemoveRoomMessage(Guid roomId, Guid messageId, string? clientMessageId = null);

    // —— Per-room members ——
    bool TryGetRoomMembers(Guid roomId, out IReadOnlyList<SnChatMember> members, out DateTimeOffset loadedAt);

    void SetRoomMembers(Guid roomId, IReadOnlyList<SnChatMember> members);

    bool IsRoomMembersFresh(Guid roomId, TimeSpan? ttl = null);

    // —— Disk hydrate (offline) ——
    /// <summary>
    /// Load rooms from SQLite (and one-shot import legacy JSON) into L1.
    /// Async only — never call via GetResult/Wait on the UI thread.
    /// </summary>
    Task<bool> HydrateRoomsFromDiskAsync(CancellationToken cancellationToken = default);
}

/// <summary>Cached message window for one chat room.</summary>
public sealed class ChatRoomMessageCacheEntry
{
    public Guid RoomId { get; init; }

    public List<SnChatMessage> Messages { get; } = [];

    public HashSet<Guid> KnownIds { get; } = [];

    public long LastSyncTimestamp { get; set; }

    public Guid? LastSyncMessageId { get; set; }

    public bool HasMore { get; set; } = true;

    public int Offset { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
