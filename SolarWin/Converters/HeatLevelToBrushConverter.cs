using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace SolarWin.Converters;

public sealed class HeatLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var intensity = value is double number ? Math.Clamp(number, 0, 1) : 0;
        var theme = parameter is ElementTheme elementTheme ? elementTheme : ElementTheme.Default;
        var dark = theme == ElementTheme.Dark
                   || (theme == ElementTheme.Default
                       && Application.Current.RequestedTheme == ApplicationTheme.Dark);
        var low = dark ? Color.FromArgb(255, 46, 48, 54) : Color.FromArgb(255, 237, 241, 246);
        var accent = ResolveAccent();
        // Keep zero cells visible while making non-zero cells quickly distinguishable.
        var amount = intensity <= 0 ? 0 : 0.18 + intensity * 0.82;
        return new SolidColorBrush(Blend(low, accent, amount));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    private static Color ResolveAccent()
    {
        try
        {
            if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var value)
                && value is Color color)
            {
                return color;
            }
        }
        catch
        {
            // fall back below
        }

        return Color.FromArgb(255, 54, 123, 245);
    }

    private static Color Blend(Color from, Color to, double amount)
        => Color.FromArgb(
            255,
            (byte)Math.Round(from.R + (to.R - from.R) * amount),
            (byte)Math.Round(from.G + (to.G - from.G) * amount),
            (byte)Math.Round(from.B + (to.B - from.B) * amount));
}
