using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SolarWin.Models;

namespace SolarWin.Data;

/// <summary>SQLite keyset reads for chat history (design v1.0 Phase 3).</summary>
public sealed class ChatLocalStore : IChatLocalStore
{
    private readonly IAccountDbContextFactory _dbFactory;

    public ChatLocalStore(IAccountDbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public Task<IReadOnlyList<SnChatMessage>> GetNewestMessagesAsync(
        Guid roomId,
        int take,
        CancellationToken cancellationToken = default)
        => GetOlderMessagesAsync(roomId, olderThan: null, take, cancellationToken);

    public async Task<IReadOnlyList<SnChatMessage>> GetNewerMessagesAsync(
        Guid roomId,
        MessageKeysetCursor? newerThan,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty || take <= 0)
        {
            return Array.Empty<SnChatMessage>();
        }

        if (newerThan is null)
        {
            return await GetNewestMessagesAsync(roomId, take, cancellationToken).ConfigureAwait(false);
        }

        if (_dbFactory.BoundAccountId is null)
        {
            return Array.Empty<SnChatMessage>();
        }

        try
        {
            await _dbFactory.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatLocalStore] DB not ready: {ex.Message}");
            return Array.Empty<SnChatMessage>();
        }

        try
        {
            await using var db = _dbFactory.CreateDbContext();
            var seq = newerThan.Value.RoomSequence;
            var mid = newerThan.Value.MessageId;

            // Pull ASC candidates at/after cursor sequence; filter Guid tie in memory.
            var candidates = await db.Messages.AsNoTracking()
                .Where(m => m.RoomId == roomId && m.RoomSequence >= seq)
                .OrderBy(m => m.RoomSequence)
                .ThenBy(m => m.MessageId)
                .Take(take + 64)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var rows = candidates
                .Where(m =>
                    m.RoomSequence > seq
                    || (m.RoomSequence == seq && m.MessageId.CompareTo(mid) > 0))
                .Take(take)
                .ToList();

            if (rows.Count == 0)
            {
                return Array.Empty<SnChatMessage>();
            }

            var list = new List<SnChatMessage>(rows.Count);
            foreach (var row in rows)
            {
                list.Add(ChatMessageMapper.ToDto(row));
            }

            return list;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatLocalStore] GetNewerMessages failed: {ex.Message}");
            return Array.Empty<SnChatMessage>();
        }
    }

    public async Task<IReadOnlyList<SnChatMessage>> GetOlderMessagesAsync(
        Guid roomId,
        MessageKeysetCursor? olderThan,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty || take <= 0)
        {
            return Array.Empty<SnChatMessage>();
        }

        if (_dbFactory.BoundAccountId is null)
        {
            return Array.Empty<SnChatMessage>();
        }

        try
        {
            await _dbFactory.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatLocalStore] DB not ready: {ex.Message}");
            return Array.Empty<SnChatMessage>();
        }

        try
        {
            await using var db = _dbFactory.CreateDbContext();

            // Keyset (design §2): pull DESC, filter cursor, reverse to ASC for UI.
            // Guid tie-break is applied in-memory (SQLite EF may not translate Guid.CompareTo).
            List<Entities.ChatMessageEntity> rows;
            if (olderThan is { } cursor)
            {
                var seq = cursor.RoomSequence;
                var mid = cursor.MessageId;
                var candidates = await db.Messages.AsNoTracking()
                    .Where(m => m.RoomId == roomId && m.RoomSequence <= seq)
                    .OrderByDescending(m => m.RoomSequence)
                    .ThenByDescending(m => m.MessageId)
                    .Take(take + 64)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                rows = candidates
                    .Where(m =>
                        m.RoomSequence < seq
                        || (m.RoomSequence == seq && m.MessageId.CompareTo(mid) < 0))
                    .Take(take)
                    .ToList();
            }
            else
            {
                rows = await db.Messages.AsNoTracking()
                    .Where(m => m.RoomId == roomId)
                    .OrderByDescending(m => m.RoomSequence)
                    .ThenByDescending(m => m.MessageId)
                    .Take(take)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            if (rows.Count == 0)
            {
                return Array.Empty<SnChatMessage>();
            }

            // UI expects ascending (oldest → newest within the page).
            rows.Reverse();
            var list = new List<SnChatMessage>(rows.Count);
            foreach (var row in rows)
            {
                list.Add(ChatMessageMapper.ToDto(row));
            }

            return list;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatLocalStore] GetOlderMessages failed: {ex.Message}");
            return Array.Empty<SnChatMessage>();
        }
    }
}
