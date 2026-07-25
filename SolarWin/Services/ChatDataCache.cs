using System.Collections.Concurrent;
using System.Diagnostics;
using SolarWin.Data;
using SolarWin.Data.Entities;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// Singleton L1 cache for Messager payloads.
/// Messages dual-write via write pump; rooms list authority is SQLite (Phase 6).
/// </summary>
public sealed class ChatDataCache : IChatDataCache
{
    public static readonly TimeSpan DefaultListTtl = TimeSpan.FromSeconds(45);
    public static readonly TimeSpan DefaultMembersTtl = TimeSpan.FromSeconds(60);

    /// <summary>Per-room retained message window — enough for instant paint + one scroll-back.</summary>
    private const int MaxMessagesPerRoom = 200;

    /// <summary>
    /// Phase 5: only the current room's messages stay in L1 (was 25-room LRU).
    /// </summary>
    private const int MaxMessageRooms = 1;

    private const int MaxMemberRooms = 3;

    private readonly object _gate = new();
    private readonly object _memberGate = new();
    private readonly IChatWritePump? _writePump;
    private readonly IRoomLocalStore? _roomStore;
    private Guid? _accountId;

    public ChatDataCache(IChatWritePump? writePump = null, IRoomLocalStore? roomStore = null)
    {
        _writePump = writePump;
        _roomStore = roomStore;
    }

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
    private readonly Dictionary<Guid, MemberCacheEntry> _members = [];
    private long _memberAccessSequence;

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
        lock (_memberGate)
        {
            _members.Clear();
            _memberAccessSequence = 0;
        }
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

    public void SetRooms(
        IReadOnlyList<SnChatRoom> rooms,
        bool persistSqlite = true,
        bool removeMissing = false)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        Dictionary<string, ChatSummaryResponse> summarySnapshot;
        lock (_gate)
        {
            _rooms = rooms.ToList();
            _roomsLoadedAt = DateTimeOffset.UtcNow;
            summarySnapshot = new Dictionary<string, ChatSummaryResponse>(_summary, StringComparer.OrdinalIgnoreCase);
        }

        if (persistSqlite)
        {
            PersistRoomsFull(rooms, summarySnapshot, removeMissing);
        }
    }

    public void PersistRoomsFull(
        IReadOnlyList<SnChatRoom> rooms,
        IReadOnlyDictionary<string, ChatSummaryResponse>? summary,
        bool removeMissing)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        if (_writePump is null || rooms.Count == 0 && !removeMissing)
        {
            return;
        }

        var pairs = new List<(SnChatRoom Room, ChatSummaryResponse? Summary)>(rooms.Count);
        foreach (var room in rooms)
        {
            ChatSummaryResponse? s = null;
            if (summary is not null)
            {
                if (!summary.TryGetValue(room.Id.ToString(), out s))
                {
                    summary.TryGetValue(room.Id.ToString("D"), out s);
                }
            }

            pairs.Add((room, s));
        }

        EnqueueWrite(new UpsertRoomsBatchOp(pairs, ChatMessageSource.Api, removeMissing));
    }

    public void MarkRoomReadPersisted(Guid roomId, long lastReadSequence = 0)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        lock (_gate)
        {
            if (_summary.TryGetValue(roomId.ToString(), out var s)
                || _summary.TryGetValue(roomId.ToString("D"), out s))
            {
                s.UnreadCount = 0;
            }
        }

        EnqueueWrite(new UpdateRoomReadOp(roomId, lastReadSequence, UnreadCount: 0));
    }

    public void UpdateRoomPreviewPersisted(Guid roomId, SnChatMessage? lastMessage, int? unreadCount)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        EnqueueWrite(new UpdateRoomPreviewOp(roomId, lastMessage, unreadCount, lastMessage?.CreatedAt));
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

    public async Task<bool> HydrateRoomsFromDiskAsync(CancellationToken cancellationToken = default)
    {
        Guid accountId;
        lock (_gate)
        {
            if (_rooms.Count > 0)
            {
                return true;
            }

            if (_accountId is not { } id || id == Guid.Empty)
            {
                return false;
            }

            accountId = id;
        }

        if (_roomStore is null)
        {
            return false;
        }

        try
        {
            // One-shot JSON import when rooms table empty (Phase 6 migration).
            await _roomStore.TryImportLegacyJsonAsync(accountId, cancellationToken).ConfigureAwait(false);
            var rows = await _roomStore.GetRoomsOrderedAsync(cancellationToken).ConfigureAwait(false);
            if (rows.Count == 0)
            {
                return false;
            }

            var rooms = new List<SnChatRoom>(rows.Count);
            var summary = new Dictionary<string, ChatSummaryResponse>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                rooms.Add(ChatRoomMapper.ToRoomDto(row));
                summary[row.RoomId.ToString("D")] = ChatRoomMapper.ToSummary(row);
            }

            lock (_gate)
            {
                if (_rooms.Count > 0)
                {
                    return true;
                }

                _rooms = rooms;
                _summary = summary;
                // Stale so UI soft-refreshes from network.
                _roomsLoadedAt = DateTimeOffset.UtcNow - DefaultListTtl - TimeSpan.FromSeconds(1);
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatDataCache] SQLite room hydrate: {ex.Message}");
            return false;
        }
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

        FocusMessageRoom(roomId);
        var entry = _messages.GetOrAdd(roomId, id => new ChatRoomMessageCacheEntry { RoomId = id });
        lock (entry)
        {
            entry.Messages.Clear();
            entry.KnownIds.Clear();
            // Keep only the newest window — instant paint uses the tail of the list.
            var start = Math.Max(0, messages.Count - MaxMessagesPerRoom);
            for (var i = start; i < messages.Count; i++)
            {
                var m = messages[i];
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

        EvictMessageRoomsIfNeeded();

        // Phase 7: SetRoomMessages is L1-only. SQLite dual-write is per-message via UpsertRoomMessage
        // (avoids full-window UpsertMessagesBatchOp on every PersistMessagesToCache).
    }

    /// <summary>Drop all in-memory room message windows (keeps rooms list / summary).</summary>
    public void ClearMessageWindows()
    {
        _messages.Clear();
    }

    public bool UpsertRoomMessage(Guid roomId, SnChatMessage message)
    {
        if (roomId == Guid.Empty || message is null)
        {
            return false;
        }

        FocusMessageRoom(roomId);
        var entry = _messages.GetOrAdd(roomId, id => new ChatRoomMessageCacheEntry { RoomId = id });
        bool added;
        lock (entry)
        {
            added = UpsertRoomMessageCore(entry, message);
        }

        if (added)
        {
            EvictMessageRoomsIfNeeded();
        }

        // Always dual-write upsert (including in-place updates).
        EnqueueWrite(new UpsertMessageOp(roomId, message, ChatMessageSource.Ws));

        return added;
    }

    /// <summary>Phase 5: drop every room message window except <paramref name="roomId"/>.</summary>
    private void FocusMessageRoom(Guid roomId)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        foreach (var key in _messages.Keys)
        {
            if (key != roomId)
            {
                _messages.TryRemove(key, out _);
            }
        }
    }

    private static bool UpsertRoomMessageCore(ChatRoomMessageCacheEntry entry, SnChatMessage message)
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
                TrimEntryMessages_NoLock(entry);
                entry.UpdatedAt = DateTimeOffset.UtcNow;
                return true;
            }
        }

        entry.Messages.Add(message);
        entry.Offset = Math.Max(entry.Offset, entry.Messages.Count);
        TrimEntryMessages_NoLock(entry);
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    /// <summary>Drop oldest messages beyond the per-room window (caller holds the entry lock).</summary>
    private static void TrimEntryMessages_NoLock(ChatRoomMessageCacheEntry entry)
    {
        var excess = entry.Messages.Count - MaxMessagesPerRoom;
        if (excess <= 0)
        {
            return;
        }

        for (var i = 0; i < excess; i++)
        {
            var old = entry.Messages[i];
            if (old.Id != Guid.Empty)
            {
                entry.KnownIds.Remove(old.Id);
            }
        }

        entry.Messages.RemoveRange(0, excess);
    }

    /// <summary>LRU-evict whole rooms' message windows beyond the room cap.</summary>
    private void EvictMessageRoomsIfNeeded()
    {
        while (_messages.Count > MaxMessageRooms)
        {
            var oldest = DateTimeOffset.MaxValue;
            Guid oldestKey = Guid.Empty;
            foreach (var kv in _messages)
            {
                DateTimeOffset updated;
                lock (kv.Value)
                {
                    updated = kv.Value.UpdatedAt;
                }

                if (updated < oldest)
                {
                    oldest = updated;
                    oldestKey = kv.Key;
                }
            }

            if (oldestKey == Guid.Empty || !_messages.TryRemove(oldestKey, out _))
            {
                break;
            }
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
        EnqueueWrite(new DeleteRoomMessagesOp(roomId));
    }

    public void RemoveRoomMessage(Guid roomId, Guid messageId, string? clientMessageId = null)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        if (_messages.TryGetValue(roomId, out var entry))
        {
            lock (entry)
            {
                for (var i = entry.Messages.Count - 1; i >= 0; i--)
                {
                    var m = entry.Messages[i];
                    var idMatch = messageId != Guid.Empty && m.Id == messageId;
                    var clientMatch = !string.IsNullOrEmpty(clientMessageId)
                                      && string.Equals(m.ClientMessageId, clientMessageId, StringComparison.Ordinal);
                    if (idMatch || clientMatch)
                    {
                        if (m.Id != Guid.Empty)
                        {
                            entry.KnownIds.Remove(m.Id);
                        }

                        entry.Messages.RemoveAt(i);
                    }
                }

                entry.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        EnqueueWrite(new DeleteMessageOp(roomId, messageId, clientMessageId));
    }

    private void EnqueueWrite(ChatWriteOp op)
    {
        try
        {
            _writePump?.Enqueue(op);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatDataCache] dual-write enqueue: {ex.Message}");
        }
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

        lock (_memberGate)
        {
            if (!_members.TryGetValue(roomId, out var entry) || entry.Members.Count == 0)
            {
                return false;
            }

            entry.LastAccessSequence = ++_memberAccessSequence;
            members = entry.Members.ToList();
            loadedAt = entry.LoadedAt;
            return true;
        }
    }

    public void SetRoomMembers(Guid roomId, IReadOnlyList<SnChatMember> members)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(members);
        lock (_memberGate)
        {
            _members[roomId] = new MemberCacheEntry
            {
                Members = members.ToList(),
                LoadedAt = DateTimeOffset.UtcNow,
                LastAccessSequence = ++_memberAccessSequence,
            };

            while (_members.Count > MaxMemberRooms)
            {
                var oldest = _members.MinBy(static kv => kv.Value.LastAccessSequence);
                if (!_members.Remove(oldest.Key))
                {
                    break;
                }
            }
        }
    }

    public bool IsRoomMembersFresh(Guid roomId, TimeSpan? ttl = null)
    {
        var window = ttl ?? DefaultMembersTtl;
        lock (_memberGate)
        {
            if (!_members.TryGetValue(roomId, out var entry) || entry.Members.Count == 0)
            {
                return false;
            }

            entry.LastAccessSequence = ++_memberAccessSequence;
            return DateTimeOffset.UtcNow - entry.LoadedAt < window;
        }
    }

    private sealed class MemberCacheEntry
    {
        public required List<SnChatMember> Members { get; init; }

        public DateTimeOffset LoadedAt { get; init; }

        public long LastAccessSequence { get; set; }
    }
}
