namespace SolarWin.Helpers;

/// <summary>User preferences for desktop shell (tray, notifications, close behavior).</summary>
public static class AppSettings
{
    private const string MinimizeToTrayKey = "MinimizeToTray";
    private const string CloseToTrayKey = "CloseToTray";
    private const string SystemNotificationsKey = "UseSystemNotifications";
    private const string ProtocolRegisteredKey = "ProtocolRegistered";

    public static bool MinimizeToTray
    {
        get => SettingsStore.GetString(MinimizeToTrayKey) is not "0";
        set => SettingsStore.SetString(MinimizeToTrayKey, value ? "1" : "0");
    }

    public static bool CloseToTray
    {
        get => SettingsStore.GetString(CloseToTrayKey) is not "0";
        set => SettingsStore.SetString(CloseToTrayKey, value ? "1" : "0");
    }

    public static bool UseSystemNotifications
    {
        get => SettingsStore.GetString(SystemNotificationsKey) is not "0";
        set => SettingsStore.SetString(SystemNotificationsKey, value ? "1" : "0");
    }

    private const string ChatMessageNotificationsKey = "ChatMessageNotifications";

    /// <summary>Popup when a DM/group message arrives while in tray / other page (default on).</summary>
    public static bool ChatMessageNotifications
    {
        get => SettingsStore.GetString(ChatMessageNotificationsKey) is not "0";
        set => SettingsStore.SetString(ChatMessageNotificationsKey, value ? "1" : "0");
    }

    public static bool ProtocolRegisteredOnce
    {
        get => SettingsStore.GetString(ProtocolRegisteredKey) == "1";
        set => SettingsStore.SetString(ProtocolRegisteredKey, value ? "1" : "0");
    }
}
