using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace SolarWin.Helpers;

/// <summary>App wallpaper: image path, opacity, blur strength, glass / frosted overlay.</summary>
public enum WallpaperEffectMode
{
    /// <summary>Image only (opacity applies).</summary>
    None = 0,

    /// <summary>Liquid glass — translucent acrylic, more of the wallpaper shows through.</summary>
    Glass = 1,

    /// <summary>Frosted / matte — denser tint, softer wash over the image.</summary>
    Frosted = 2,
}

/// <summary>Persist + apply custom wallpaper behind shell content.</summary>
public static class WallpaperHelper
{
    private const string PathKey = "WallpaperPath";
    private const string OpacityKey = "WallpaperOpacity";
    private const string BlurKey = "WallpaperBlur";
    private const string EffectKey = "WallpaperEffect";
    private const string EnabledKey = "WallpaperEnabled";

    public static event EventHandler? Changed;

    public static string WallpaperDirectory => Path.Combine(AppPaths.RootDirectory, "wallpaper");

    public static bool IsEnabled
    {
        get => SettingsStore.GetString(EnabledKey) is "1";
        set => SettingsStore.SetString(EnabledKey, value ? "1" : "0");
    }

    public static string? ImagePath
    {
        get => SettingsStore.GetString(PathKey);
        set => SettingsStore.SetString(PathKey, value ?? string.Empty);
    }

    /// <summary>Wallpaper image opacity 0–100 (default 100).</summary>
    public static double OpacityPercent
    {
        get => ClampPercent(ParseDouble(SettingsStore.GetString(OpacityKey), 100));
        set => SettingsStore.SetString(OpacityKey, ClampPercent(value).ToString("0"));
    }

    /// <summary>Blur / frost strength 0–100 (default 40).</summary>
    public static double BlurPercent
    {
        get => ClampPercent(ParseDouble(SettingsStore.GetString(BlurKey), 40));
        set => SettingsStore.SetString(BlurKey, ClampPercent(value).ToString("0"));
    }

    public static WallpaperEffectMode EffectMode
    {
        get
        {
            var raw = SettingsStore.GetString(EffectKey);
            if (int.TryParse(raw, out var n) && Enum.IsDefined(typeof(WallpaperEffectMode), n))
            {
                return (WallpaperEffectMode)n;
            }

            return WallpaperEffectMode.Glass;
        }
        set => SettingsStore.SetString(EffectKey, ((int)value).ToString());
    }

    public static bool HasImage
    {
        get
        {
            var p = ImagePath;
            return !string.IsNullOrWhiteSpace(p) && File.Exists(p);
        }
    }

    public static void NotifyChanged() => Changed?.Invoke(null, EventArgs.Empty);

    /// <summary>
    /// Copy a user-picked file into the app data folder and enable wallpaper.
    /// </summary>
    public static string InstallImage(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("找不到所选图片", sourcePath);
        }

        Directory.CreateDirectory(WallpaperDirectory);
        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 8)
        {
            ext = ".jpg";
        }

        // Single active wallpaper file; wipe previous variants.
        try
        {
            foreach (var old in Directory.EnumerateFiles(WallpaperDirectory, "current.*"))
            {
                try
                {
                    File.Delete(old);
                }
                catch
                {
                    // ignore locked
                }
            }
        }
        catch
        {
            // ignore
        }

        var dest = Path.Combine(WallpaperDirectory, "current" + ext.ToLowerInvariant());
        File.Copy(sourcePath, dest, overwrite: true);
        ImagePath = dest;
        IsEnabled = true;
        NotifyChanged();
        return dest;
    }

    public static void ClearImage()
    {
        IsEnabled = false;
        var p = ImagePath;
        ImagePath = string.Empty;
        try
        {
            if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
            {
                File.Delete(p);
            }
        }
        catch
        {
            // ignore
        }

        NotifyChanged();
    }

    /// <summary>
    /// Paint wallpaper layers on the main window host.
    /// <paramref name="image"/> is the full-bleed picture;
    /// <paramref name="effectOverlay"/> is glass/frost acrylic;
    /// <paramref name="blurWash"/> is an extra soft wash driven by the blur slider.
    /// </summary>
    public static void ApplyToLayers(
        Image? image,
        Border? effectOverlay,
        Border? blurWash,
        Panel? contentRoot,
        Window? window)
    {
        var enabled = IsEnabled && HasImage;

        if (image is not null)
        {
            if (enabled)
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bmp.UriSource = new Uri(ImagePath!, UriKind.Absolute);
                    image.Source = bmp;
                    image.Opacity = OpacityPercent / 100.0;
                    image.Visibility = Visibility.Visible;
                }
                catch
                {
                    image.Source = null;
                    image.Visibility = Visibility.Collapsed;
                    enabled = false;
                }
            }
            else
            {
                image.Source = null;
                image.Visibility = Visibility.Collapsed;
            }
        }

        var blur = BlurPercent / 100.0;
        var mode = EffectMode;
        var isDark = contentRoot?.ActualTheme == ElementTheme.Dark
                     || (contentRoot?.ActualTheme == ElementTheme.Default
                         && Application.Current.RequestedTheme == ApplicationTheme.Dark);

        if (blurWash is not null)
        {
            if (enabled && blur > 0.01)
            {
                // Soft desaturating wash simulates “blur strength” without Win2D.
                var alpha = (byte)Math.Clamp((int)(blur * (mode == WallpaperEffectMode.Frosted ? 140 : 90)), 0, 180);
                var c = isDark
                    ? Color.FromArgb(alpha, 20, 24, 32)
                    : Color.FromArgb(alpha, 255, 255, 255);
                blurWash.Background = new SolidColorBrush(c);
                blurWash.Visibility = Visibility.Visible;
            }
            else
            {
                blurWash.Background = null;
                blurWash.Visibility = Visibility.Collapsed;
            }
        }

        if (effectOverlay is not null)
        {
            if (enabled && mode is WallpaperEffectMode.Glass or WallpaperEffectMode.Frosted)
            {
                effectOverlay.Background = CreateEffectBrush(mode, blur, isDark);
                effectOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                effectOverlay.Background = null;
                effectOverlay.Visibility = Visibility.Collapsed;
            }
        }

        // When wallpaper is active, drop Mica so the custom image is visible.
        if (window is not null)
        {
            try
            {
                window.SystemBackdrop = enabled
                    ? null
                    : new MicaBackdrop();
            }
            catch
            {
                // SystemBackdrop optional on some hosts
            }
        }

        if (contentRoot is not null)
        {
            contentRoot.Background = enabled
                ? new SolidColorBrush(Colors.Transparent)
                : null;
        }
    }

    public static Brush CreateEffectBrush(WallpaperEffectMode mode, double blur01, bool isDark)
    {
        blur01 = Math.Clamp(blur01, 0, 1);
        try
        {
            if (mode == WallpaperEffectMode.Glass)
            {
                // Liquid glass: lighter tint, more wallpaper, luminosity for refraction feel.
                var tint = isDark
                    ? Color.FromArgb(0x55, 0x30, 0x38, 0x50)
                    : Color.FromArgb(0x50, 0xE8, 0xF0, 0xFF);
                var fallback = isDark
                    ? Color.FromArgb(0x66, 0x18, 0x1C, 0x28)
                    : Color.FromArgb(0x55, 0xF0, 0xF4, 0xFA);
                return new AcrylicBrush
                {
                    TintColor = tint,
                    TintOpacity = 0.18 + blur01 * 0.28,
                    TintLuminosityOpacity = 0.35 + blur01 * 0.35,
                    FallbackColor = fallback,
                };
            }

            // Frosted / matte: denser milky layer.
            var frostTint = isDark
                ? Color.FromArgb(0x90, 0x22, 0x26, 0x32)
                : Color.FromArgb(0xA0, 0xF5, 0xF5, 0xF7);
            var frostFallback = isDark
                ? Color.FromArgb(0xB0, 0x1A, 0x1A, 0x1E)
                : Color.FromArgb(0xB8, 0xF2, 0xF2, 0xF4);
            return new AcrylicBrush
            {
                TintColor = frostTint,
                TintOpacity = 0.42 + blur01 * 0.40,
                TintLuminosityOpacity = 0.55 + blur01 * 0.30,
                FallbackColor = frostFallback,
            };
        }
        catch
        {
            var a = (byte)(mode == WallpaperEffectMode.Frosted
                ? 0x90 + (int)(blur01 * 40)
                : 0x50 + (int)(blur01 * 40));
            return new SolidColorBrush(isDark
                ? Color.FromArgb(a, 20, 24, 32)
                : Color.FromArgb(a, 250, 250, 252));
        }
    }

    private static double ParseDouble(string? raw, double fallback)
        => double.TryParse(raw, out var v) ? v : fallback;

    private static double ClampPercent(double v) => Math.Clamp(v, 0, 100);
}
