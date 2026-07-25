using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SolarWin.Data.Entities;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Data;

/// <summary>SQLite room list reads + one-shot JSON import (Phase 6).</summary>
public sealed class RoomLocalStore : IRoomLocalStore
{
    private readonly IAccountDbContextFactory _dbFactory;

    public RoomLocalStore(IAccountDbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<ChatRoomEntity>> GetRoomsOrderedAsync(
        CancellationToken cancellationToken = default)
    {
        if (_dbFactory.BoundAccountId is null)
        {
            return Array.Empty<ChatRoomEntity>();
        }

        try
        {
            await _dbFactory.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RoomLocalStore] DB not ready: {ex.Message}");
            return Array.Empty<ChatRoomEntity>();
        }

        try
        {
            await using var db = _dbFactory.CreateDbContext();
            return await db.Rooms.AsNoTracking()
                .Where(r => r.DeletedAt == null)
                .OrderByDescending(r => r.Pinned)
                .ThenByDescending(r => r.LastActivity)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RoomLocalStore] GetRoomsOrdered failed: {ex.Message}");
            return Array.Empty<ChatRoomEntity>();
        }
    }

    public async Task<ChatRoomEntity?> GetRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty || _dbFactory.BoundAccountId is null)
        {
            return null;
        }

        try
        {
            await _dbFactory.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
            await using var db = _dbFactory.CreateDbContext();
            return await db.Rooms.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoomId == roomId && r.DeletedAt == null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RoomLocalStore] GetRoom failed: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> TryImportLegacyJsonAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        if (accountId == Guid.Empty)
        {
            return false;
        }

        try
        {
            await _dbFactory.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }

        try
        {
            await using var db = _dbFactory.CreateDbContext();
            if (await db.Rooms.AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                // Already has rows — drop legacy file if present.
                OfflineCache.Remove($"chat_rooms_{accountId:N}");
                return false;
            }

            var key = $"chat_rooms_{accountId:N}";
            if (!OfflineCache.TryGetJson<List<SnChatRoom>>(key, out var rooms, allowExpired: true)
                || rooms is not { Count: > 0 })
            {
                return false;
            }

            foreach (var room in rooms)
            {
                if (room.Id == Guid.Empty)
                {
                    continue;
                }

                db.Rooms.Add(ChatRoomMapper.ToEntity(room, summary: null, ChatMessageSource.Api));
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            OfflineCache.Remove(key);
            Debug.WriteLine($"[RoomLocalStore] Imported {rooms.Count} rooms from JSON for {accountId:N}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RoomLocalStore] Import legacy JSON failed: {ex.Message}");
            return false;
        }
    }
}
