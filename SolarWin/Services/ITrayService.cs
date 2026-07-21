namespace SolarWin.Services;

public interface ITrayService : IDisposable
{
    void Initialize();

    /// <summary>Re-create tray icon if missing (call before hide-to-tray).</summary>
    void EnsureVisible();

    void ShowBalloon(string title, string text);

    void SetToolTip(string text);

    bool IsInitialized { get; }

    /// <summary>Last initialization failure message (if any).</summary>
    string? LastError { get; }
}
