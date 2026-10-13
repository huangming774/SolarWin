namespace SolarWin.Helpers;

/// <summary>Exact-origin gate for credentials used by Solar media downloads.</summary>
internal static class TrustedMediaOrigin
{
    internal const string SolarApiOrigin = "https://api.solian.app";

    internal static bool IsTrusted(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var candidate)
            || !string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || candidate.UserInfo.Length != 0)
        {
            return false;
        }

        var trusted = new Uri(SolarApiOrigin);
        return string.Equals(candidate.Host, trusted.Host, StringComparison.OrdinalIgnoreCase)
               && candidate.Port == trusted.Port;
    }
}
