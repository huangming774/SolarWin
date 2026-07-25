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

        var channel = Channel.CreateBounded<ChatWriteOp>(new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        var cts = new CancellationTokenSource();
        _channel = channel;
        _cts = cts;
        _consumer = Task.Run(() => ConsumeAsync(channel.Reader, cts.Token), CancellationToken.None);

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

                await RunBatchCountedAsync(batch, cancellationToken).ConfigureAwait(false);
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
                await RunBatchCountedAsync(tail, CancellationToken.None).ConfigureAwait(false);
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
        var touchedRooms = new HashSet<Guid>();
        var runFullEviction = false;

        foreach (var op in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (op)
            {
                case UpsertMessageOp upsert:
                    await UpsertOneAsync(db, upsert.RoomId, upsert.Message, upsert.Source, cancellationToken)
                        .ConfigureAwait(false);
                    if (upsert.RoomId != Guid.Empty)
                    {
                        touchedRooms.Add(upsert.RoomId);
                        // Preview only when this message is newer than stored list preview (skip history backfill).
                        await UpdateRoomPreviewIfNewerAsync(db, upsert.RoomId, upsert.Message, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    break;

                case UpsertMessagesBatchOp page:
                    foreach (var msg in page.Messages)
                    {
                        await UpsertOneAsync(db, page.RoomId, msg, page.Source, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    if (page.RoomId != Guid.Empty)
                    {
                        touchedRooms.Add(page.RoomId);
                    }

                    break;

                case DeleteMessageOp del:
                    await DeleteMessageAsync(db, del, cancellationToken).ConfigureAwait(false);
                    if (del.RoomId != Guid.Empty)
                    {
                        touchedRooms.Add(del.RoomId);
                    }

                    break;

                case DeleteRoomMessagesOp delRoom:
                    await db.Messages
                        .Where(m => m.RoomId == delRoom.RoomId)
                        .ExecuteDeleteAsync(cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case RunFullEvictionOp:
                    runFullEviction = true;
                    break;

                case UpsertRoomOp roomOp:
                    await UpsertRoomAsync(
                            db,
                            roomOp.Room,
                            roomOp.Summary,
                            roomOp.Source,
                            roomOp.LastReadSequence,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;

                case UpsertRoomsBatchOp roomsBatch:
                    await UpsertRoomsBatchAsync(db, roomsBatch, cancellationToken).ConfigureAwait(false);
                    break;

                case UpdateRoomReadOp readOp:
                    await UpdateRoomReadAsync(db, readOp, cancellationToken).ConfigureAwait(false);
                    break;

                case UpdateRoomPreviewOp previewOp:
                    await UpdateRoomPreviewAsync(db, previewOp, cancellationToken).ConfigureAwait(false);
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

        // Light per-room eviction after writes (design §1.7 timing #1).
        foreach (var roomId in touchedRooms)
        {
            await EvictRoomAsync(db, roomId, cancellationToken).ConfigureAwait(false);
        }

        if (runFullEviction)
        {
            await EvictAllRoomsAsync(db, cancellationToken).ConfigureAwait(false);
            await EvictByFileSizeIfNeededAsync(db, cancellationToken).ConfigureAwait(false);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task UpsertRoomAsync(
        SolarWinDbContext db,
        SnChatRoom room,
        ChatSummaryResponse? summary,
        ChatMessageSource source,
        long? lastReadSequence,
        CancellationToken cancellationToken)
    {
        if (room.Id == Guid.Empty)
        {
            return;
        }

        var existing = db.Rooms.Local.FirstOrDefault(r => r.RoomId == room.Id)
                       ?? await db.Rooms
                           .FirstOrDefaultAsync(r => r.RoomId == room.Id, cancellationToken)
                           .ConfigureAwait(false);

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
            await UpsertRoomAsync(db, room, summary, batch.Source, lastReadSequence: null, cancellationToken)
                .ConfigureAwait(false);
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

    private static async Task UpdateRoomReadAsync(
        SolarWinDbContext db,
        UpdateRoomReadOp op,
        CancellationToken cancellationToken)
    {
        if (op.RoomId == Guid.Empty)
        {
            return;
        }

        var row = db.Rooms.Local.FirstOrDefault(r => r.RoomId == op.RoomId)
                  ?? await db.Rooms
                      .FirstOrDefaultAsync(r => r.RoomId == op.RoomId, cancellationToken)
                      .ConfigureAwait(false);
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

    private static async Task UpdateRoomPreviewIfNewerAsync(
        SolarWinDbContext db,
        Guid roomId,
        SnChatMessage message,
        CancellationToken cancellationToken)
    {
        if (roomId == Guid.Empty || message is null)
        {
            return;
        }

        var row = db.Rooms.Local.FirstOrDefault(r => r.RoomId == roomId)
                  ?? await db.Rooms
                      .FirstOrDefaultAsync(r => r.RoomId == roomId, cancellationToken)
                      .ConfigureAwait(false);
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

        await UpdateRoomPreviewAsync(
                db,
                new UpdateRoomPreviewOp(
                    roomId,
                    message,
                    UnreadCount: null,
                    LastActivity: message.CreatedAt ?? DateTimeOffset.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task UpdateRoomPreviewAsync(
        SolarWinDbContext db,
        UpdateRoomPreviewOp op,
        CancellationToken cancellationToken)
    {
        if (op.RoomId == Guid.Empty)
        {
            return;
        }

        var row = db.Rooms.Local.FirstOrDefault(r => r.RoomId == op.RoomId)
                  ?? await db.Rooms
                      .FirstOrDefaultAsync(r => r.RoomId == op.RoomId, cancellationToken)
                      .ConfigureAwait(false);
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

    private static async Task UpsertOneAsync(
        SolarWinDbContext db,
        Guid roomId,
        SnChatMessage message,
        ChatMessageSource source,
        CancellationToken cancellationToken)
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
            existing ??= await db.Messages
                .FirstOrDefaultAsync(
                    m => m.RoomId == effectiveRoom && m.MessageId == message.Id,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (existing is null && !string.IsNullOrWhiteSpace(message.ClientMessageId))
        {
            var clientId = message.ClientMessageId;
            existing = db.Messages.Local.FirstOrDefault(
                m => m.RoomId == effectiveRoom
                     && string.Equals(m.ClientMessageId, clientId, StringComparison.Ordinal));
            existing ??= await db.Messages
                .FirstOrDefaultAsync(
                    m => m.RoomId == effectiveRoom && m.ClientMessageId == clientId,
                    cancellationToken)
                .ConfigureAwait(false);
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
