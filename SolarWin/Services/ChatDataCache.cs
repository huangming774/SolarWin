using System.Collections.Concurrent;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// Singleton in-memory + disk-backed cache for chat API responses.
/// ViewModels read/write through this instead of holding raw API lists.
/// </summary>
public sealed class ChatDataCache : IChatDataCache
{
    public static readonly TimeSpan DefaultListTtl = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan DefaultMembersTtl = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan DiskRoomsTtl = TimeSpan.FromDays(7);

    private readonly object _gate = new();
    private Guid? _accountId;

    private List<SnChatRoom> _rooms = [];
    private DateTimeOffset? _roomsLoadedAt;
    private Dictionary<string, ChatSummaryResponse> _summary = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _summaryLoadedAt;
    private int? _totalUnread;
    private List<SnChatRoom> _invites = [];
    private DateTimeOffset? _invitesLoadedAt;
    private List<SnChatGroup> _groups = [];
    private DateTimeOffset? _groupsLoadedAt;

    private readonly ConcurrentDictionary<Guid, ChatRoomMessageCacheEntry> _messages = new();
    private readonly ConcurrentDictionary<Guid, (List<SnChatMember> Members, DateTimeOffset LoadedAt)> _members = new();

    public Guid? BoundAccountId
    {
        get
        {
            lock (_gate)
            {
                return _accountId;
            }
        }
    }

    public DateTimeOffset? RoomsLoadedAt
    {
        get
        {
            lock (_gate)
            {
                return _roomsLoadedAt;
            }
        }
    }

    public int? TotalUnread
    {
        get
        {
            lock (_gate)
            {
                return _totalUnread;
            }
        }
        set
        {
            lock (_gate)
            {
                _totalUnread = value;
            }
        }
    }

    public void BindAccount(Guid? accountId)
    {
        lock (_gate)
        {
            if (_accountId == accountId)
            {
                return;
            }

            ClearAll_NoLock();
            _accountId = accountId is { } id && id != Guid.Empty ? id : null;
        }
    }

    public void ClearAll()
    {
        lock (_gate)
        {
            ClearAll_NoLock();
        }
    }

    private void ClearAll_NoLock()
    {
        _rooms = [];
        _roomsLoadedAt = null;
        _summary = new Dictionary<string, ChatSummaryResponse>(StringComparer.OrdinalIgnoreCase);
        _summaryLoadedAt = null;
        _totalUnread = null;
        _invites = [];
        _invitesLoadedAt = null;
        _groups = [];
        _groupsLoadedAt = null;
        _messages.Clear();
        _members.Clear();
    }

    // —— Rooms ——

    public bool TryGetRooms(out IReadOnlyList<SnChatRoom> rooms)
    {
        lock (_gate)
        {
            if (_rooms.Count == 0)
            {
                rooms = Array.Empty<SnChatRoom>();
                return false;
            }

            rooms = _rooms.ToList();
            return true;
        }
    }

    public void SetRooms(IReadOnlyList<SnChatRoom> rooms, bool persistDisk = true)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        lock (_gate)
        {
            _rooms = rooms.ToList();
            _roomsLoadedAt = DateTimeOffset.UtcNow;
            if (persistDisk)
            {
                PersistRooms_NoLock();
            }
        }
    }

    public bool IsRoomsFresh(TimeSpan? ttl = null)
    {
        var window = ttl ?? DefaultListTtl;
        lock (_gate)
        {
            return _rooms.Count > 0
                   && _roomsLoadedAt is { } at
                   && DateTimeOffset.UtcNow - at < window;
        }
    }

    public bool TryHydrateRoomsFromDisk()
    {
        var key = DiskRoomsKey();
        if (key is null)
        {
            return false;
        }

        if (!OfflineCache.TryGetJson<List<SnChatRoom>>(key, out var disk, allowExpired: true)
            || disk is not { Count: > 0 })
        {
            return false;
        }

        lock (_gate)
        {
            if (_rooms.Count > 0)
            {
                return true;
            }

            _rooms = disk;
            // Treat disk hydrate as stale so UI will soft-refresh.
            _roomsLoadedAt = DateTimeOffset.UtcNow - DefaultListTtl - TimeSpan.FromSeconds(1);
        }

        return true;
    }

    private void PersistRooms_NoLock()
    {
        var key = DiskRoomsKey_NoLock();
        if (key is null || _rooms.Count == 0)
        {
            return;
        }

        OfflineCache.SetJson(key, _rooms, DiskRoomsTtl);
    }

    private string? DiskRoomsKey()
    {
        lock (_gate)
        {
            return DiskRoomsKey_NoLock();
        }
    }

    private string? DiskRoomsKey_NoLock()
    {
        if (_accountId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        return $"chat_rooms_{id:N}";
    }

    // —— Summary ——

    public bool TryGetSummary(out IReadOnlyDictionary<string, ChatSummaryResponse> summary)
    {
        lock (_gate)
        {
            if (_summary.Count == 0)
            {
                summary = new Dictionary<string, ChatSummaryResponse>();
                return false;
            }

            summary = new Dictionary<string, ChatSummaryResponse>(_summary, StringComparer.OrdinalIgnoreCase);
            return true;
        }
    }

    public void SetSummary(IReadOnlyDictionary<string, ChatSummaryResponse> summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        lock (_gate)
        {
            _summary = new Dictionary<string, ChatSummaryResponse>(summary, StringComparer.OrdinalIgnoreCase);
            _summaryLoadedAt = DateTimeOffset.UtcNow;
        }
    }

    // —— Invites ——

    public bool TryGetInvites(out IReadOnlyList<SnChatRoom> invites)
    {
        lock (_gate)
        {
            if (_invites.Count == 0)
            {
                invites = Array.Empty<SnChatRoom>();
                return false;
            }

            invites = _invites.ToList();
            return true;
        }
    }

    public void SetInvites(IReadOnlyList<SnChatRoom> invites)
    {
        ArgumentNullException.ThrowIfNull(invites);
        lock (_gate)
        {
            _invites = invites.ToList();
            _invitesLoadedAt = DateTimeOffset.UtcNow;
        }
    }

    // —— Groups ——

    public bool TryGetGroups(out IReadOnlyList<SnChatGroup> groups)
    {
        lock (_gate)
        {
            if (_groups.Count == 0)
            {
                groups = Array.Empty<SnChatGroup>();
                return false;
            }

            groups = _groups.ToList();
            return true;
        }
    }

    public void SetGroups(IReadOnlyList<SnChatGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        lock (_gate)
        {
            _groups = groups.ToList();
            _groupsLoadedAt = DateTimeOffset.UtcNow;
        }
    }

    // —— Messages ——

    public bool TryGetRoomMessages(Guid roomId, out ChatRoomMessageCacheEntry entry)
    {
        if (roomId == Guid.Empty)
        {
            entry = new ChatRoomMessageCacheEntry { RoomId = roomId };
            return false;
        }

        if (_messages.TryGetValue(roomId, out var found) && found.Messages.Count > 0)
        {
            entry = found;
            return true;
        }

        entry = new ChatRoomMessageCacheEntry { RoomId = roomId };
        return false;
    }

    public void SetRoomMessages(
        Guid roomId,
        IReadOnlyList<SnChatMessage> messages,
        long lastSyncTimestamp,
        Guid? lastSyncMessageId,
        bool hasMore,
        int offset)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(messages);

        var entry = _messages.GetOrAdd(roomId, id => new ChatRoomMessageCacheEntry { RoomId = id });
        lock (entry)
        {
            entry.Messages.Clear();
            entry.KnownIds.Clear();
            foreach (var m in messages)
            {
                entry.Messages.Add(m);
                if (m.Id != Guid.Empty)
                {
                    entry.KnownIds.Add(m.Id);
                }
            }

            entry.LastSyncTimestamp = lastSyncTimestamp;
            entry.LastSyncMessageId = lastSyncMessageId;
            entry.HasMore = hasMore;
            entry.Offset = offset;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public bool UpsertRoomMessage(Guid roomId, SnChatMessage message)
    {
        if (roomId == Guid.Empty || message is null)
        {
            return false;
        }

        var entry = _messages.GetOrAdd(roomId, id => new ChatRoomMessageCacheEntry { RoomId = id });
        lock (entry)
        {
            // Reconcile optimistic echo by client_message_id
            if (!string.IsNullOrEmpty(message.ClientMessageId))
            {
                for (var i = 0; i < entry.Messages.Count; i++)
                {
                    var existing = entry.Messages[i];
                    if (existing.Id == Guid.Empty
                        && string.Equals(existing.ClientMessageId, message.ClientMessageId, StringComparison.Ordinal))
                    {
                        entry.Messages[i] = message;
                        if (message.Id != Guid.Empty)
                        {
                            entry.KnownIds.Add(message.Id);
                        }

                        entry.UpdatedAt = DateTimeOffset.UtcNow;
                        return true;
                    }
                }
            }

            if (message.Id != Guid.Empty)
            {
                if (!entry.KnownIds.Add(message.Id))
                {
                    // Update existing payload in place
                    for (var i = 0; i < entry.Messages.Count; i++)
                    {
                        if (entry.Messages[i].Id == message.Id)
                        {
                            entry.Messages[i] = message;
                            entry.UpdatedAt = DateTimeOffset.UtcNow;
                            return false;
                        }
                    }

                    return false;
                }
            }

            // Append in room sequence order when possible
            if (entry.Messages.Count > 0 && message.RoomSequence > 0)
            {
                var last = entry.Messages[^1];
                if (message.RoomSequence < last.RoomSequence)
                {
                    var idx = entry.Messages.Count - 1;
                    while (idx >= 0 && entry.Messages[idx].RoomSequence > message.RoomSequence)
                    {
                        idx--;
                    }

                    entry.Messages.Insert(idx + 1, message);
                    entry.UpdatedAt = DateTimeOffset.UtcNow;
                    return true;
                }
            }

            entry.Messages.Add(message);
            entry.Offset = Math.Max(entry.Offset, entry.Messages.Count);
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            return true;
        }
    }

    public void UpdateRoomSyncCursor(Guid roomId, long lastSyncTimestamp, Guid? lastSyncMessageId)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        var entry = _messages.GetOrAdd(roomId, id => new ChatRoomMessageCacheEntry { RoomId = id });
        lock (entry)
        {
            entry.LastSyncTimestamp = lastSyncTimestamp;
            entry.LastSyncMessageId = lastSyncMessageId;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public void InvalidateRoomMessages(Guid roomId)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        _messages.TryRemove(roomId, out _);
    }

    // —— Members ——

    public bool TryGetRoomMembers(Guid roomId, out IReadOnlyList<SnChatMember> members, out DateTimeOffset loadedAt)
    {
        members = Array.Empty<SnChatMember>();
        loadedAt = default;
        if (roomId == Guid.Empty)
        {
            return false;
        }

        if (!_members.TryGetValue(roomId, out var tuple) || tuple.Members.Count == 0)
        {
            return false;
        }

        members = tuple.Members.ToList();
        loadedAt = tuple.LoadedAt;
        return true;
    }

    public void SetRoomMembers(Guid roomId, IReadOnlyList<SnChatMember> members)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(members);
        _members[roomId] = (members.ToList(), DateTimeOffset.UtcNow);
    }

    public bool IsRoomMembersFresh(Guid roomId, TimeSpan? ttl = null)
    {
        var window = ttl ?? DefaultMembersTtl;
        if (!_members.TryGetValue(roomId, out var tuple) || tuple.Members.Count == 0)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - tuple.LoadedAt < window;
    }
}
