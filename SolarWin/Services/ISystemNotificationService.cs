namespace SolarWin.Services;

/// <summary>Windows Action Center / toast notifications (OS-level, not in-app banner).</summary>
public interface ISystemNotificationService
{
    /// <summary>Show a system toast. Failures are swallowed (best-effort).</summary>
    void Show(string title, string body, string? launchArgs = null);

    void ShowInfo(string body) => Show("Solar Network", body);

    void ShowSuccess(string body) => Show("Solar Network", body);

    void ShowError(string body) => Show("Solar Network", body);
}
