namespace SolarWin.Helpers;

/// <summary>Persisted OAuth client settings for device-code login.</summary>
public static class OAuthSettings
{
    private const string ClientIdKey = "OAuthClientId";

    public static string GetClientId()
        => SettingsStore.GetString(ClientIdKey) ?? string.Empty;

    public static void SetClientId(string clientId)
        => SettingsStore.SetString(ClientIdKey, clientId?.Trim() ?? string.Empty);
}
