namespace SolarWin.Services;

/// <summary>
/// Global listener: when a chat message arrives while the user is not viewing that room
/// (or the window is in tray / background), raise a Windows system notification.
/// </summary>
public interface IChatMessageNotifier
{
    /// <summary>Room currently open in ChatDetailPage; null when not in a conversation.</summary>
    Guid? ActiveRoomId { get; set; }

    /// <summary>Start WS (if needed) and subscribe once. Safe to call repeatedly.</summary>
    void Start();

    void Stop();
}
