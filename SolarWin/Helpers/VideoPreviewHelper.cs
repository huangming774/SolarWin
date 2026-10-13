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
        Windows.Foundation.TypedEventHandler<MediaPlayer, MediaPlayerFailedEventArgs>? mediaFailedHandler = null;
        var mediaFailed = false;
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
            var videoWidth = (double)videoProperties.Width;
            var videoHeight = (double)videoProperties.Height;
            if (videoProperties.Orientation is VideoOrientation.Rotate90 or VideoOrientation.Rotate270)
            {
                (videoWidth, videoHeight) = (videoHeight, videoWidth);
            }

            var aspectRatio = videoWidth > 0 && videoHeight > 0
                ? videoWidth / videoHeight
                : 16d / 9d;
            var layout = VideoPreviewLayout.Calculate(rootWidth, rootHeight, aspectRatio);

            mediaSource = MediaSource.CreateFromStorageFile(file);
            player = new MediaPlayer
            {
                Source = mediaSource,
                AutoPlay = true,
                IsLoopingEnabled = false,
            };
            mediaFailedHandler = (_, _) => mediaFailed = true;
            player.MediaFailed += mediaFailedHandler;
            var view = new MediaPlayerElement
            {
                AreTransportControlsEnabled = true,
                Width = layout.PlayerWidth,
                Height = layout.PlayerHeight,
                MaxWidth = layout.AvailableWidth,
                MaxHeight = layout.AvailableHeight,
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
                if (!await ShowWindowOverlayAsync(windowRoot, view, player, fileName, rootWidth, rootHeight, aspectRatio))
                {
                    return false;
                }
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
                    MaxWidth = Math.Min(rootWidth - 48, layout.AvailableWidth + 64),
                };
                dialog.Resources["ContentDialogMaxWidth"] = Math.Min(rootWidth - 48, layout.AvailableWidth + 64);
                await dialog.ShowAsync();
            }

            return !mediaFailed;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            // Download, Media Foundation, and overlay failures are optional-preview failures.
            return false;
        }
        finally
        {
            if (player is not null && mediaFailedHandler is not null)
            {
                try
                {
                    player.MediaFailed -= mediaFailedHandler;
                }
                catch
                {
                }
            }

            try
            {
                player?.Pause();
            }
            catch
            {
            }

            try
            {
                player?.Dispose();
            }
            catch
            {
            }

            try
            {
                mediaSource?.Dispose();
            }
            catch
            {
            }

            DialogGate.Release();
        }
    }

    private static async Task<bool> ShowWindowOverlayAsync(
        Grid windowRoot,
        MediaPlayerElement view,
        MediaPlayer player,
        string? fileName,
        double rootWidth,
        double rootHeight,
        double aspectRatio)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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
            Width = Math.Min(rootWidth - (VideoPreviewLayout.OverlayMargin * 2), view.Width + (VideoPreviewLayout.CardPadding * 2)),
            Height = Math.Min(rootHeight - (VideoPreviewLayout.OverlayMargin * 2), view.Height + VideoPreviewLayout.DialogChromeHeight),
            MaxWidth = Math.Max(280, rootWidth - (VideoPreviewLayout.OverlayMargin * 2)),
            MaxHeight = Math.Max(300, rootHeight - (VideoPreviewLayout.OverlayMargin * 2)),
            Padding = new Thickness(VideoPreviewLayout.CardPadding),
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 250, 250, 250)),
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        void UpdateSize(double width, double height)
        {
            var layout = VideoPreviewLayout.Calculate(width, height, aspectRatio);
            view.Width = layout.PlayerWidth;
            view.Height = layout.PlayerHeight;
            view.MaxWidth = layout.AvailableWidth;
            view.MaxHeight = layout.AvailableHeight;
            card.Width = Math.Min(width - (VideoPreviewLayout.OverlayMargin * 2), view.Width + (VideoPreviewLayout.CardPadding * 2));
            card.Height = Math.Min(height - (VideoPreviewLayout.OverlayMargin * 2), view.Height + VideoPreviewLayout.DialogChromeHeight);
            card.MaxWidth = layout.CardMaxWidth;
            card.MaxHeight = layout.CardMaxHeight;
        }

        UpdateSize(rootWidth, rootHeight);

        var overlay = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(112, 0, 0, 0)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsTabStop = true,
        };
        overlay.Children.Add(card);

        var closed = false;
        void Close(bool success)
        {
            if (!closed)
            {
                closed = true;
                completion.TrySetResult(success);
            }
        }

        void OnRootSizeChanged(object sender, SizeChangedEventArgs args)
            => UpdateSize(args.NewSize.Width, args.NewSize.Height);

        void OnCloseButtonClick(object sender, RoutedEventArgs args)
            => Close(true);

        void OnRootUnloaded(object sender, RoutedEventArgs args)
            => Close(false);

        void OnOverlayKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (args.Key == Windows.System.VirtualKey.Escape)
            {
                args.Handled = true;
                Close(true);
            }
        }

        void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
            => Close(false);

        closeButton.Click += OnCloseButtonClick;
        windowRoot.SizeChanged += OnRootSizeChanged;
        windowRoot.Unloaded += OnRootUnloaded;
        overlay.KeyDown += OnOverlayKeyDown;
        player.MediaFailed += OnMediaFailed;

        try
        {
            windowRoot.Children.Add(overlay);
            overlay.Focus(FocusState.Programmatic);
            return await completion.Task.ConfigureAwait(true);
        }
        finally
        {
            closeButton.Click -= OnCloseButtonClick;
            windowRoot.SizeChanged -= OnRootSizeChanged;
            windowRoot.Unloaded -= OnRootUnloaded;
            overlay.KeyDown -= OnOverlayKeyDown;
            player.MediaFailed -= OnMediaFailed;
            if (windowRoot.Children.Contains(overlay))
            {
                windowRoot.Children.Remove(overlay);
            }

            completion.TrySetResult(false);
        }
    }
}
