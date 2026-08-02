using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using SolarWin.Services;

namespace SolarWin.Helpers;

public static class VideoPreviewHelper
{
    private const double OverlayMargin = 24;
    private const double CardPadding = 24;
    private const double DialogChromeHeight = 160;
    private const double MaxPlayerWidth = 1200;
    private const double MaxPlayerHeight = 760;

    private static readonly SemaphoreSlim DialogGate = new(1, 1);

    public static async Task<bool> ShowAsync(
        XamlRoot xamlRoot,
        string source,
        string? fileName,
        string? mimeType,
        VideoMediaCache cache)
    {
        if (!await DialogGate.WaitAsync(0).ConfigureAwait(true)) return false;
        MediaSource? mediaSource = null;
        MediaPlayer? player = null;
        try
        {
            var localPath = await cache.GetLocalVideoAsync(source, fileName, mimeType).ConfigureAwait(true);
            if (localPath is null) return false;

            var file = await StorageFile.GetFileFromPathAsync(localPath);
            var videoProperties = await file.Properties.GetVideoPropertiesAsync();
            var windowElement = App.Window.Content as FrameworkElement;
            var rootElement = windowElement ?? xamlRoot.Content as FrameworkElement;
            var rootWidth = rootElement?.ActualWidth is > 0 ? rootElement.ActualWidth : 1000;
            var rootHeight = rootElement?.ActualHeight is > 0 ? rootElement.ActualHeight : 760;
            var availableWidth = Math.Clamp(
                rootWidth - ((OverlayMargin + CardPadding) * 2),
                280,
                MaxPlayerWidth);
            var availableHeight = Math.Clamp(
                rootHeight - ((OverlayMargin * 2) + DialogChromeHeight),
                180,
                MaxPlayerHeight);
            var videoWidth = (double)videoProperties.Width;
            var videoHeight = (double)videoProperties.Height;
            if (videoProperties.Orientation is VideoOrientation.Rotate90 or VideoOrientation.Rotate270)
            {
                (videoWidth, videoHeight) = (videoHeight, videoWidth);
            }

            var aspectRatio = videoWidth > 0 && videoHeight > 0
                ? videoWidth / videoHeight
                : 16d / 9d;

            var playerWidth = availableWidth;
            var playerHeight = playerWidth / aspectRatio;
            if (playerHeight > availableHeight)
            {
                playerHeight = availableHeight;
                playerWidth = playerHeight * aspectRatio;
            }

            mediaSource = MediaSource.CreateFromStorageFile(file);
            player = new MediaPlayer
            {
                Source = mediaSource,
                AutoPlay = true,
                IsLoopingEnabled = false,
            };
            var view = new MediaPlayerElement
            {
                AreTransportControlsEnabled = true,
                Width = Math.Max(280, playerWidth),
                Height = Math.Max(180, playerHeight),
                MaxWidth = availableWidth,
                MaxHeight = availableHeight,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            view.SetMediaPlayer(player);
            // ContentDialog is hosted by an internal Popup which can inherit the
            // NavigationView content bounds. A window-root overlay uses the actual
            // client bounds and therefore stays centered when maximized.
            if (windowElement is Grid windowRoot)
            {
                await ShowWindowOverlayAsync(windowRoot, view, fileName, rootWidth, rootHeight);
            }
            else
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = windowElement?.XamlRoot ?? xamlRoot,
                    Title = string.IsNullOrWhiteSpace(fileName) ? "视频预览" : fileName,
                    Content = view,
                    CloseButtonText = "关闭",
                    DefaultButton = ContentDialogButton.None,
                    MaxWidth = Math.Min(rootWidth - 48, availableWidth + 64),
                };
                dialog.Resources["ContentDialogMaxWidth"] = Math.Min(rootWidth - 48, availableWidth + 64);
                await dialog.ShowAsync();
            }

            return true;
        }
        finally
        {
            player?.Pause();
            player?.Dispose();
            mediaSource?.Dispose();
            DialogGate.Release();
        }
    }

    private static async Task ShowWindowOverlayAsync(
        Grid windowRoot,
        MediaPlayerElement view,
        string? fileName,
        double rootWidth,
        double rootHeight)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeButton = new Button
        {
            Content = "关闭",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 40,
        };

        var content = new Grid { RowSpacing = 16 };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(fileName) ? "视频预览" : fileName,
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetRow(view, 1);
        content.Children.Add(view);
        Grid.SetRow(closeButton, 2);
        content.Children.Add(closeButton);

        var card = new Border
        {
            Width = Math.Min(rootWidth - (OverlayMargin * 2), view.Width + (CardPadding * 2)),
            Height = Math.Min(rootHeight - (OverlayMargin * 2), view.Height + DialogChromeHeight),
            MaxWidth = Math.Max(280, rootWidth - (OverlayMargin * 2)),
            MaxHeight = Math.Max(300, rootHeight - (OverlayMargin * 2)),
            Padding = new Thickness(CardPadding),
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 250, 250, 250)),
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var overlay = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(112, 0, 0, 0)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsTabStop = true,
        };
        overlay.Children.Add(card);

        void Close()
        {
            if (windowRoot.Children.Contains(overlay))
            {
                windowRoot.Children.Remove(overlay);
            }

            completion.TrySetResult();
        }

        closeButton.Click += (_, _) => Close();
        overlay.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Escape)
            {
                args.Handled = true;
                Close();
            }
        };

        windowRoot.Children.Add(overlay);
        overlay.Focus(FocusState.Programmatic);
        await completion.Task;
    }
}
