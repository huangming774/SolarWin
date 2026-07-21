using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;
using Windows.Storage.Streams;

namespace SolarWin.Helpers;

/// <summary>Generate scannable QR bitmaps from Padlock <c>qr_data</c> payloads.</summary>
public static class QrCodeImageHelper
{
    /// <summary>Encode payload as PNG bytes (thread-safe, no UI dependency).</summary>
    public static byte[] CreatePng(string payload, int pixelsPerModule = 12)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        pixelsPerModule = Math.Clamp(pixelsPerModule, 4, 24);

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload.Trim(), QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        // quietZones: true for reliable phone scanning
        return png.GetGraphic(pixelsPerModule);
    }

    /// <summary>Build a <see cref="BitmapImage"/> on the UI thread from PNG bytes.</summary>
    public static async Task<BitmapImage> ToBitmapImageAsync(byte[] pngBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        if (pngBytes.Length == 0)
        {
            throw new ArgumentException("PNG 数据为空。", nameof(pngBytes));
        }

        var image = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(pngBytes);
            await writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(true);
            await writer.FlushAsync().AsTask(cancellationToken).ConfigureAwait(true);
        }

        stream.Seek(0);
        await image.SetSourceAsync(stream).AsTask(cancellationToken).ConfigureAwait(true);
        return image;
    }

    public static async Task<BitmapImage> CreateBitmapAsync(string payload, CancellationToken cancellationToken = default)
    {
        var png = CreatePng(payload);
        return await ToBitmapImageAsync(png, cancellationToken).ConfigureAwait(true);
    }
}
