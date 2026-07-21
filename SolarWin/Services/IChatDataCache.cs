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

    // —— Room list ——
    bool TryGetRooms(out IReadOnlyList<SnChatRoom> rooms);

    void SetRooms(IReadOnlyList<SnChatRoom> rooms, bool persistDisk = true);

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

    void SetRoomMessages(
        Guid roomId,
        IReadOnlyList<SnChatMessage> messages,
        long lastSyncTimestamp,
        Guid? lastSyncMessageId,
        bool hasMore,
        int offset);

    /// <summary>Upsert a single message (WS / send). Returns true if newly added.</summary>
    bool UpsertRoomMessage(Guid roomId, SnChatMessage message);

    void UpdateRoomSyncCursor(Guid roomId, long lastSyncTimestamp, Guid? lastSyncMessageId);

    void InvalidateRoomMessages(Guid roomId);

    // —— Per-room members ——
    bool TryGetRoomMembers(Guid roomId, out IReadOnlyList<SnChatMember> members, out DateTimeOffset loadedAt);

    void SetRoomMembers(Guid roomId, IReadOnlyList<SnChatMember> members);

    bool IsRoomMembersFresh(Guid roomId, TimeSpan? ttl = null);

    // —— Disk hydrate (offline) ——
    /// <summary>Try load rooms from disk offline cache into memory.</summary>
    bool TryHydrateRoomsFromDisk();
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
