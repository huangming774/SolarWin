using Microsoft.UI.Xaml;

namespace SolarWin.Helpers;

/// <summary>Persist and apply light/dark/system theme.</summary>
public static class ThemeHelper
{
    private const string SettingsKey = "AppTheme";

    public static ElementTheme GetSavedTheme()
    {
        var raw = SettingsStore.GetString(SettingsKey);
        if (raw is not null && Enum.TryParse<ElementTheme>(raw, ignoreCase: true, out var theme))
        {
            return theme;
        }

        return ElementTheme.Default;
    }

    public static void SaveTheme(ElementTheme theme)
    {
        SettingsStore.SetString(SettingsKey, theme.ToString());
    }

    public static void ApplyToWindow(Window? window, ElementTheme theme)
    {
        if (window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme;
        }
    }

    public static string ToDisplayName(ElementTheme theme) => theme switch
    {
        ElementTheme.Light => "浅色",
        ElementTheme.Dark => "深色",
        _ => "跟随系统",
    };
}
