namespace SolarWin.Helpers;

/// <summary>User preferences for desktop shell (tray, notifications, close behavior).</summary>
public static class AppSettings
{
    private const string MinimizeToTrayKey = "MinimizeToTray";
    private const string CloseToTrayKey = "CloseToTray";
    private const string SystemNotificationsKey = "UseSystemNotifications";
    private const string ProtocolRegisteredKey = "ProtocolRegistered";
    private const string FileThumbnailMaxConcurrencyKey = "FileThumbnailMaxConcurrency";
    private const string McpEnabledKey = "McpEnabled";
    private const string McpPortKey = "McpPort";
    private const string McpAccessTokenKey = "McpAccessToken";
    private const string LuckinMcpEndpointKey = "LuckinMcpEndpoint";

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

    /// <summary>Maximum concurrent file thumbnail jobs.</summary>
    public static int FileThumbnailMaxConcurrency
    {
        get
        {
            var raw = SettingsStore.GetString(FileThumbnailMaxConcurrencyKey);
            return int.TryParse(raw, out var value) ? Math.Clamp(value, 1, 16) : 4;
        }
        set => SettingsStore.SetString(FileThumbnailMaxConcurrencyKey, Math.Clamp(value, 1, 16).ToString());
    }

    /// <summary>Whether the local MCP bridge should be started. Disabled by default.</summary>
    public static bool McpEnabled
    {
        get => SettingsStore.GetString(McpEnabledKey) == "1";
        set => SettingsStore.SetString(McpEnabledKey, value ? "1" : "0");
    }

    /// <summary>Loopback-only MCP port. Invalid values fall back to 49321.</summary>
    public static int McpPort
    {
        get
        {
            var raw = SettingsStore.GetString(McpPortKey);
            return int.TryParse(raw, out var value) && value is >= 1024 and <= 65535 ? value : 49321;
        }
        set => SettingsStore.SetString(McpPortKey, Math.Clamp(value, 1024, 65535).ToString());
    }

    /// <summary>Per-install bearer token used by local MCP clients.</summary>
    public static string McpAccessToken
    {
        get
        {
            var token = SettingsStore.GetString(McpAccessTokenKey);
            if (!string.IsNullOrWhiteSpace(token))
            {
                return token;
            }

            token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            SettingsStore.SetString(McpAccessTokenKey, token);
            return token;
        }
    }

    public static string LuckinMcpEndpoint
    {
        get => SettingsStore.GetString(LuckinMcpEndpointKey) ?? "https://gwmcp.lkcoffee.com/order/user/mcp";
        set => SettingsStore.SetString(LuckinMcpEndpointKey, value?.Trim() ?? string.Empty);
    }

    // —— OpenAI-compatible AI chat (user-configured endpoint; no baked-in defaults) ——
    private const string AiApiUrlKey = "AiApiUrl";
    private const string AiApiKeyKey = "AiApiKey";
    private const string AiModelNameKey = "AiModelName";
    private const string AiReasoningEffortKey = "AiReasoningEffort";
    private const string AiUse1MContextKey = "AiUse1MContext";

    /// <summary>Base URL or full chat/completions URL. Empty until user configures.</summary>
    public static string AiApiUrl
    {
        get => SettingsStore.GetString(AiApiUrlKey) ?? string.Empty;
        set => SettingsStore.SetString(AiApiUrlKey, value?.Trim() ?? string.Empty);
    }

    public static string AiApiKey
    {
        get => SettingsStore.GetString(AiApiKeyKey) ?? string.Empty;
        set => SettingsStore.SetString(AiApiKeyKey, value ?? string.Empty);
    }

    /// <summary>Model id; empty until user fills it (no default model).</summary>
    public static string AiModelName
    {
        get => SettingsStore.GetString(AiModelNameKey) ?? string.Empty;
        set => SettingsStore.SetString(AiModelNameKey, value?.Trim() ?? string.Empty);
    }

    /// <summary>low | medium | high | empty (omit from request).</summary>
    public static string AiReasoningEffort
    {
        get => SettingsStore.GetString(AiReasoningEffortKey) ?? string.Empty;
        set => SettingsStore.SetString(AiReasoningEffortKey, value?.Trim() ?? string.Empty);
    }

    /// <summary>When true, keep full history and request long-context friendly options.</summary>
    public static bool AiUse1MContext
    {
        get => SettingsStore.GetString(AiUse1MContextKey) == "1";
        set => SettingsStore.SetString(AiUse1MContextKey, value ? "1" : "0");
    }
}
