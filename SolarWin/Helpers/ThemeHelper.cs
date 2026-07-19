using Microsoft.UI.Xaml;
using Windows.Storage;

namespace SolarWin.Helpers;

/// <summary>Persist and apply light/dark/system theme.</summary>
public static class ThemeHelper
{
    private const string SettingsKey = "AppTheme";

    public static ElementTheme GetSavedTheme()
    {
        var values = ApplicationData.Current.LocalSettings.Values;
        if (values.TryGetValue(SettingsKey, out var raw) && raw is string s
            && Enum.TryParse<ElementTheme>(s, ignoreCase: true, out var theme))
        {
            return theme;
        }

        return ElementTheme.Default;
    }

    public static void SaveTheme(ElementTheme theme)
    {
        ApplicationData.Current.LocalSettings.Values[SettingsKey] = theme.ToString();
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
