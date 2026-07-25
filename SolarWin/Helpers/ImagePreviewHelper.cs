using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Services;

namespace SolarWin.Helpers;

/// <summary>Shared lightbox for post / chat images (pinch/scroll zoom).</summary>
public static class ImagePreviewHelper
{
    public static async Task ShowAsync(
        XamlRoot? xamlRoot,
        ImageSource? image,
        string title = "图片预览",
        string? fullResUrl = null,
        DysonFileImageLoader? imageLoader = null)
    {
        if (xamlRoot is null || image is null)
        {
            return;
        }

        var preview = new Image
        {
            Source = image,
            Stretch = Stretch.Uniform,
            MaxWidth = 1400,
            MaxHeight = 1000,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = preview,
                HorizontalScrollMode = ScrollMode.Auto,
                VerticalScrollMode = ScrollMode.Auto,
                ZoomMode = ZoomMode.Enabled,
                MinZoomFactor = 0.25f,
                MaxZoomFactor = 5f,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            },
            CloseButtonText = "关闭",
            XamlRoot = xamlRoot,
        };

        // Upgrade to full-res while the dialog is open (feed may only have a thumbnail).
        if (!string.IsNullOrWhiteSpace(fullResUrl) && imageLoader is not null)
        {
            _ = UpgradeToFullResAsync(preview, fullResUrl, imageLoader);
        }

        await dialog.ShowAsync();
    }

    private static async Task UpgradeToFullResAsync(
        Image preview,
        string fullResUrl,
        DysonFileImageLoader imageLoader)
    {
        try
        {
            var full = await imageLoader
                .LoadSafeAsync(fullResUrl, DysonFileImageLoader.DetailImageDecodeWidth)
                .ConfigureAwait(true);
            if (full is not null)
            {
                preview.Source = full;
            }
        }
        catch
        {
            // Keep the thumbnail already shown.
        }
    }
}
