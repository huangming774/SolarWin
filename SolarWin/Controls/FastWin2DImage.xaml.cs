using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using SolarWin.Helpers;
using Windows.Foundation;

namespace SolarWin.Controls;

/// <summary>
/// High-performance Win2D image control for <see cref="DysonFileImageLoader"/> GPU bitmaps.
/// <list type="bullet">
/// <item>Draws via <see cref="CanvasControl"/> on the shared <see cref="CanvasDevice"/>.</item>
/// <item>Acquires a <see cref="DysonFileImageLoader.CanvasBitmapLease"/> for <see cref="Source"/>.</item>
/// <item>Releases the lease on Unloaded / off-viewport / Source clear so VRAM can be disposed.</item>
/// <item>Optional gray placeholder + short fade-in when the bitmap arrives.</item>
/// </list>
/// Prefer this over legacy <c>Image</c> + <c>BitmapImage</c> on the posts pipeline.
/// </summary>
public sealed partial class FastWin2DImage : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(string),
        typeof(FastWin2DImage),
        new PropertyMetadata(null, OnImagePropertyChanged));

    public static readonly DependencyProperty FallbackSourceProperty = DependencyProperty.Register(
        nameof(FallbackSource),
        typeof(string),
        typeof(FastWin2DImage),
        new PropertyMetadata(null, OnImagePropertyChanged));

    /// <summary>Decode width in pixels (96 avatar / 640 feed / 1440 detail).</summary>
    public static readonly DependencyProperty DecodeWidthProperty = DependencyProperty.Register(
        nameof(DecodeWidth),
        typeof(int),
        typeof(FastWin2DImage),
        new PropertyMetadata(DysonFileImageLoader.FeedImageDecodeWidth, OnImagePropertyChanged));

    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
        nameof(Stretch),
        typeof(Stretch),
        typeof(FastWin2DImage),
        new PropertyMetadata(Stretch.Uniform, OnVisualPropertyChanged));

    public static readonly DependencyProperty ShowPlaceholderProperty = DependencyProperty.Register(
        nameof(ShowPlaceholder),
        typeof(bool),
        typeof(FastWin2DImage),
        new PropertyMetadata(true, OnVisualPropertyChanged));

    public static readonly DependencyProperty EnableFadeInProperty = DependencyProperty.Register(
        nameof(EnableFadeIn),
        typeof(bool),
        typeof(FastWin2DImage),
        new PropertyMetadata(true));

    public static readonly DependencyProperty FadeInDurationProperty = DependencyProperty.Register(
        nameof(FadeInDuration),
        typeof(TimeSpan),
        typeof(FastWin2DImage),
        new PropertyMetadata(TimeSpan.FromMilliseconds(180)));

    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading),
        typeof(bool),
        typeof(FastWin2DImage),
        new PropertyMetadata(false));

    /// <summary>
    /// When true (default), release the GPU lease while EffectiveViewport is empty (ListView recycle).
    /// Set false for ContentDialog / overlays where viewport is unreliable.
    /// </summary>
    public static readonly DependencyProperty PauseWhenOffscreenProperty = DependencyProperty.Register(
        nameof(PauseWhenOffscreen),
        typeof(bool),
        typeof(FastWin2DImage),
        new PropertyMetadata(true));

    private readonly DysonFileImageLoader _loader;
    private DysonFileImageLoader.CanvasBitmapLease? _lease;
    private CancellationTokenSource? _loadCancellation;
    private Storyboard? _fadeStoryboard;
    private bool _isInEffectiveViewport = true;
    private bool _isLoaded;
    private bool _hasBitmap;
    private double _naturalWidth;
    private double _naturalHeight;

    public FastWin2DImage()
    {
        InitializeComponent();
        _loader = App.Services.GetRequiredService<DysonFileImageLoader>();
        ImageCanvas.CustomDevice = _loader.Device;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        EffectiveViewportChanged += OnEffectiveViewportChanged;
        SizeChanged += OnSizeChanged;
        // Reuse Control.CornerRadius (no shadowing DP) for rounded / circular clip.
        RegisterPropertyChangedCallback(CornerRadiusProperty, OnCornerRadiusChanged);
        UpdatePlaceholderChrome();
    }

    /// <summary>Image URL or drive file id / cache key resolved by <see cref="DysonFileImageLoader"/>.</summary>
    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Optional file id / URL used when <see cref="Source"/> cannot be downloaded or decoded.</summary>
    public string? FallbackSource
    {
        get => (string?)GetValue(FallbackSourceProperty);
        set => SetValue(FallbackSourceProperty, value);
    }

    /// <summary>Decode width (px). Use <see cref="DysonFileImageLoader.AvatarDecodeWidth"/>, Feed, or Detail constants.</summary>
    public int DecodeWidth
    {
        get => (int)GetValue(DecodeWidthProperty);
        set => SetValue(DecodeWidthProperty, value);
    }

    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>Show a muted block while the bitmap is missing.</summary>
    public bool ShowPlaceholder
    {
        get => (bool)GetValue(ShowPlaceholderProperty);
        set => SetValue(ShowPlaceholderProperty, value);
    }

    /// <summary>Animate canvas opacity 0→1 when a new bitmap is ready.</summary>
    public bool EnableFadeIn
    {
        get => (bool)GetValue(EnableFadeInProperty);
        set => SetValue(EnableFadeInProperty, value);
    }

    public TimeSpan FadeInDuration
    {
        get => (TimeSpan)GetValue(FadeInDurationProperty);
        set => SetValue(FadeInDurationProperty, value);
    }

    /// <summary>True while a load is in flight (for ProgressRing overlays).</summary>
    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        private set => SetValue(IsLoadingProperty, value);
    }

    public bool PauseWhenOffscreen
    {
        get => (bool)GetValue(PauseWhenOffscreenProperty);
        set => SetValue(PauseWhenOffscreenProperty, value);
    }

    private static void OnImagePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((FastWin2DImage)sender).RestartLoad();

    private static void OnVisualPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (FastWin2DImage)sender;
        control.UpdatePlaceholderChrome();
        control.ImageCanvas.Invalidate();
    }

    private static void OnCornerRadiusChanged(DependencyObject sender, DependencyProperty property)
    {
        var control = (FastWin2DImage)sender;
        control.UpdatePlaceholderChrome();
        control.ImageCanvas.Invalidate();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        _loader.GpuCacheInvalidated += OnGpuCacheInvalidated;
        RestartLoad();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = false;
        _loader.GpuCacheInvalidated -= OnGpuCacheInvalidated;
        ReleaseImage(resetPlaceholder: true);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
        => UpdatePlaceholderChrome();

    private void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
    {
        if (!PauseWhenOffscreen)
        {
            // ContentDialog / zoom ScrollViewer often reports empty viewport — keep loading.
            if (!_isInEffectiveViewport)
            {
                _isInEffectiveViewport = true;
                RestartLoad();
            }

            return;
        }

        // ListView recycle / scroll-off: EffectiveViewport becomes empty.
        var viewport = args.EffectiveViewport;
        var visible = viewport.Width > 0 && viewport.Height > 0;
        if (_isInEffectiveViewport == visible)
        {
            return;
        }

        _isInEffectiveViewport = visible;
        if (visible)
        {
            RestartLoad();
        }
        else
        {
            // Drop GPU lease while recycled so LRU can dispose the CanvasBitmap.
            ReleaseImage(resetPlaceholder: true);
        }
    }

    private void OnGpuCacheInvalidated(object? sender, EventArgs args)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            RestartLoad();
        }
        else
        {
            DispatcherQueue.TryEnqueue(RestartLoad);
        }
    }

    private void RestartLoad()
    {
        ReleaseImage(resetPlaceholder: true);
        if (!_isLoaded
            || !_isInEffectiveViewport
            || string.IsNullOrWhiteSpace(Source)
            || DecodeWidth <= 0)
        {
            IsLoading = false;
            return;
        }

        IsLoading = true;
        _loadCancellation = new CancellationTokenSource();
        _ = LoadAsync(Source, FallbackSource, DecodeWidth, _loadCancellation);
    }

    private async Task LoadAsync(
        string source,
        string? fallbackSource,
        int decodeWidth,
        CancellationTokenSource request)
    {
        try
        {
            // Fast path: already in GPU LRU — acquire lease without network.
            if (_loader.TryAcquireCached(source, decodeWidth, out var cached) && cached is not null)
            {
                if (!CanApply(request, source, fallbackSource, decodeWidth))
                {
                    cached.Dispose();
                    return;
                }

                ApplyLease(cached, animate: false);
                return;
            }

            var lease = await _loader.LoadImageAsync(source, decodeWidth, request.Token).ConfigureAwait(true);
            if (lease is null
                && !string.IsNullOrWhiteSpace(fallbackSource)
                && !string.Equals(source, fallbackSource, StringComparison.OrdinalIgnoreCase))
            {
                if (!_loader.TryAcquireCached(fallbackSource, decodeWidth, out lease) || lease is null)
                {
                    lease = await _loader
                        .LoadImageAsync(fallbackSource, decodeWidth, request.Token)
                        .ConfigureAwait(true);
                }
            }

            if (lease is null)
            {
                if (ReferenceEquals(_loadCancellation, request))
                {
                    IsLoading = false;
                }

                return;
            }

            if (!CanApply(request, source, fallbackSource, decodeWidth))
            {
                lease.Dispose();
                return;
            }

            ApplyLease(lease, animate: EnableFadeIn);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, request))
            {
                _loadCancellation = null;
            }

            request.Dispose();
        }
    }

    private bool CanApply(
        CancellationTokenSource request,
        string source,
        string? fallbackSource,
        int decodeWidth)
        => !request.IsCancellationRequested
           && ReferenceEquals(_loadCancellation, request)
           && string.Equals(Source, source, StringComparison.Ordinal)
           && string.Equals(FallbackSource, fallbackSource, StringComparison.Ordinal)
           && DecodeWidth == decodeWidth
           && _isLoaded
           && _isInEffectiveViewport;

    private void ApplyLease(DysonFileImageLoader.CanvasBitmapLease lease, bool animate)
    {
        _lease = lease;
        _hasBitmap = lease.TryGetBitmap(out var bitmap) && bitmap is not null;
        if (_hasBitmap && bitmap is not null)
        {
            _naturalWidth = bitmap.SizeInPixels.Width;
            _naturalHeight = bitmap.SizeInPixels.Height;
            InvalidateMeasure();
        }

        IsLoading = false;
        UpdatePlaceholderChrome();
        ImageCanvas.Invalidate();

        if (animate)
        {
            StartFadeIn();
        }
        else
        {
            StopFade();
            ImageCanvas.Opacity = _hasBitmap ? 1 : 0;
        }
    }

    private void ReleaseImage(bool resetPlaceholder)
    {
        var cancellation = Interlocked.Exchange(ref _loadCancellation, null);
        if (cancellation is not null)
        {
            cancellation.Cancel();
        }

        Interlocked.Exchange(ref _lease, null)?.Dispose();
        _hasBitmap = false;
        _naturalWidth = 0;
        _naturalHeight = 0;
        IsLoading = false;
        StopFade();
        ImageCanvas.Opacity = 0;
        if (resetPlaceholder)
        {
            UpdatePlaceholderChrome();
        }

        InvalidateMeasure();
        ImageCanvas.Invalidate();
    }

    private void UpdatePlaceholderChrome()
    {
        Placeholder.CornerRadius = CornerRadius;
        var show = ShowPlaceholder
                   && !_hasBitmap
                   && !string.IsNullOrWhiteSpace(Source);
        Placeholder.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void StartFadeIn()
    {
        StopFade();
        ImageCanvas.Opacity = 0;
        var duration = FadeInDuration <= TimeSpan.Zero
            ? TimeSpan.FromMilliseconds(1)
            : FadeInDuration;
        var animation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(duration),
            EnableDependentAnimation = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(animation, ImageCanvas);
        Storyboard.SetTargetProperty(animation, "Opacity");
        _fadeStoryboard = new Storyboard();
        _fadeStoryboard.Children.Add(animation);
        _fadeStoryboard.Begin();
    }

    private void StopFade()
    {
        if (_fadeStoryboard is null)
        {
            return;
        }

        _fadeStoryboard.Stop();
        _fadeStoryboard.Children.Clear();
        _fadeStoryboard = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var hasExplicitWidth = !double.IsNaN(Width);
        var hasExplicitHeight = !double.IsNaN(Height);
        if (hasExplicitWidth && hasExplicitHeight)
        {
            var explicitSize = new Size(Width, Height);
            RootGrid.Measure(explicitSize);
            return explicitSize;
        }

        if (_naturalWidth <= 0 || _naturalHeight <= 0)
        {
            // ScrollViewer / ContentDialog pass infinite available size — never measure to 0×0.
            var fallbackW = !double.IsNaN(MinWidth) && MinWidth > 0 ? MinWidth : 200;
            var fallbackH = !double.IsNaN(MinHeight) && MinHeight > 0 ? MinHeight : 160;
            var w = hasExplicitWidth
                ? Width
                : double.IsInfinity(availableSize.Width) || availableSize.Width <= 0
                    ? fallbackW
                    : availableSize.Width;
            var h = hasExplicitHeight
                ? Height
                : double.IsInfinity(availableSize.Height) || availableSize.Height <= 0
                    ? fallbackH
                    : availableSize.Height;
            if (!double.IsNaN(MaxWidth) && !double.IsInfinity(MaxWidth))
            {
                w = Math.Min(w, MaxWidth);
            }

            if (!double.IsNaN(MaxHeight) && !double.IsInfinity(MaxHeight))
            {
                h = Math.Min(h, MaxHeight);
            }

            var placeholder = new Size(Math.Max(1, w), Math.Max(1, h));
            RootGrid.Measure(placeholder);
            return placeholder;
        }

        var maxW = availableSize.Width;
        if (!double.IsNaN(MaxWidth))
        {
            maxW = Math.Min(maxW, MaxWidth);
        }

        if (hasExplicitWidth)
        {
            maxW = Width;
        }

        var maxH = availableSize.Height;
        if (!double.IsNaN(MaxHeight))
        {
            maxH = Math.Min(maxH, MaxHeight);
        }

        if (hasExplicitHeight)
        {
            maxH = Height;
        }

        if (double.IsInfinity(maxW) || maxW <= 0)
        {
            maxW = _naturalWidth;
        }

        if (double.IsInfinity(maxH) || maxH <= 0)
        {
            maxH = _naturalHeight;
        }

        Size desired;
        if (Stretch == Stretch.Fill)
        {
            desired = new Size(maxW, maxH);
        }
        else if (Stretch == Stretch.None)
        {
            desired = new Size(
                Math.Min(_naturalWidth, maxW),
                Math.Min(_naturalHeight, maxH));
        }
        else
        {
            var scaleX = maxW / _naturalWidth;
            var scaleY = maxH / _naturalHeight;
            var scale = Stretch == Stretch.UniformToFill
                ? Math.Max(scaleX, scaleY)
                : Math.Min(scaleX, scaleY);
            if (Stretch == Stretch.Uniform
                && double.IsInfinity(availableSize.Width)
                && double.IsInfinity(availableSize.Height))
            {
                scale = Math.Min(1, scale);
            }

            desired = new Size(_naturalWidth * scale, _naturalHeight * scale);
        }

        if (hasExplicitWidth)
        {
            desired.Width = Width;
        }

        if (hasExplicitHeight)
        {
            desired.Height = Height;
        }

        RootGrid.Measure(desired);
        return desired;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        RootGrid.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        return finalSize;
    }

    private void ImageCanvas_OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var lease = _lease;
        if (lease is null || !lease.TryGetBitmap(out var bitmap) || bitmap is null)
        {
            return;
        }

        var destination = CalculateDestinationRect(
            bitmap.SizeInPixels.Width,
            bitmap.SizeInPixels.Height,
            sender.ActualWidth,
            sender.ActualHeight,
            Stretch);
        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return;
        }

        var clip = TryCreateClipGeometry(sender, CornerRadius);
        if (clip is not null)
        {
            using (clip)
            using (args.DrawingSession.CreateLayer(1f, clip))
            {
                args.DrawingSession.DrawImage(bitmap, destination);
            }

            return;
        }

        args.DrawingSession.DrawImage(bitmap, destination);
    }

    private static CanvasGeometry? TryCreateClipGeometry(CanvasControl sender, CornerRadius radius)
    {
        var w = (float)sender.ActualWidth;
        var h = (float)sender.ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return null;
        }

        var tl = (float)Math.Max(0, radius.TopLeft);
        var tr = (float)Math.Max(0, radius.TopRight);
        var br = (float)Math.Max(0, radius.BottomRight);
        var bl = (float)Math.Max(0, radius.BottomLeft);
        if (tl <= 0 && tr <= 0 && br <= 0 && bl <= 0)
        {
            return null;
        }

        // Uniform circular avatar: all corners ≈ half of the smaller side.
        var halfMin = Math.Min(w, h) / 2f;
        if (Math.Abs(tl - tr) < 0.5f
            && Math.Abs(tr - br) < 0.5f
            && Math.Abs(br - bl) < 0.5f
            && tl >= halfMin - 0.5f)
        {
            var center = new Vector2(w / 2f, h / 2f);
            return CanvasGeometry.CreateCircle(sender.Device, center, halfMin);
        }

        var maxR = halfMin;
        tl = Math.Min(tl, maxR);
        tr = Math.Min(tr, maxR);
        br = Math.Min(br, maxR);
        bl = Math.Min(bl, maxR);

        // Rounded rectangle path (independent corner radii approximated with a uniform radius
        // when corners differ slightly; use average for CreateRoundedRectangle).
        var uniform = (tl + tr + br + bl) / 4f;
        return CanvasGeometry.CreateRoundedRectangle(sender.Device, 0, 0, w, h, uniform, uniform);
    }

    internal static Rect CalculateDestinationRect(
        double sourceWidth,
        double sourceHeight,
        double targetWidth,
        double targetHeight,
        Stretch stretch)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
        {
            return Rect.Empty;
        }

        if (stretch == Stretch.Fill)
        {
            return new Rect(0, 0, targetWidth, targetHeight);
        }

        if (stretch == Stretch.None)
        {
            return new Rect(
                (targetWidth - sourceWidth) / 2,
                (targetHeight - sourceHeight) / 2,
                sourceWidth,
                sourceHeight);
        }

        var scaleX = targetWidth / sourceWidth;
        var scaleY = targetHeight / sourceHeight;
        var scale = stretch == Stretch.UniformToFill
            ? Math.Max(scaleX, scaleY)
            : Math.Min(scaleX, scaleY);
        var width = sourceWidth * scale;
        var height = sourceHeight * scale;
        return new Rect((targetWidth - width) / 2, (targetHeight - height) / 2, width, height);
    }
}
