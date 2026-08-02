using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using LiveKit.Proto;
using LiveKit.Rtc;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace SolarWin.Services;

/// <summary>
/// Windows.Graphics.Capture publisher backed by D3D11. Capture and scaling stay on GPU;
/// the final BGRA readback is required by LiveKit .NET 0.1.3's byte-only VideoFrame API.
/// </summary>
internal sealed class ScreenCapturePublisher : IAsyncDisposable
{
    private const int TargetFrameIntervalMs = 80;
    private readonly VideoSource _source;
    private readonly int _outW;
    private readonly int _outH;
    private readonly object _resourceGate = new();
    private IDirect3DDevice? _direct3DDevice;
    private CanvasDevice? _canvasDevice;
    private CanvasRenderTarget? _scaledFrame;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private byte[]? _pixelBytes;
    private Windows.Storage.Streams.IBuffer? _pixelBuffer;
    private VideoFrame? _videoFrame;
    private long _lastFrameTick;
    private int _processingFrame;

    public ScreenCapturePublisher(VideoSource source, int outWidth = 960, int outHeight = 540)
    {
        _source = source;
        _outW = outWidth;
        _outH = outHeight;
    }

    public Task StartAsync()
    {
        lock (_resourceGate)
        {
            StopCore();
            if (!GraphicsCaptureSession.IsSupported())
            {
                throw new NotSupportedException("Windows.Graphics.Capture is unavailable on this system.");
            }

            _direct3DDevice = CreateDirect3DDevice();
            _canvasDevice = CanvasDevice.CreateFromDirect3D11Device(_direct3DDevice);
            _scaledFrame = new CanvasRenderTarget(_canvasDevice, _outW, _outH, 96);
            _pixelBytes = new byte[checked(_outW * _outH * 4)];
            _pixelBuffer = _pixelBytes.AsBuffer();
            _videoFrame = new VideoFrame(_outW, _outH, VideoBufferType.Bgra, _pixelBytes);
            _item = CreatePrimaryMonitorItem();
            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _direct3DDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                _item.Size);
            _framePool.FrameArrived += OnFrameArrived;
            _session = _framePool.CreateCaptureSession(_item);
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                _session.IsCursorCaptureEnabled = true;
            }
            _session.StartCapture();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        lock (_resourceGate)
        {
            StopCore();
        }

        return Task.CompletedTask;
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        Direct3D11CaptureFrame? frame = null;
        var ownsProcessingGate = false;
        try
        {
            frame = sender.TryGetNextFrame();
            var now = Environment.TickCount64;
            if (frame is null
                || now - Interlocked.Read(ref _lastFrameTick) < TargetFrameIntervalMs
                || Interlocked.CompareExchange(ref _processingFrame, 1, 0) != 0)
            {
                return;
            }

            ownsProcessingGate = true;
            lock (_resourceGate)
            {
                if (_canvasDevice is null
                    || _scaledFrame is null
                    || _pixelBuffer is null
                    || _videoFrame is null)
                {
                    return;
                }

                using var captured = CanvasBitmap.CreateFromDirect3D11Surface(_canvasDevice, frame.Surface);
                using (var drawing = _scaledFrame.CreateDrawingSession())
                {
                    drawing.Clear(Microsoft.UI.Colors.Black);
                    var sourceSize = captured.SizeInPixels;
                    var scale = Math.Min((double)_outW / sourceSize.Width, (double)_outH / sourceSize.Height);
                    var width = sourceSize.Width * scale;
                    var height = sourceSize.Height * scale;
                    var destination = new Rect((_outW - width) / 2, (_outH - height) / 2, width, height);
                    drawing.DrawImage(captured, destination);
                }

                // LiveKit's current FFI has no texture-handle input, so this is the sole GPU -> CPU boundary.
                // Read into one reusable WinRT buffer and reuse the VideoFrame wrapper instead
                // of allocating a 960 x 540 x 4 byte array (~2 MiB) for every callback.
                _scaledFrame.GetPixelBytes(_pixelBuffer);
                _source.CaptureFrame(_videoFrame);
                Interlocked.Exchange(ref _lastFrameTick, now);
            }
        }
        catch
        {
            // Capture is best effort; a transient device/frame failure drops only this frame.
        }
        finally
        {
            frame?.Dispose();
            if (ownsProcessingGate) Interlocked.Exchange(ref _processingFrame, 0);
        }
    }

    private void StopCore()
    {
        if (_framePool is not null)
        {
            _framePool.FrameArrived -= OnFrameArrived;
        }

        _session?.Dispose();
        _framePool?.Dispose();
        _scaledFrame?.Dispose();
        _canvasDevice?.Dispose();
        _direct3DDevice?.Dispose();
        _session = null;
        _framePool = null;
        _item = null;
        _scaledFrame = null;
        _canvasDevice = null;
        _direct3DDevice = null;
        _pixelBuffer = null;
        _pixelBytes = null;
        _videoFrame = null;
        Interlocked.Exchange(ref _processingFrame, 0);
    }

    public ValueTask DisposeAsync()
    {
        lock (_resourceGate)
        {
            StopCore();
        }

        return ValueTask.CompletedTask;
    }

    private static GraphicsCaptureItem CreatePrimaryMonitorItem()
    {
        var monitor = MonitorFromWindow(IntPtr.Zero, 1);
        var className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        CheckHr(WindowsCreateString(className, className.Length, out var hstring));
        IntPtr factoryPointer = IntPtr.Zero;
        IntPtr itemPointer = IntPtr.Zero;
        try
        {
            var interopId = typeof(IGraphicsCaptureItemInterop).GUID;
            CheckHr(RoGetActivationFactory(hstring, ref interopId, out factoryPointer));
            var factory = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPointer);
            var itemId = typeof(GraphicsCaptureItem).GUID;
            CheckHr(factory.CreateForMonitor(monitor, ref itemId, out itemPointer));
            return MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);
        }
        finally
        {
            if (itemPointer != IntPtr.Zero) Marshal.Release(itemPointer);
            if (factoryPointer != IntPtr.Zero) Marshal.Release(factoryPointer);
            CheckHr(WindowsDeleteString(hstring));
        }
    }

    private static IDirect3DDevice CreateDirect3DDevice()
    {
        IntPtr d3dDevice = IntPtr.Zero;
        IntPtr context = IntPtr.Zero;
        IntPtr dxgiDevice = IntPtr.Zero;
        IntPtr inspectable = IntPtr.Zero;
        try
        {
            var hr = D3D11CreateDevice(
                IntPtr.Zero, 1, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7,
                out d3dDevice, out _, out context);
            if (hr < 0)
            {
                hr = D3D11CreateDevice(
                    IntPtr.Zero, 5, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7,
                    out d3dDevice, out _, out context);
            }

            CheckHr(hr);
            var dxgiId = new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
            CheckHr(Marshal.QueryInterface(d3dDevice, in dxgiId, out dxgiDevice));
            CheckHr(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out inspectable));
            return MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        }
        finally
        {
            if (inspectable != IntPtr.Zero) Marshal.Release(inspectable);
            if (dxgiDevice != IntPtr.Zero) Marshal.Release(dxgiDevice);
            if (context != IntPtr.Zero) Marshal.Release(context);
            if (d3dDevice != IntPtr.Zero) Marshal.Release(d3dDevice);
        }
    }

    private static void CheckHr(int hr)
    {
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig]
        int CreateForWindow(IntPtr window, ref Guid iid, out IntPtr result);

        [PreserveSig]
        int CreateForMonitor(IntPtr monitor, ref Guid iid, out IntPtr result);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("combase.dll")]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        int length,
        out IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(IntPtr hstring, ref Guid iid, out IntPtr factory);

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(
        IntPtr adapter,
        int driverType,
        IntPtr software,
        uint flags,
        IntPtr featureLevels,
        uint featureLevelsCount,
        uint sdkVersion,
        out IntPtr device,
        out int featureLevel,
        out IntPtr immediateContext);

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
}
