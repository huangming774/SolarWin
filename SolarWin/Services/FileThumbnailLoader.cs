using System.Collections.Concurrent;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using Windows.Storage.Streams;

namespace SolarWin.Services;

/// <summary>
/// Shared authenticated thumbnail loader for the Drive file grid.
/// Network I/O stays async, BitmapImage creation stays on the UI thread, and
/// concurrent thumbnail work is bounded so scrolling cannot flood the app.
/// </summary>
public sealed class FileThumbnailLoader
{
    public const int DefaultDecodePixelWidth = 192;

    private const int DefaultMaxConcurrency = 4;
    private const int MaxEntries = 120;
    private const long MaxEstimatedBytes = 64L * 1024 * 1024;
    private const int MaxDownloadBytes = 8 * 1024 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenStorage _tokenStorage;
    private readonly SemaphoreSlim _workGate;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _evictGate = new();
    private long _estimatedTotalBytes;

    public FileThumbnailLoader(IHttpClientFactory httpClientFactory, ITokenStorage tokenStorage)
    {
        _httpClientFactory = httpClientFactory;
        _tokenStorage = tokenStorage;
        MaxConcurrency = AppSettings.FileThumbnailMaxConcurrency;
        if (MaxConcurrency <= 0)
        {
            MaxConcurrency = DefaultMaxConcurrency;
        }

        _workGate = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
    }

    public int MaxConcurrency { get; }

    public int CacheMaxEntries => MaxEntries;

    public long CacheMaxEstimatedBytes => MaxEstimatedBytes;

    private sealed class CacheEntry
    {
        public required BitmapImage Image { get; init; }

        public long EstimatedBytes { get; init; }

        public long LastAccessTick;
    }

    public bool TryGetCached(string? thumbnailUrl, int decodePixelWidth, out BitmapImage? image)
    {
        image = null;
        var key = CacheKey(thumbnailUrl, decodePixelWidth);
        if (key is null)
        {
            return false;
        }

        if (!_cache.TryGetValue(key, out var entry))
        {
            return false;
        }

        Interlocked.Exchange(ref entry.LastAccessTick, Environment.TickCount64);
        image = entry.Image;
        return true;
    }

    public async Task<BitmapImage?> LoadSafeAsync(
        string? thumbnailUrl,
        int decodePixelWidth = DefaultDecodePixelWidth,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await LoadAsync(thumbnailUrl, decodePixelWidth, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<BitmapImage?> LoadAsync(
        string? thumbnailUrl,
        int decodePixelWidth,
        CancellationToken cancellationToken)
    {
        if (TryGetCached(thumbnailUrl, decodePixelWidth, out var cached))
        {
            return cached;
        }

        var normalizedUrl = CloudFileUrlHelper.Normalize(thumbnailUrl);
        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return null;
        }

        var key = CacheKey(normalizedUrl, decodePixelWidth);
        if (key is null)
        {
            return null;
        }

        await _workGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var timeoutCts = new CancellationTokenSource(RequestTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var token = linkedCts.Token;

            var bytes = await DownloadThumbnailBytesAsync(normalizedUrl, token).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                return null;
            }

            return await CreateBitmapOnUiThreadAsync(key, bytes, decodePixelWidth, token).ConfigureAwait(false);
        }
        finally
        {
            _workGate.Release();
        }
    }

    private async Task<byte[]> DownloadThumbnailBytesAsync(string thumbnailUrl, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(SolarApiClient.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, thumbnailUrl);
        var token = await _tokenStorage.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode
            || response.Content.Headers.ContentLength is > MaxDownloadBytes)
        {
            return [];
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var buffer = new MemoryStream();
        var readBuffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(readBuffer.AsMemory(0, readBuffer.Length), cancellationToken)
                   .ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxDownloadBytes)
            {
                return [];
            }

            await buffer.WriteAsync(readBuffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private async Task<BitmapImage?> CreateBitmapOnUiThreadAsync(
        string cacheKey,
        byte[] bytes,
        int decodePixelWidth,
        CancellationToken cancellationToken)
    {
        var dq = App.DispatcherQueue;
        if (dq is null)
        {
            return null;
        }

        if (dq.HasThreadAccess)
        {
            return await CreateBitmapCoreAsync(cacheKey, bytes, decodePixelWidth, cancellationToken).ConfigureAwait(true);
        }

        var tcs = new TaskCompletionSource<BitmapImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dq.TryEnqueue(DispatcherQueuePriority.Low, async () =>
            {
                try
                {
                    var image = await CreateBitmapCoreAsync(cacheKey, bytes, decodePixelWidth, cancellationToken)
                        .ConfigureAwait(true);
                    tcs.TrySetResult(image);
                }
                catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken || cancellationToken.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(cancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
        {
            return null;
        }

        return await tcs.Task.ConfigureAwait(false);
    }

    private async Task<BitmapImage?> CreateBitmapCoreAsync(
        string cacheKey,
        byte[] bytes,
        int decodePixelWidth,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync().AsTask(cancellationToken);
            await writer.FlushAsync().AsTask(cancellationToken);
        }

        stream.Seek(0);
        var bitmap = new BitmapImage();
        if (decodePixelWidth > 0)
        {
            bitmap.DecodePixelWidth = decodePixelWidth;
        }

        await bitmap.SetSourceAsync(stream).AsTask(cancellationToken);
        AddToCache(cacheKey, bitmap);
        return bitmap;
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

    /// <summary>Drop all cached thumbnails (e.g. logout).</summary>
    public void Clear()
    {
        _cache.Clear();
        Interlocked.Exchange(ref _estimatedTotalBytes, 0);
    }

    private static string? CacheKey(string? thumbnailUrl, int decodePixelWidth)
    {
        if (string.IsNullOrWhiteSpace(thumbnailUrl))
        {
            return null;
        }

        var normalized = CloudFileUrlHelper.Normalize(thumbnailUrl);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return $"{normalized}#w{Math.Max(0, decodePixelWidth)}";
    }
}
