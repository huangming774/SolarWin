using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SolarWin.Helpers;
using Windows.Foundation;

namespace SolarWin.Controls;

/// <summary>
/// Win2D-backed image control for the posts pipeline.
/// Loads a <see cref="DysonFileImageLoader.CanvasBitmapLease"/> for <see cref="Source"/>,
/// draws via <see cref="CanvasControl"/>, and releases the lease when unloaded or scrolled off-screen.
/// Chat / profile pages should keep using BitmapImage + PersonPicture.
/// </summary>
public sealed partial class GpuImage : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(string),
        typeof(GpuImage),
        new PropertyMetadata(null, OnImagePropertyChanged));

    public static readonly DependencyProperty DecodePixelWidthProperty = DependencyProperty.Register(
        nameof(DecodePixelWidth),
        typeof(int),
        typeof(GpuImage),
        new PropertyMetadata(DysonFileImageLoader.FeedImageDecodeWidth, OnImagePropertyChanged));

    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
        nameof(Stretch),
        typeof(Stretch),
        typeof(GpuImage),
        new PropertyMetadata(Stretch.Uniform, OnVisualPropertyChanged));

    public static readonly DependencyProperty IsCircularProperty = DependencyProperty.Register(
        nameof(IsCircular),
        typeof(bool),
        typeof(GpuImage),
        new PropertyMetadata(false, OnVisualPropertyChanged));

    private readonly DysonFileImageLoader _loader;
    private DysonFileImageLoader.CanvasBitmapLease? _lease;
    private CancellationTokenSource? _loadCancellation;
    private bool _isInEffectiveViewport = true;
    private bool _isLoaded;
    private double _naturalWidth;
    private double _naturalHeight;

    public GpuImage()
    {
        InitializeComponent();
        _loader = App.Services.GetRequiredService<DysonFileImageLoader>();
        ImageCanvas.CustomDevice = _loader.Device;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public int DecodePixelWidth
    {
        get => (int)GetValue(DecodePixelWidthProperty);
        set => SetValue(DecodePixelWidthProperty, value);
    }

    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public bool IsCircular
    {
        get => (bool)GetValue(IsCircularProperty);
        set => SetValue(IsCircularProperty, value);
    }

    private static void OnImagePropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((GpuImage)sender).RestartLoad();

    private static void OnVisualPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((GpuImage)sender).ImageCanvas.Invalidate();

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
        ReleaseImage();
    }

    private void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
    {
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
            ReleaseImage();
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
        ReleaseImage();
        if (!_isLoaded
            || !_isInEffectiveViewport
            || string.IsNullOrWhiteSpace(Source)
            || DecodePixelWidth <= 0)
        {
            return;
        }

        _loadCancellation = new CancellationTokenSource();
        _ = LoadAsync(Source, DecodePixelWidth, _loadCancellation);
    }

    private async Task LoadAsync(string source, int decodeWidth, CancellationTokenSource request)
    {
        try
        {
            var lease = await _loader.LoadImageAsync(source, decodeWidth, request.Token).ConfigureAwait(true);
            if (lease is null)
            {
                return;
            }

            if (request.IsCancellationRequested
                || !ReferenceEquals(_loadCancellation, request)
                || !string.Equals(Source, source, StringComparison.Ordinal)
                || DecodePixelWidth != decodeWidth)
            {
                lease.Dispose();
                return;
            }

            _lease = lease;
            if (lease.TryGetBitmap(out var bitmap) && bitmap is not null)
            {
                _naturalWidth = bitmap.SizeInPixels.Width;
                _naturalHeight = bitmap.SizeInPixels.Height;
                InvalidateMeasure();
            }

            ImageCanvas.Invalidate();
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

    private void ReleaseImage()
    {
        var cancellation = Interlocked.Exchange(ref _loadCancellation, null);
        if (cancellation is not null)
        {
            cancellation.Cancel();
        }

        Interlocked.Exchange(ref _lease, null)?.Dispose();
        _naturalWidth = 0;
        _naturalHeight = 0;
        InvalidateMeasure();
        ImageCanvas.Invalidate();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Explicit Width/Height on the control always win.
        var hasExplicitWidth = !double.IsNaN(Width);
        var hasExplicitHeight = !double.IsNaN(Height);
        if (hasExplicitWidth && hasExplicitHeight)
        {
            var explicitSize = new Size(Width, Height);
            ImageCanvas.Measure(explicitSize);
            return explicitSize;
        }

        if (_naturalWidth <= 0 || _naturalHeight <= 0)
        {
            var placeholder = new Size(
                hasExplicitWidth ? Width : (double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width),
                hasExplicitHeight ? Height : (double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height));
            if (placeholder.Width <= 0 && placeholder.Height <= 0)
            {
                placeholder = new Size(0, 0);
            }

            ImageCanvas.Measure(placeholder);
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
            // Clamp Uniform so we never upscale beyond natural pixels when unconstrained.
            if (Stretch == Stretch.Uniform && double.IsInfinity(availableSize.Width) && double.IsInfinity(availableSize.Height))
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

        ImageCanvas.Measure(desired);
        return desired;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ImageCanvas.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
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

        if (IsCircular)
        {
            var radius = (float)(Math.Min(sender.ActualWidth, sender.ActualHeight) / 2);
            var center = new Vector2(
                (float)(sender.ActualWidth / 2),
                (float)(sender.ActualHeight / 2));
            using var clip = CanvasGeometry.CreateCircle(sender.Device, center, radius);
            using (args.DrawingSession.CreateLayer(1f, clip))
            {
                args.DrawingSession.DrawImage(bitmap, destination);
            }

            return;
        }

        args.DrawingSession.DrawImage(bitmap, destination);
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
