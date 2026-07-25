using SolarWin.Models;

namespace SolarWin.Data;

/// <summary>
/// Read-side local message queries (Phase 3+). Short-lived DbContext per call.
/// </summary>
public interface IChatLocalStore
{
    /// <summary>
    /// Keyset page of messages older than <paramref name="olderThan"/> (exclusive).
    /// When <paramref name="olderThan"/> is null, returns the newest <paramref name="take"/> rows
    /// (first-page shape for Phase 4 cold paint).
    /// Result order: ascending by RoomSequence, then MessageId (UI insert-ready).
    /// </summary>
    Task<IReadOnlyList<SnChatMessage>> GetOlderMessagesAsync(
        Guid roomId,
        MessageKeysetCursor? olderThan,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Newest <paramref name="take"/> messages for a room (Phase 4 first paint).
    /// Equivalent to <see cref="GetOlderMessagesAsync"/> with a null cursor.
    /// </summary>
    Task<IReadOnlyList<SnChatMessage>> GetNewestMessagesAsync(
        Guid roomId,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Messages strictly newer than <paramref name="newerThan"/> (Phase 5 tail rehydrate).
    /// When <paramref name="newerThan"/> is null, same as <see cref="GetNewestMessagesAsync"/>.
    /// Result order: ascending (oldest → newest within the page).
    /// </summary>
    Task<IReadOnlyList<SnChatMessage>> GetNewerMessagesAsync(
        Guid roomId,
        MessageKeysetCursor? newerThan,
        int take,
        CancellationToken cancellationToken = default);
}
