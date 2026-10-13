using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Services;
using Windows.Foundation;
using Windows.UI;

namespace SolarWin.Helpers;

/// <summary>
/// Shared lightbox for post / chat images (pinch/scroll zoom).
/// <para>
/// ContentDialog cannot host <c>CanvasControl</c> reliably (blank surface).
/// GPU path: load <see cref="CanvasBitmap"/> via <see cref="DysonFileImageLoader.LoadImageAsync"/>,
/// blit into a <see cref="CanvasImageSource"/> (valid XAML <see cref="ImageSource"/>), show with
/// a normal <see cref="Image"/>. That keeps decode/cache on the shared Win2D device / VRAM LRU.
/// </para>
/// </summary>
public static class ImagePreviewHelper
{
    private const double MaxDisplayWidth = 1200;
    private const double MaxDisplayHeight = 900;
    private const double WheelZoomStep = 1.15;
    private const double OverlayMargin = 24;
    private const double PreviewPadding = 24;
    private const double PreviewChromeHeight = 160;

    /// <summary>
    /// Keep a single image preview open, including the ContentDialog fallback.
    /// </summary>
    private static readonly SemaphoreSlim DialogGate = new(1, 1);

    public static Task ShowAsync(
        XamlRoot? xamlRoot,
        ImageSource? image,
        string title = "图片预览",
        string? fullResUrl = null,
        DysonFileImageLoader? imageLoader = null)
        => ShowExclusiveAsync(xamlRoot, image, title, fullResUrl, imageLoader);

    /// <summary>Open a lightbox from a URL (file id or absolute URL) using the GPU cache when possible.</summary>
    public static async Task ShowAsync(
        XamlRoot? xamlRoot,
        string? imageUrl,
        string title = "图片预览",
        string? fullResUrl = null,
        DysonFileImageLoader? imageLoader = null)
    {
        if (xamlRoot is null)
        {
            return;
        }

        var primary = !string.IsNullOrWhiteSpace(fullResUrl) ? fullResUrl! : imageUrl;
        var seed = !string.IsNullOrWhiteSpace(imageUrl) ? imageUrl! : primary;
        if (string.IsNullOrWhiteSpace(primary) && string.IsNullOrWhiteSpace(seed))
        {
            return;
        }

        if (!await DialogGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            if (imageLoader is not null)
            {
                var ok = await TryShowGpuDialogAsync(
                        xamlRoot,
                        primary ?? seed!,
                        seed,
                        title,
                        imageLoader)
                    .ConfigureAwait(true);
                if (ok)
                {
                    return;
                }
            }

            // Fallback: legacy BitmapImage (private drive files still go through the loader).
            if (imageLoader is not null)
            {
                await ShowLegacyUrlDialogAsync(xamlRoot, seed ?? primary!, primary, title, imageLoader)
                    .ConfigureAwait(true);
            }
        }
        finally
        {
            DialogGate.Release();
        }
    }

    private static async Task ShowExclusiveAsync(
        XamlRoot? xamlRoot,
        ImageSource? image,
        string title,
        string? fullResUrl,
        DysonFileImageLoader? imageLoader)
    {
        if (xamlRoot is null)
        {
            return;
        }

        if (!await DialogGate.WaitAsync(0).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(fullResUrl) && imageLoader is not null)
            {
                var ok = await TryShowGpuDialogAsync(
                        xamlRoot,
                        fullResUrl,
                        seedUrl: fullResUrl,
                        title,
                        imageLoader)
                    .ConfigureAwait(true);
                if (ok)
                {
                    return;
                }

                await ShowLegacyUrlDialogAsync(xamlRoot, fullResUrl, fullResUrl, title, imageLoader)
                    .ConfigureAwait(true);
                return;
            }

            if (image is null)
            {
                return;
            }

            await ShowImageDialogAsync(xamlRoot, image, title).ConfigureAwait(true);
        }
        finally
        {
            DialogGate.Release();
        }
    }

    /// <summary>
    /// Load CanvasBitmap from VRAM cache / GPU upload, blit to CanvasImageSource, host with Image.
    /// Returns false if GPU path failed (caller may fall back to BitmapImage).
    /// </summary>
    private static async Task<bool> TryShowGpuDialogAsync(
        XamlRoot xamlRoot,
        string primaryUrl,
        string? seedUrl,
        string title,
        DysonFileImageLoader imageLoader)
    {
        DysonFileImageLoader.CanvasBitmapLease? lease = null;
        CanvasImageSource? imageSource = null;
        try
        {
            // Prefer detail decode for lightbox; fall back to feed-width if detail fails.
            lease = await imageLoader
                .LoadImageAsync(primaryUrl, DysonFileImageLoader.DetailImageDecodeWidth)
                .ConfigureAwait(true);

            if ((lease is null || !lease.TryGetBitmap(out var bmp) || bmp is null)
                && !string.IsNullOrWhiteSpace(seedUrl)
                && !string.Equals(seedUrl, primaryUrl, StringComparison.OrdinalIgnoreCase))
            {
                lease?.Dispose();
                lease = await imageLoader
                    .LoadImageAsync(seedUrl, DysonFileImageLoader.FeedImageDecodeWidth)
                    .ConfigureAwait(true);
            }

            if (lease is null || !lease.TryGetBitmap(out bmp) || bmp is null)
            {
                return false;
            }

            imageSource = CreateImageSourceFromBitmap(imageLoader.Device, bmp);
            if (imageSource is null)
            {
                return false;
            }

            var preview = new Image
            {
                Source = imageSource,
                Stretch = Stretch.Uniform,
                Width = imageSource.Size.Width,
                Height = imageSource.Size.Height,
                MaxWidth = MaxDisplayWidth,
                MaxHeight = MaxDisplayHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            await ShowZoomPreviewAsync(xamlRoot, title, preview);

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            // Every exit path must release the lease: the LRU never evicts entries with
            // an outstanding lease, so an abandoned lease pins the CanvasBitmap in VRAM
            // for the rest of the session. The CanvasImageSource has no Dispose on
            // WinUI 3 (finalization reclaims it), but it holds its own GPU copy, so
            // releasing the cache lease is safe once the dialog is done.
            lease?.Dispose();
        }
    }

    private static CanvasImageSource? CreateImageSourceFromBitmap(CanvasDevice device, CanvasBitmap bitmap)
    {
        try
        {
            var pixelW = (double)bitmap.SizeInPixels.Width;
            var pixelH = (double)bitmap.SizeInPixels.Height;
            if (pixelW <= 0 || pixelH <= 0)
            {
                return null;
            }

            var scale = Math.Min(1d, Math.Min(MaxDisplayWidth / pixelW, MaxDisplayHeight / pixelH));
            var dispW = Math.Max(1f, (float)Math.Round(pixelW * scale));
            var dispH = Math.Max(1f, (float)Math.Round(pixelH * scale));

            // CanvasImageSource is a normal XAML ImageSource — works inside ContentDialog.
            var imageSource = new CanvasImageSource(device, dispW, dispH, 96);
            using (var session = imageSource.CreateDrawingSession(Color.FromArgb(0, 0, 0, 0)))
            {
                session.DrawImage(
                    bitmap,
                    new Rect(0, 0, dispW, dispH),
                    new Rect(0, 0, pixelW, pixelH));
            }

            return imageSource;
        }
        catch
        {
            return null;
        }
    }

    private static async Task ShowLegacyUrlDialogAsync(
        XamlRoot xamlRoot,
        string seedUrl,
        string? fullResUrl,
        string title,
        DysonFileImageLoader imageLoader)
    {
        BitmapImage? seed = null;
        try
        {
            seed = await imageLoader
                .LoadSafeAsync(seedUrl, DysonFileImageLoader.FeedImageDecodeWidth)
                .ConfigureAwait(true);
        }
        catch
        {
        }

        if (seed is null
            && !string.IsNullOrWhiteSpace(fullResUrl)
            && !string.Equals(fullResUrl, seedUrl, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                seed = await imageLoader
                    .LoadSafeAsync(fullResUrl, DysonFileImageLoader.DetailImageDecodeWidth)
                    .ConfigureAwait(true);
            }
            catch
            {
            }
        }

        if (seed is null && string.IsNullOrWhiteSpace(fullResUrl))
        {
            return;
        }

        var preview = new Image
        {
            Source = seed,
            Stretch = Stretch.Uniform,
            MaxWidth = MaxDisplayWidth,
            MaxHeight = MaxDisplayHeight,
            MinWidth = 200,
            MinHeight = 160,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (!string.IsNullOrWhiteSpace(fullResUrl))
        {
            _ = UpgradeLegacyAsync(preview, fullResUrl, imageLoader);
        }

        await ShowZoomPreviewAsync(xamlRoot, title, preview);
    }

    private static Task ShowImageDialogAsync(XamlRoot xamlRoot, ImageSource image, string title)
    {
        var preview = new Image
        {
            Source = image,
            Stretch = Stretch.Uniform,
            MaxWidth = MaxDisplayWidth,
            MaxHeight = MaxDisplayHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        return ShowZoomPreviewAsync(xamlRoot, title, preview);
    }

    private static async Task ShowZoomPreviewAsync(XamlRoot xamlRoot, string title, Image preview)
    {
        var viewer = new ScrollViewer
        {
            Content = preview,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollMode = ScrollMode.Auto,
            ZoomMode = ZoomMode.Enabled,
            MinZoomFactor = 0.25f,
            MaxZoomFactor = 5f,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        // ScrollViewer handles wheel input internally, so opt into already-handled events.
        var wheelHandler = new PointerEventHandler(ZoomViewer_OnPointerWheelChanged);
        viewer.AddHandler(UIElement.PointerWheelChangedEvent, wheelHandler, handledEventsToo: true);

        void UpdateLayout()
        {
            var availableWidth = Math.Max(1, Math.Min(MaxDisplayWidth,
                xamlRoot.Size.Width - 2 * (OverlayMargin + PreviewPadding)));
            var availableHeight = Math.Max(1, Math.Min(MaxDisplayHeight,
                xamlRoot.Size.Height - 2 * OverlayMargin - PreviewChromeHeight));
            var (width, height) = preview.Source switch
            {
                CanvasImageSource canvas => (canvas.Size.Width, canvas.Size.Height),
                BitmapSource bitmap => ((double)bitmap.PixelWidth, (double)bitmap.PixelHeight),
                _ => (0d, 0d),
            };
            var size = ImagePreviewLayout.Fit(width, height, availableWidth, availableHeight);
            preview.MinWidth = 0;
            preview.MinHeight = 0;
            preview.Width = size.Width;
            preview.Height = size.Height;
            viewer.Width = Math.Min(availableWidth, Math.Max(200, size.Width));
            viewer.Height = Math.Min(availableHeight, Math.Max(160, size.Height));
        }

        void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateLayout();
        void OnImageOpened(object sender, RoutedEventArgs args) => UpdateLayout();

        var sourceToken = preview.RegisterPropertyChangedCallback(Image.SourceProperty, (_, _) => UpdateLayout());
        xamlRoot.Changed += OnRootChanged;
        preview.ImageOpened += OnImageOpened;
        try
        {
            UpdateLayout();
            // Use the window client area, not the NavigationView's popup placement bounds.
            if (xamlRoot.Content is Grid windowRoot)
            {
                await ShowWindowOverlayAsync(windowRoot, viewer, title);
            }
            else
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = viewer,
                    CloseButtonText = "关闭",
                    XamlRoot = xamlRoot,
                };
                dialog.Resources["ContentDialogMaxWidth"] = MaxDisplayWidth + 2 * PreviewPadding;
                await dialog.ShowAsync();
            }
        }
        finally
        {
            xamlRoot.Changed -= OnRootChanged;
            preview.ImageOpened -= OnImageOpened;
            preview.UnregisterPropertyChangedCallback(Image.SourceProperty, sourceToken);
            viewer.RemoveHandler(UIElement.PointerWheelChangedEvent, wheelHandler);
        }
    }

    private static async Task ShowWindowOverlayAsync(Grid windowRoot, ScrollViewer viewer, string title)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var previousFocus = FocusManager.GetFocusedElement(windowRoot.XamlRoot) as Control;
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
            Text = title,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
        });
        Grid.SetRow(viewer, 1);
        content.Children.Add(viewer);
        Grid.SetRow(closeButton, 2);
        content.Children.Add(closeButton);

        var card = new Border
        {
            Padding = new Thickness(PreviewPadding),
            CornerRadius = new CornerRadius(8),
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var overlay = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(112, 0, 0, 0)),
            TabFocusNavigation = KeyboardNavigationMode.Cycle,
        };
        overlay.Children.Add(card);
        Grid.SetRowSpan(overlay, Math.Max(1, windowRoot.RowDefinitions.Count));
        Grid.SetColumnSpan(overlay, Math.Max(1, windowRoot.ColumnDefinitions.Count));

        void UpdateCard()
        {
            card.Width = viewer.Width + 2 * PreviewPadding;
            card.Background = new SolidColorBrush(windowRoot.ActualTheme == ElementTheme.Dark
                ? Color.FromArgb(255, 32, 32, 32)
                : Color.FromArgb(255, 250, 250, 250));
        }
        void OnViewerSizeChanged(object sender, SizeChangedEventArgs args) => UpdateCard();
        void OnThemeChanged(FrameworkElement sender, object args) => UpdateCard();
        void OnUnloaded(object sender, RoutedEventArgs args) => completion.TrySetResult();
        void OnKeyDown(object sender, KeyRoutedEventArgs args)
        {
            if (args.Key == Windows.System.VirtualKey.Escape)
            {
                args.Handled = true;
                completion.TrySetResult();
            }
        }
        void OnClose(object sender, RoutedEventArgs args) => completion.TrySetResult();

        viewer.SizeChanged += OnViewerSizeChanged;
        windowRoot.ActualThemeChanged += OnThemeChanged;
        overlay.Unloaded += OnUnloaded;
        overlay.KeyDown += OnKeyDown;
        closeButton.Click += OnClose;
        try
        {
            UpdateCard();
            windowRoot.Children.Add(overlay);
            closeButton.Focus(FocusState.Programmatic);
            await completion.Task;
        }
        finally
        {
            viewer.SizeChanged -= OnViewerSizeChanged;
            windowRoot.ActualThemeChanged -= OnThemeChanged;
            overlay.Unloaded -= OnUnloaded;
            overlay.KeyDown -= OnKeyDown;
            closeButton.Click -= OnClose;
            windowRoot.Children.Remove(overlay);
            previousFocus?.Focus(FocusState.Programmatic);
        }
    }

    private static void ZoomViewer_OnPointerWheelChanged(object sender, PointerRoutedEventArgs args)
    {
        if (sender is not ScrollViewer viewer)
        {
            return;
        }

        var wheelDelta = args.GetCurrentPoint(viewer).Properties.MouseWheelDelta;
        if (wheelDelta == 0)
        {
            return;
        }

        var oldZoom = Math.Max(viewer.MinZoomFactor, viewer.ZoomFactor);
        var zoomMultiplier = Math.Pow(WheelZoomStep, wheelDelta / 120d);
        var newZoom = (float)Math.Clamp(
            oldZoom * zoomMultiplier,
            viewer.MinZoomFactor,
            viewer.MaxZoomFactor);

        if (Math.Abs(newZoom - oldZoom) < 0.001f)
        {
            args.Handled = true;
            return;
        }

        var pointer = args.GetCurrentPoint(viewer).Position;
        if (viewer.Content is not FrameworkElement content)
        {
            return;
        }

        var horizontalOffset = ImagePreviewLayout.ZoomOffset(
            viewer.HorizontalOffset, pointer.X, viewer.ViewportWidth, content.ActualWidth, oldZoom, newZoom);
        var verticalOffset = ImagePreviewLayout.ZoomOffset(
            viewer.VerticalOffset, pointer.Y, viewer.ViewportHeight, content.ActualHeight, oldZoom, newZoom);

        viewer.ChangeView(horizontalOffset, verticalOffset, newZoom, disableAnimation: true);
        args.Handled = true;
    }

    private static async Task UpgradeLegacyAsync(
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
            // Keep seed.
        }
    }
}
