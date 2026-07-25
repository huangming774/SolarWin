namespace SolarWin.Data;

/// <summary>
/// Keyset cursor for older-page loads (design v1.0 §2):
/// next page is strictly older than <see cref="RoomSequence"/> / <see cref="MessageId"/>.
/// </summary>
public readonly record struct MessageKeysetCursor(long RoomSequence, Guid MessageId);
