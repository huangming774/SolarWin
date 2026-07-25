using System.Collections.Concurrent;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Services;
using Windows.Storage.Streams;

namespace SolarWin.Helpers;

/// <summary>
/// Authenticated loader + bounded LRU memory cache for DysonFS files
/// (GET https://api.solian.app/drive/files/{id}).
/// Bitmaps are decoded at display size when a decode width is given —
/// a full-resolution decode of every avatar/sticker was the main driver
/// of multi-GB working sets over long sessions.
/// </summary>
public sealed class DysonFileImageLoader
{
    /// <summary>36px chat/list avatars (2.5x DPI headroom).</summary>
    public const int AvatarDecodeWidth = 96;

    /// <summary>Sticker picker cells (~72px).</summary>
    public const int StickerThumbDecodeWidth = 192;

    /// <summary>In-bubble stickers (up to 160px).</summary>
    public const int StickerDecodeWidth = 320;

    /// <summary>Chat image attachments (bubble ~280x240, preview dialog 720).</summary>
    public const int ChatImageDecodeWidth = 640;

    /// <summary>Post feed images (list MaxWidth 480 + DPI headroom).</summary>
    public const int FeedImageDecodeWidth = 640;

    /// <summary>Large profile pictures / headers.</summary>
    public const int ProfileDecodeWidth = 256;

    /// <summary>Profile background banners.</summary>
    public const int BannerDecodeWidth = 1280;

    /// <summary>Post detail / chat image preview (dialog ~720px, 2x DPI headroom).</summary>
    public const int DetailImageDecodeWidth = 1440;

    private const int MaxEntries = 160;
    private const int MaxConcurrentLoads = 8;
    private const int MaxDownloadBytes = 16 * 1024 * 1024;
    /// <summary>Was 256MB; tightened after memory review (UI may still pin some BitmapImages).</summary>
    private const long MaxEstimatedBytes = 128L * 1024 * 1024;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenStorage _tokenStorage;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<BitmapImage?>> _inflight = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadGate = new(MaxConcurrentLoads, MaxConcurrentLoads);
    private readonly object _evictGate = new();
    private long _estimatedTotalBytes;

    public DysonFileImageLoader(IHttpClientFactory httpClientFactory, ITokenStorage tokenStorage)
    {
        _httpClientFactory = httpClientFactory;
        _tokenStorage = tokenStorage;
    }

    private sealed class CacheEntry
    {
        public required BitmapImage Image { get; init; }

        public long EstimatedBytes { get; init; }

        public long LastAccessTick;
    }

    public bool TryGetCached(string? fileIdOrUrl, out BitmapImage? image, int decodeWidth = 0)
    {
        image = null;
        var key = CacheKey(fileIdOrUrl);
        if (key is null)
        {
            return false;
        }

        if (TryGet(EffectiveKey(key, decodeWidth), out image))
        {
            return true;
        }

        // A full-resolution copy is always an acceptable substitute for a thumbnail.
        return decodeWidth > 0 && TryGet(key, out image);
    }

    public Task<BitmapImage?> LoadAsync(string? fileIdOrUrl, CancellationToken cancellationToken = default)
        => LoadAsync(fileIdOrUrl, decodeWidth: 0, cancellationToken);

    public Task<BitmapImage?> LoadAsync(string? fileIdOrUrl, int decodeWidth, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(fileIdOrUrl);
        if (key is null)
        {
            return Task.FromResult<BitmapImage?>(null);
        }

        if (TryGet(EffectiveKey(key, decodeWidth), out var hit))
        {
            return Task.FromResult(hit);
        }

        if (decodeWidth > 0 && TryGet(key, out var fullRes))
        {
            return Task.FromResult(fullRes);
        }

        var effectiveKey = EffectiveKey(key, decodeWidth);
        var task = _inflight.GetOrAdd(
            effectiveKey,
            _ => LoadCoreAsync(key, effectiveKey, decodeWidth, cancellationToken));
        return task;
    }

    /// <summary>LoadAsync that never faults; null on any failure. Preferred for UI image binding.</summary>
    public Task<BitmapImage?> LoadSafeAsync(string? fileIdOrUrl, CancellationToken cancellationToken = default)
        => LoadSafeAsync(fileIdOrUrl, decodeWidth: 0, cancellationToken);

    /// <summary>LoadAsync that never faults; null on any failure. Preferred for UI image binding.</summary>
    public async Task<BitmapImage?> LoadSafeAsync(string? fileIdOrUrl, int decodeWidth, CancellationToken cancellationToken = default)
    {
        try
        {
            return await LoadAsync(fileIdOrUrl, decodeWidth, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private async Task<BitmapImage?> LoadCoreAsync(
        string key,
        string effectiveKey,
        int decodeWidth,
        CancellationToken cancellationToken)
    {
        var gateEntered = false;
        try
        {
            await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            gateEntered = true;

            var url = key.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? key
                : CloudFileUrlHelper.DriveFileUrl(key);

            var client = _httpClientFactory.CreateClient(SolarApiClient.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            var token = await _tokenStorage.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength is > MaxDownloadBytes)
            {
                return null;
            }

            var bytes = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
            {
                return null;
            }

            // BitmapImage must be created/set on UI thread in WinUI.
            // Prepare the RandomAccessStream before enqueueing so WriteBytes of multi-MB
            // post images does not stall the dispatcher (was a major feed lag source).
            var stream = await CreateImageStreamAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (stream is null)
            {
                return null;
            }

            var tcs = new TaskCompletionSource<BitmapImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dq = App.DispatcherQueue;
            if (dq is null)
            {
                stream.Dispose();
                return null;
            }

            var ok = dq.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stream.Seek(0);
                    var bmp = new BitmapImage();
                    if (decodeWidth > 0)
                    {
                        bmp.DecodePixelWidth = decodeWidth;
                    }

                    await bmp.SetSourceAsync(stream).AsTask(cancellationToken);
                    AddToCache(effectiveKey, bmp);
                    tcs.TrySetResult(bmp);
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
                finally
                {
                    stream.Dispose();
                }
            });

            if (!ok)
            {
                stream.Dispose();
                return null;
            }

            return await tcs.Task.ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (gateEntered)
            {
                _loadGate.Release();
            }

            _inflight.TryRemove(effectiveKey, out _);
        }
    }

    private static async Task<InMemoryRandomAccessStream?> CreateImageStreamAsync(
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        try
        {
            var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
            }

            stream.Seek(0);
            return stream;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]?> ReadBoundedAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var initialCapacity = content.Headers.ContentLength is > 0 and <= MaxDownloadBytes
            ? (int)content.Headers.ContentLength.Value
            : 81920;
        using var buffer = new MemoryStream(initialCapacity);
        var readBuffer = new byte[81920];

        while (true)
        {
            var read = await source
                .ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > MaxDownloadBytes)
            {
                return null;
            }

            await buffer
                .WriteAsync(readBuffer.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private bool TryGet(string key, out BitmapImage? image)
    {
        image = null;
        if (!_cache.TryGetValue(key, out var entry))
        {
            return false;
        }

        Interlocked.Exchange(ref entry.LastAccessTick, Environment.TickCount64);
        image = entry.Image;
        return true;
    }

    private void AddToCache(string key, BitmapImage image)
    {
        var estimated = Math.Max(1L, (long)image.PixelWidth * image.PixelHeight * 4);
        if (_cache.TryRemove(key, out var previous))
        {
            Interlocked.Add(ref _estimatedTotalBytes, -previous.EstimatedBytes);
        }

        _cache[key] = new CacheEntry
        {
            Image = image,
            EstimatedBytes = estimated,
            LastAccessTick = Environment.TickCount64,
        };
        Interlocked.Add(ref _estimatedTotalBytes, estimated);
        EvictIfNeeded();
    }

    private void EvictIfNeeded()
    {
        if (_cache.Count <= MaxEntries
            && Interlocked.Read(ref _estimatedTotalBytes) <= MaxEstimatedBytes)
        {
            return;
        }

        lock (_evictGate)
        {
            while (!_cache.IsEmpty
                   && (_cache.Count > MaxEntries
                       || Interlocked.Read(ref _estimatedTotalBytes) > MaxEstimatedBytes))
            {
                string? oldestKey = null;
                var oldestTick = long.MaxValue;
                foreach (var kv in _cache)
                {
                    var tick = Interlocked.Read(ref kv.Value.LastAccessTick);
                    if (tick < oldestTick)
                    {
                        oldestTick = tick;
                        oldestKey = kv.Key;
                    }
                }

                if (oldestKey is null)
                {
                    break;
                }

                if (_cache.TryRemove(oldestKey, out var removed))
                {
                    Interlocked.Add(ref _estimatedTotalBytes, -removed.EstimatedBytes);
                }
                else
                {
                    break;
                }
            }
        }
    }

    /// <summary>Drop all cached bitmaps (e.g. logout). Does not clear UI-held references.</summary>
    public void Clear()
    {
        _cache.Clear();
        _inflight.Clear();
        Interlocked.Exchange(ref _estimatedTotalBytes, 0);
    }

    private static string EffectiveKey(string key, int decodeWidth)
        => decodeWidth > 0 ? $"{key}#w{decodeWidth}" : key;

    private static string? CacheKey(string? fileIdOrUrl)
    {
        if (string.IsNullOrWhiteSpace(fileIdOrUrl))
        {
            return null;
        }

        var s = fileIdOrUrl.Trim();
        var marker = "/drive/files/";
        var idx = s.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var rest = s[(idx + marker.Length)..].Trim('/');
            return rest.Split('?', '#')[0];
        }

        return s;
    }
}
