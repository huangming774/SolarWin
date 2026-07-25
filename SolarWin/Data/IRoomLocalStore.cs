using SolarWin.Data.Entities;
using SolarWin.Models;

namespace SolarWin.Data;

/// <summary>Read-side room list queries (Phase 6).</summary>
public interface IRoomLocalStore
{
    /// <summary>
    /// Active rooms ordered by Pinned DESC, LastActivity DESC (sole list index).
    /// </summary>
    Task<IReadOnlyList<ChatRoomEntity>> GetRoomsOrderedAsync(
        CancellationToken cancellationToken = default);

    Task<ChatRoomEntity?> GetRoomAsync(Guid roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Import OfflineCache JSON <c>chat_rooms_{account}</c> once when table is empty.
    /// </summary>
    Task<bool> TryImportLegacyJsonAsync(Guid accountId, CancellationToken cancellationToken = default);
}
