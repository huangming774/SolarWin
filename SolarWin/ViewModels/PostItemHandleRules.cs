namespace SolarWin.ViewModels;

/// <summary>Pure helpers for post author identity (no WinUI dependency).</summary>
public static class PostItemHandleRules
{
    /// <summary>
    /// Account/publisher handles are latin slug-like. Chinese nicknames must not hit /stargate/accounts/{name}.
    /// </summary>
    public static bool LooksLikeAccountHandle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var s = value.Trim().TrimStart('@');
        if (s.Length is < 1 or > 64)
        {
            return false;
        }

        // Allow letters, digits, underscore, hyphen, period only (no CJK / spaces).
        foreach (var ch in s)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' or '.')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
