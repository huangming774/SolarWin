using System.Runtime.InteropServices;
using LiveKit.Proto;
using LiveKit.Rtc;

namespace SolarWin.Services;

/// <summary>
/// GDI primary-monitor capture → LiveKit VideoSource (screenshare). ~12 fps scaled.
/// </summary>
internal sealed class ScreenCapturePublisher : IAsyncDisposable
{
    private const uint SrcCopy = 0x00CC0020;
    private const int ColorOnColor = 3;

    private readonly VideoSource _source;
    private readonly int _outW;
    private readonly int _outH;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public ScreenCapturePublisher(VideoSource source, int outWidth = 960, int outHeight = 540)
    {
        _source = source;
        _outW = outWidth;
        _outH = outHeight;
    }

    public async Task StartAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token), _cts.Token);
    }

    public async Task StopAsync()
    {
        var cts = _cts;
        var loop = _loop;
        _cts = null;
        _loop = null;

        try
        {
            cts?.Cancel();
        }
        catch
        {
            // ignore
        }

        if (loop is not null)
        {
            try
            {
                await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected
            }
            catch
            {
                // ignore
            }
        }

        cts?.Dispose();
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var screenW = GetSystemMetrics(0);
        var screenH = GetSystemMetrics(1);
        if (screenW <= 0 || screenH <= 0)
        {
            return;
        }

        var hdcScreen = GetDC(IntPtr.Zero);
        var hdcMem = CreateCompatibleDC(hdcScreen);
        var hBitmap = CreateCompatibleBitmap(hdcScreen, _outW, _outH);
        var old = SelectObject(hdcMem, hBitmap);
        SetStretchBltMode(hdcMem, ColorOnColor);

        var bmi = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = _outW,
                biHeight = -_outH,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0,
            },
        };

        var frameBytes = new byte[_outW * _outH * 4];
        var handle = GCHandle.Alloc(frameBytes, GCHandleType.Pinned);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                StretchBlt(
                    hdcMem,
                    0,
                    0,
                    _outW,
                    _outH,
                    hdcScreen,
                    0,
                    0,
                    screenW,
                    screenH,
                    SrcCopy);
                GetDIBits(
                    hdcMem,
                    hBitmap,
                    0,
                    (uint)_outH,
                    handle.AddrOfPinnedObject(),
                    ref bmi,
                    0);

                SwapRedBlue(frameBytes);
                try
                {
                    _source.CaptureFrame(new VideoFrame(_outW, _outH, VideoBufferType.Rgba, frameBytes));
                }
                catch
                {
                    // drop
                }

                try
                {
                    await Task.Delay(80, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            handle.Free();
            SelectObject(hdcMem, old);
            DeleteObject(hBitmap);
            DeleteDC(hdcMem);
            ReleaseDC(IntPtr.Zero, hdcScreen);
        }
    }

    private static void SwapRedBlue(byte[] pixels)
    {
        for (var i = 0; i < pixels.Length; i += 4)
        {
            (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        }
    }

    public ValueTask DisposeAsync()
        => new(StopAsync());

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern int SetStretchBltMode(IntPtr hdc, int mode);

    [DllImport("gdi32.dll")]
    private static extern bool StretchBlt(
        IntPtr hdcDest,
        int xDest,
        int yDest,
        int widthDest,
        int heightDest,
        IntPtr hdcSrc,
        int xSrc,
        int ySrc,
        int widthSrc,
        int heightSrc,
        uint rop);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        IntPtr hdc,
        IntPtr hbmp,
        uint uStartScan,
        uint cScanLines,
        IntPtr lpvBits,
        ref BITMAPINFO lpbi,
        uint uUsage);

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }
}
