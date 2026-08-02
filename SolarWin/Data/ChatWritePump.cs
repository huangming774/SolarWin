using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SolarWin.Data.Entities;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Data;

/// <summary>
/// Single-consumer write pump: 50ms / 100-op batching, upsert on UNIQUE conflicts,
/// full eviction once per bind (design v1.0 §1.7 + Phase 2).
/// </summary>
public sealed class ChatWritePump : IChatWritePump, IAsyncDisposable
{
    public static readonly TimeSpan BatchWindow = TimeSpan.FromMilliseconds(50);
    public const int BatchMaxOps = 100;

    /// <summary>Design §1.7: max messages retained per room.</summary>
    public const int MaxMessagesPerRoom = 5000;

    /// <summary>Design §1.7: max age.</summary>
    public static readonly TimeSpan MaxMessageAge = TimeSpan.FromDays(90);

    public const int EvictionDeleteBatch = 500;

    /// <summary>Design §1.7: soft cap on single-account db file size.</summary>
    public const long SoftDbMaxBytes = 512L * 1024 * 1024;

    /// <summary>
    /// Max queued write ops. A stalled DB (locked file etc.) must not pile up ops in
    /// memory without bound; when full, the oldest op is dropped — the server remains
    /// the source of truth and the next sync backfills the L2 mirror.
    /// </summary>
    public const int QueueCapacity = 4096;

    private readonly IAccountDbContextFactory _dbFactory;
    private readonly object _lifecycle = new();

    private Channel<ChatWriteOp>? _channel;
    private CancellationTokenSource? _cts;
    private Task? _consumer;
    private Guid? _evictionScheduledForAccount;

    private long _enqueuedTotal;
    private long _droppedTotal;
    private long _batchesSucceeded;
    private long _batchesFailed;

    public ChatWritePump(IAccountDbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    // Read from the channel itself: with DropOldest a manual counter can never be
    // decremented for dropped ops and would drift permanently high.
    public int QueueDepth => _channel?.Reader.Count ?? 0;

    public long EnqueuedTotal => Interlocked.Read(ref _enqueuedTotal);

    public long DroppedTotal => Interlocked.Read(ref _droppedTotal);

    public long BatchesSucceeded => Interlocked.Read(ref _batchesSucceeded);

    public long BatchesFailed => Interlocked.Read(ref _batchesFailed);

    public double BatchFailureRate
    {
        get
        {
            var ok = BatchesSucceeded;
            var fail = BatchesFailed;
            var total = ok + fail;
            return total <= 0 ? 0 : (double)fail / total;
        }
    }

    public void EnsureRunning()
    {
        lock (_lifecycle)
        {
            StartConsumer_NoLock();
        }
    }

    public void Enqueue(ChatWriteOp op)
    {
        ArgumentNullException.ThrowIfNull(op);
        try
        {
            ChannelWriter<ChatWriteOp>? writer;
            lock (_lifecycle)
            {
                StartConsumer_NoLock();
                writer = _channel?.Writer;
            }

            if (writer is null || !writer.TryWrite(op))
            {
                Interlocked.Increment(ref _droppedTotal);
                Debug.WriteLine($"[ChatWritePump] Dropped op {op.GetType().Name} (channel closed).");
                return;
            }

            Interlocked.Increment(ref _enqueuedTotal);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _droppedTotal);
            Debug.WriteLine($"[ChatWritePump] Enqueue failed: {ex.Message}");
        }
    }

    public async Task CompleteAndDrainAsync(CancellationToken cancellationToken = default)
    {
        Task? consumer;
        CancellationTokenSource? cts;
        lock (_lifecycle)
        {
            try
            {
                // Complete writer so the consumer drains remaining items then exits.
                _channel?.Writer.TryComplete();
            }
            catch
            {
                // ignore
            }

            consumer = _consumer;
            cts = _cts;
            _channel = null;
            _consumer = null;
            _cts = null;
            _evictionScheduledForAccount = null;
        }

        if (consumer is not null)
        {
            try
            {
                await consumer.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                try { cts?.Cancel(); } catch { /* ignore */ }
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatWritePump] Drain observed: {ex.Message}");
            }
        }

        try { cts?.Dispose(); } catch { /* ignore */ }
    }

    public async ValueTask DisposeAsync()
    {
        await CompleteAndDrainAsync().ConfigureAwait(false);
    }

    private void StartConsumer_NoLock()
    {
        if (_consumer is { IsCompleted: false })
        {
            return;
        }

        var channel = Channel.CreateBounded<ChatWriteOp>(
            new BoundedChannelOptions(QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.DropOldest,
            },
            droppedOp =>
            {
                Interlocked.Increment(ref _droppedTotal);
                Debug.WriteLine($"[ChatWritePump] Evicted queued op {droppedOp.GetType().Name}.");
            });
        var cts = new CancellationTokenSource();
        _channel = channel;
        _cts = cts;
        _consumer = ConsumeAsync(channel.Reader, cts.Token);

        // One-shot full eviction after this account's migrate gate opens.
        ScheduleEvictionIfNeeded_NoLock(channel.Writer);
    }

    private void ScheduleEvictionIfNeeded_NoLock(ChannelWriter<ChatWriteOp> writer)
    {
        var accountId = _dbFactory.BoundAccountId;
        if (accountId is not { } id || id == Guid.Empty)
        {
            return;
        }

        if (_evictionScheduledForAccount == id)
        {
            return;
        }

        _evictionScheduledForAccount = id;
        if (writer.TryWrite(new RunFullEvictionOp()))
        {
            Interlocked.Increment(ref _enqueuedTotal);
        }
    }

    private async Task ConsumeAsync(ChannelReader<ChatWriteOp> reader, CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var batch = new List<ChatWriteOp>(BatchMaxOps);
                while (batch.Count < BatchMaxOps && reader.TryRead(out var op))
                {
                    batch.Add(op);
                }

                if (batch.Count < BatchMaxOps)
                {
                    using var windowCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    windowCts.CancelAfter(BatchWindow);
                    try
                    {
                        while (batch.Count < BatchMaxOps
                               && await reader.WaitToReadAsync(windowCts.Token).ConfigureAwait(false))
                        {
                            while (batch.Count < BatchMaxOps && reader.TryRead(out var more))
                            {
                                batch.Add(more);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // batch window elapsed
                    }
                }

                if (batch.Count == 0)
                {
                    continue;
                }

                await RunBatchCountedAsync(CoalesceBatch(batch), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // shutdown
        }
        catch (ChannelClosedException)
        {
            // normal complete
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatWritePump] Consumer stopped: {ex}");
        }

        // Drain anything left after writer completed (cancellation may have aborted WaitToRead).
        try
        {
            var tail = new List<ChatWriteOp>();
            while (reader.TryRead(out var op))
            {
                tail.Add(op);
            }

            if (tail.Count > 0)
            {
                await RunBatchCountedAsync(CoalesceBatch(tail), CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatWritePump] Tail drain failed: {ex.Message}");
        }
    }

    private async Task RunBatchCountedAsync(List<ChatWriteOp> batch, CancellationToken cancellationToken)
    {
        try
        {
            await ProcessBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _batchesSucceeded);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _batchesFailed);
            // Never surface to UI (design Phase 2).
            Debug.WriteLine($"[ChatWritePump] Batch failed: {ex}");
        }
    }

    /// <summary>
    /// Collapse idempotent updates before touching SQLite. Ordering-sensitive operations
    /// (delete versus upsert, page replacement, eviction) deliberately remain in place.
    /// </summary>
    private static List<ChatWriteOp> CoalesceBatch(List<ChatWriteOp> batch)
    {
        if (batch.Count < 2)
        {
            return batch;
        }

        var keep = new bool[batch.Count];
        Array.Fill(keep, true);
        var messageUpdates = new HashSet<(Guid RoomId, string Key)>();
        var roomReads = new Dictionary<Guid, int>();
        var roomDeletes = new HashSet<Guid>();
        var messageDeletes = new HashSet<(Guid RoomId, Guid MessageId, string ClientId)>();

        for (var i = batch.Count - 1; i >= 0; i--)
        {
            switch (batch[i])
            {
                case UpsertMessageOp upsert:
                {
                    var roomId = upsert.RoomId != Guid.Empty ? upsert.RoomId : upsert.Message.ChatRoomId;
                    var key = upsert.Message.Id != Guid.Empty
                        ? "m:" + upsert.Message.Id.ToString("N")
                        : !string.IsNullOrWhiteSpace(upsert.Message.ClientMessageId)
                            ? "c:" + upsert.Message.ClientMessageId
                            : null;
                    if (roomId != Guid.Empty && key is not null
                        && !messageUpdates.Add((roomId, key)))
                    {
                        keep[i] = false;
                    }

                    break;
                }
                case UpdateRoomReadOp read when read.RoomId != Guid.Empty:
                    if (roomReads.TryGetValue(read.RoomId, out var laterIndex)
                        && batch[laterIndex] is UpdateRoomReadOp later)
                    {
                        // The newest unread count wins, while the read cursor is monotonic.
                        batch[laterIndex] = later with
                        {
                            LastReadSequence = Math.Max(read.LastReadSequence, later.LastReadSequence),
                        };
                        keep[i] = false;
                    }
                    else
                    {
                        roomReads[read.RoomId] = i;
                    }

                    break;
                case DeleteRoomListOp deleteRoom when deleteRoom.RoomId != Guid.Empty:
                    if (!roomDeletes.Add(deleteRoom.RoomId)) keep[i] = false;
                    break;
                case DeleteMessageOp deleteMessage when deleteMessage.RoomId != Guid.Empty:
                {
                    var key = (
                        deleteMessage.RoomId,
                        deleteMessage.MessageId,
                        deleteMessage.ClientMessageId ?? string.Empty);
                    if (!messageDeletes.Add(key)) keep[i] = false;
                    break;
                }
            }
        }

        var result = new List<ChatWriteOp>(batch.Count);
        for (var i = 0; i < batch.Count; i++)
        {
            if (keep[i]) result.Add(batch[i]);
        }

        return result;
    }

    private async Task ProcessBatchAsync(List<ChatWriteOp> batch, CancellationToken cancellationToken)
    {
        if (_dbFactory.BoundAccountId is null)
        {
            return;
        }

        try
        {
            await _dbFactory.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatWritePump] DB not ready: {ex.Message}");
            return;
        }

        await using var db = _dbFactory.CreateDbContext();
        var runFullEviction = batch.Any(static op => op is RunFullEvictionOp);
        var writeOps = batch.Where(static op => op is not RunFullEvictionOp).ToList();

        if (writeOps.Count > 0)
        {
            await using var transaction = await db.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PrefetchBatchRowsAsync(db, writeOps, cancellationToken).ConfigureAwait(false);

            foreach (var op in writeOps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (op)
                {
                    case UpsertMessageOp upsert:
                        UpsertOne(db, upsert.RoomId, upsert.Message, upsert.Source);
                        if (upsert.RoomId != Guid.Empty)
                        {
                            UpdateRoomPreviewIfNewer(db, upsert.RoomId, upsert.Message);
                        }

                        break;

                    case UpsertMessagesBatchOp page:
                        foreach (var msg in page.Messages)
                        {
                            UpsertOne(db, page.RoomId, msg, page.Source);
                        }

                        break;

                    case DeleteMessageOp del:
                        await DeleteMessageAsync(db, del, cancellationToken).ConfigureAwait(false);
                        break;

                    case DeleteRoomMessagesOp delRoom:
                        await db.Messages
                            .Where(m => m.RoomId == delRoom.RoomId)
                            .ExecuteDeleteAsync(cancellationToken)
                            .ConfigureAwait(false);
                        break;

                    case UpsertRoomOp roomOp:
                        UpsertRoom(db, roomOp.Room, roomOp.Summary, roomOp.Source, roomOp.LastReadSequence);
                        break;

                    case UpsertRoomsBatchOp roomsBatch:
                        await UpsertRoomsBatchAsync(db, roomsBatch, cancellationToken).ConfigureAwait(false);
                        break;

                    case UpdateRoomReadOp readOp:
                        UpdateRoomRead(db, readOp);
                        break;

                    case UpdateRoomPreviewOp previewOp:
                        UpdateRoomPreview(db, previewOp);
                        break;

                    case DeleteRoomListOp delList:
                        await db.Rooms
                            .Where(r => r.RoomId == delList.RoomId)
                            .ExecuteDeleteAsync(cancellationToken)
                            .ConfigureAwait(false);
                        break;
                }
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        // Retention is deliberately outside every real-time write transaction. The
        // maintenance marker is queued once per account bind and runs as a cold task.
        if (runFullEviction)
        {
            db.ChangeTracker.Clear();
            await EvictAllRoomsAsync(db, cancellationToken).ConfigureAwait(false);
            await EvictByFileSizeIfNeededAsync(db, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task PrefetchBatchRowsAsync(
        SolarWinDbContext db,
        IReadOnlyList<ChatWriteOp> batch,
        CancellationToken cancellationToken)
    {
        var roomIds = new HashSet<Guid>();
        var messageIds = new HashSet<Guid>();
        var clientIds = new HashSet<string>(StringComparer.Ordinal);

        void AddMessage(Guid requestedRoomId, SnChatMessage message)
        {
            var roomId = requestedRoomId != Guid.Empty ? requestedRoomId : message.ChatRoomId;
            if (roomId != Guid.Empty) roomIds.Add(roomId);
            if (message.Id != Guid.Empty) messageIds.Add(message.Id);
            if (!string.IsNullOrWhiteSpace(message.ClientMessageId)) clientIds.Add(message.ClientMessageId);
        }

        foreach (var op in batch)
        {
            switch (op)
            {
                case UpsertMessageOp one:
                    AddMessage(one.RoomId, one.Message);
                    break;
                case UpsertMessagesBatchOp many:
                    foreach (var message in many.Messages) AddMessage(many.RoomId, message);
                    break;
                case UpsertRoomOp room when room.Room.Id != Guid.Empty:
                    roomIds.Add(room.Room.Id);
                    break;
                case UpsertRoomsBatchOp rooms:
                    foreach (var (room, _) in rooms.Rooms)
                        if (room.Id != Guid.Empty) roomIds.Add(room.Id);
                    break;
                case UpdateRoomReadOp read when read.RoomId != Guid.Empty:
                    roomIds.Add(read.RoomId);
                    break;
                case UpdateRoomPreviewOp preview when preview.RoomId != Guid.Empty:
                    roomIds.Add(preview.RoomId);
                    break;
            }
        }

        var roomIdList = roomIds.ToArray();
        foreach (var chunk in roomIdList.Chunk(400))
        {
            await db.Rooms.Where(r => chunk.Contains(r.RoomId))
                .LoadAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var chunk in messageIds.Chunk(400))
        {
            await db.Messages
                .Where(m => chunk.Contains(m.MessageId))
                .LoadAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var chunk in clientIds.Chunk(400))
        {
            await db.Messages
                .Where(m => m.ClientMessageId != null
                            && chunk.Contains(m.ClientMessageId))
                .LoadAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void UpsertRoom(
        SolarWinDbContext db,
        SnChatRoom room,
        ChatSummaryResponse? summary,
        ChatMessageSource source,
        long? lastReadSequence)
    {
        if (room.Id == Guid.Empty)
        {
            return;
        }

        var existing = db.Rooms.Local.FirstOrDefault(r => r.RoomId == room.Id);

        if (existing is null)
        {
            db.Rooms.Add(ChatRoomMapper.ToEntity(room, summary, source, lastReadSequence));
        }
        else
        {
            // Do not regress LastReadSequence on full pull unless server provides a higher value.
            var keepRead = existing.LastReadSequence;
            ChatRoomMapper.UpdateEntity(existing, room, summary, source, lastReadSequence);
            if (lastReadSequence is null || lastReadSequence < keepRead)
            {
                existing.LastReadSequence = keepRead;
            }

            existing.DeletedAt = null;
        }
    }

    private static async Task UpsertRoomsBatchAsync(
        SolarWinDbContext db,
        UpsertRoomsBatchOp batch,
        CancellationToken cancellationToken)
    {
        var serverIds = new HashSet<Guid>();
        foreach (var (room, summary) in batch.Rooms)
        {
            if (room.Id == Guid.Empty)
            {
                continue;
            }

            serverIds.Add(room.Id);
            UpsertRoom(db, room, summary, batch.Source, lastReadSequence: null);
        }

        if (!batch.RemoveMissing || serverIds.Count == 0)
        {
            return;
        }

        // Hard-delete missing list rows (message history untouched).
        await db.Rooms
            .Where(r => !serverIds.Contains(r.RoomId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static void UpdateRoomRead(SolarWinDbContext db, UpdateRoomReadOp op)
    {
        if (op.RoomId == Guid.Empty)
        {
            return;
        }

        var row = db.Rooms.Local.FirstOrDefault(r => r.RoomId == op.RoomId);
        if (row is null)
        {
            return;
        }

        row.UnreadCount = Math.Max(0, op.UnreadCount);
        if (op.LastReadSequence > row.LastReadSequence)
        {
            row.LastReadSequence = op.LastReadSequence;
        }

        row.SyncedAt = DateTimeOffset.UtcNow;
    }

    private static void UpdateRoomPreviewIfNewer(
        SolarWinDbContext db,
        Guid roomId,
        SnChatMessage message)
    {
        if (roomId == Guid.Empty || message is null)
        {
            return;
        }

        var row = db.Rooms.Local.FirstOrDefault(r => r.RoomId == roomId);
        if (row is null)
        {
            return;
        }

        // Skip older history pages / out-of-order WS so we do not thrash preview on LoadMore.
        if (message.RoomSequence > 0
            && row.LastMessageSequence is { } curSeq
            && message.RoomSequence < curSeq)
        {
            return;
        }

        if (message.RoomSequence > 0
            && row.LastMessageSequence == message.RoomSequence
            && row.LastMessageId is { } curId
            && message.Id != Guid.Empty
            && message.Id != curId
            && string.CompareOrdinal(message.Id.ToString("N"), curId.ToString("N")) < 0)
        {
            return;
        }

        if (message.CreatedAt is { } created
            && row.LastMessageAt is { } lastAt
            && message.RoomSequence <= 0
            && created < lastAt)
        {
            return;
        }

        UpdateRoomPreview(
            db,
            new UpdateRoomPreviewOp(
                roomId,
                message,
                UnreadCount: null,
                LastActivity: message.CreatedAt ?? DateTimeOffset.UtcNow));
    }

    private static void UpdateRoomPreview(SolarWinDbContext db, UpdateRoomPreviewOp op)
    {
        if (op.RoomId == Guid.Empty)
        {
            return;
        }

        var row = db.Rooms.Local.FirstOrDefault(r => r.RoomId == op.RoomId);
        if (row is null)
        {
            // Room list may not be hydrated yet; skip preview-only update.
            return;
        }

        if (op.LastMessage is not null)
        {
            // Explicit summary/socket path: still ignore strictly older sequences when known.
            if (op.LastMessage.RoomSequence > 0
                && row.LastMessageSequence is { } curSeq
                && op.LastMessage.RoomSequence < curSeq)
            {
                if (op.UnreadCount is { } unreadOnly)
                {
                    row.UnreadCount = Math.Max(0, unreadOnly);
                    row.SyncedAt = DateTimeOffset.UtcNow;
                }

                return;
            }

            ChatRoomMapper.ApplyLastMessage(row, op.LastMessage);
        }

        if (op.LastActivity is { } act && act > row.LastActivity)
        {
            row.LastActivity = act;
        }

        if (op.UnreadCount is { } unread)
        {
            row.UnreadCount = Math.Max(0, unread);
        }

        row.SyncedAt = DateTimeOffset.UtcNow;
    }

    private static void UpsertOne(
        SolarWinDbContext db,
        Guid roomId,
        SnChatMessage message,
        ChatMessageSource source)
    {
        if (message is null)
        {
            return;
        }

        var effectiveRoom = roomId != Guid.Empty ? roomId : message.ChatRoomId;
        if (effectiveRoom == Guid.Empty)
        {
            return;
        }

        // Prefer change-tracker (same batch may Add then re-upsert before SaveChanges).
        ChatMessageEntity? existing = null;

        if (message.Id != Guid.Empty)
        {
            existing = db.Messages.Local.FirstOrDefault(
                m => m.RoomId == effectiveRoom && m.MessageId == message.Id);
        }

        if (existing is null && !string.IsNullOrWhiteSpace(message.ClientMessageId))
        {
            var clientId = message.ClientMessageId;
            existing = db.Messages.Local.FirstOrDefault(
                m => m.RoomId == effectiveRoom
                     && string.Equals(m.ClientMessageId, clientId, StringComparison.Ordinal));
        }

        if (existing is null)
        {
            db.Messages.Add(ChatMessageMapper.ToEntity(message, effectiveRoom, source));
        }
        else
        {
            ChatMessageMapper.UpdateEntity(existing, message, effectiveRoom, source);
        }
    }

    private static async Task DeleteMessageAsync(
        SolarWinDbContext db,
        DeleteMessageOp del,
        CancellationToken cancellationToken)
    {
        if (del.RoomId == Guid.Empty)
        {
            return;
        }

        if (del.MessageId != Guid.Empty)
        {
            await db.Messages
                .Where(m => m.RoomId == del.RoomId && m.MessageId == del.MessageId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(del.ClientMessageId))
        {
            var clientId = del.ClientMessageId;
            await db.Messages
                .Where(m => m.RoomId == del.RoomId && m.ClientMessageId == clientId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task EvictRoomAsync(
        SolarWinDbContext db,
        Guid roomId,
        CancellationToken cancellationToken)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        var cutoff = DateTimeOffset.UtcNow - MaxMessageAge;

        // Age: delete oldest-by-sequence in batches.
        while (true)
        {
            var oldIds = await db.Messages
                .AsNoTracking()
                .Where(m => m.RoomId == roomId
                            && ((m.CreatedAt != null && m.CreatedAt < cutoff)
                                || (m.CreatedAt == null && m.SyncedAt < cutoff)))
                .OrderBy(m => m.RoomSequence)
                .ThenBy(m => m.MessageId)
                .Select(m => m.RowId)
                .Take(EvictionDeleteBatch)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (oldIds.Count == 0)
            {
                break;
            }

            await db.Messages
                .Where(m => oldIds.Contains(m.RowId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            if (oldIds.Count < EvictionDeleteBatch)
            {
                break;
            }
        }

        // Count cap: keep newest MaxMessagesPerRoom by RoomSequence.
        var total = await db.Messages.CountAsync(m => m.RoomId == roomId, cancellationToken)
            .ConfigureAwait(false);
        var excess = total - MaxMessagesPerRoom;
        while (excess > 0)
        {
            var take = Math.Min(excess, EvictionDeleteBatch);
            var oldest = await db.Messages
                .AsNoTracking()
                .Where(m => m.RoomId == roomId)
                .OrderBy(m => m.RoomSequence)
                .ThenBy(m => m.MessageId)
                .Select(m => m.RowId)
                .Take(take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (oldest.Count == 0)
            {
                break;
            }

            await db.Messages
                .Where(m => oldest.Contains(m.RowId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            excess -= oldest.Count;
        }
    }

    private static async Task EvictAllRoomsAsync(SolarWinDbContext db, CancellationToken cancellationToken)
    {
        var roomIds = await db.Messages
            .AsNoTracking()
            .Select(m => m.RoomId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var roomId in roomIds)
        {
            await EvictRoomAsync(db, roomId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EvictByFileSizeIfNeededAsync(SolarWinDbContext db, CancellationToken cancellationToken)
    {
        var path = _dbFactory.BoundDatabasePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        long length;
        try
        {
            length = new FileInfo(path).Length;
        }
        catch
        {
            return;
        }

        if (length <= SoftDbMaxBytes)
        {
            return;
        }

        // Another full pass (already ran room rules); if still huge, drop oldest globally in batches.
        Debug.WriteLine($"[ChatWritePump] DB size {length} > {SoftDbMaxBytes}; extra global trim.");
        var cutoff = DateTimeOffset.UtcNow - MaxMessageAge;
        while (true)
        {
            var oldIds = await db.Messages
                .AsNoTracking()
                .Where(m => (m.CreatedAt != null && m.CreatedAt < cutoff)
                            || (m.CreatedAt == null && m.SyncedAt < cutoff))
                .OrderBy(m => m.RoomSequence)
                .Select(m => m.RowId)
                .Take(EvictionDeleteBatch)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (oldIds.Count == 0)
            {
                break;
            }

            await db.Messages
                .Where(m => oldIds.Contains(m.RowId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
