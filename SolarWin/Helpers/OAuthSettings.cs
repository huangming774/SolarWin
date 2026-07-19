using Windows.Storage;

namespace SolarWin.Helpers;

/// <summary>Persisted OAuth client settings for device-code login.</summary>
public static class OAuthSettings
{
    private const string ClientIdKey = "OAuthClientId";

    public static string GetClientId()
    {
        var values = ApplicationData.Current.LocalSettings.Values;
        return values.TryGetValue(ClientIdKey, out var raw) && raw is string s ? s : string.Empty;
    }

    public static void SetClientId(string clientId)
    {
        ApplicationData.Current.LocalSettings.Values[ClientIdKey] = clientId?.Trim() ?? string.Empty;
    }
}
