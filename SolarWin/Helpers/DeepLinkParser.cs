namespace SolarWin.Helpers;

/// <summary>Parse <c>solian://</c> / https deep links into typed actions.</summary>
public static class DeepLinkParser
{
    public const string ProtocolScheme = "solian";

    public static bool IsSolarUri(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        if (!Uri.TryCreate(uri.Trim(), UriKind.Absolute, out var u))
        {
            return false;
        }

        return string.Equals(u.Scheme, ProtocolScheme, StringComparison.OrdinalIgnoreCase)
               || (string.Equals(u.Host, "solian.app", StringComparison.OrdinalIgnoreCase)
                   || u.Host.EndsWith(".solian.app", StringComparison.OrdinalIgnoreCase));
    }

    public static DeepLinkAction Parse(string? uriString)
    {
        if (string.IsNullOrWhiteSpace(uriString)
            || !Uri.TryCreate(uriString.Trim(), UriKind.Absolute, out var uri))
        {
            return DeepLinkAction.Unknown(uriString);
        }

        // solian://auth/qr/{id}
        // solian://user/{name}
        // solian://chat/{roomId}
        // solian://post/{id}
        // https://solian.app/@name
        var scheme = uri.Scheme;
        if (string.Equals(scheme, ProtocolScheme, StringComparison.OrdinalIgnoreCase))
        {
            var host = uri.Host; // first segment sometimes in host for solian://auth/...
            var segs = new List<string>();
            if (!string.IsNullOrWhiteSpace(host))
            {
                segs.Add(host);
            }

            segs.AddRange(uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            return ParseSegments(segs, uriString);
        }

        if (uri.Host.Contains("solian.app", StringComparison.OrdinalIgnoreCase))
        {
            var segs = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (segs.Count >= 1 && segs[0].StartsWith('@'))
            {
                return new DeepLinkAction(DeepLinkKind.UserProfile, segs[0].TrimStart('@'), uriString);
            }

            return ParseSegments(segs, uriString);
        }

        return DeepLinkAction.Unknown(uriString);
    }

    private static DeepLinkAction ParseSegments(IReadOnlyList<string> segs, string raw)
    {
        if (segs.Count == 0)
        {
            return DeepLinkAction.Unknown(raw);
        }

        var a = segs[0].ToLowerInvariant();
        var b = segs.Count > 1 ? segs[1] : null;
        var c = segs.Count > 2 ? segs[2] : null;

        return a switch
        {
            "auth" when string.Equals(b, "qr", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(c)
                => new DeepLinkAction(DeepLinkKind.QrLogin, c!, raw),
            "qr" when !string.IsNullOrWhiteSpace(b)
                => new DeepLinkAction(DeepLinkKind.QrLogin, b!, raw),
            "user" or "u" or "profile" or "@" when !string.IsNullOrWhiteSpace(b)
                => new DeepLinkAction(DeepLinkKind.UserProfile, b!.TrimStart('@'), raw),
            "chat" or "room" when !string.IsNullOrWhiteSpace(b)
                => new DeepLinkAction(DeepLinkKind.ChatRoom, b!, raw),
            "post" or "posts" when !string.IsNullOrWhiteSpace(b)
                => new DeepLinkAction(DeepLinkKind.Post, b!, raw),
            "settings" => new DeepLinkAction(DeepLinkKind.Settings, null, raw),
            "login" => new DeepLinkAction(DeepLinkKind.Login, null, raw),
            _ when a.StartsWith('@')
                => new DeepLinkAction(DeepLinkKind.UserProfile, a.TrimStart('@'), raw),
            _ => DeepLinkAction.Unknown(raw),
        };
    }
}

public enum DeepLinkKind
{
    Unknown = 0,
    QrLogin,
    UserProfile,
    ChatRoom,
    Post,
    Settings,
    Login,
}

public sealed record DeepLinkAction(DeepLinkKind Kind, string? Value, string? RawUri)
{
    public static DeepLinkAction Unknown(string? raw) => new(DeepLinkKind.Unknown, null, raw);
}
