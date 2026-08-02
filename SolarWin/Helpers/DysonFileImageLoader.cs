using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Services;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace SolarWin.Helpers;

/// <summary>
/// Dual-path image loader for Solar cloud files.
/// <list type="bullet">
/// <item>
/// <b>GPU path (posts)</b>: <see cref="LoadImageAsync"/> / <see cref="TryAcquireCached"/> return a
/// <see cref="CanvasBitmapLease"/> over a shared <see cref="CanvasDevice"/> VRAM LRU
/// (<see cref="GpuCacheMaxBytes"/> = 64 MiB). Callers (typically <c>FastWin2DImage</c>) must dispose the lease
/// when the bitmap leaves the screen so eviction can free Direct3D textures immediately.
/// </item>
/// <item>
/// <b>Legacy path (profile / lightbox / non-migrated UI)</b>: <see cref="LoadAsync"/> / <see cref="LoadSafeAsync"/> /
/// <see cref="TryGetCached"/> return <see cref="BitmapImage"/> backed by a separate 64 MiB RAM LRU
/// (<see cref="LegacyCacheMaxBytes"/>). Combined budget stays within 128 MiB.
/// </item>
/// </list>
/// Decode widths: avatars 96px, chat/feed 640px, stickers 256px, detail 1440px. Single download hard-cap: 16 MiB.
/// All async loads honor <see cref="CancellationToken"/> and release intermediate streams/bitmaps on cancel.
/// <see cref="GpuCacheInvalidated"/> fires on <see cref="Clear"/> and device-lost so UI can re-acquire leases.
/// </summary>
public sealed class DysonFileImageLoader : IDisposable
{
    public const int AvatarDecodeWidth = 96;
    /// <summary>Sticker picker / bubble sticker decode side (max pixel width).</summary>
    public const int StickerThumbDecodeWidth = 256;
    public const int StickerDecodeWidth = 256;
    public const int ChatImageDecodeWidth = 640;
    public const int FeedImageDecodeWidth = 640;
    public const int ProfileDecodeWidth = 256;
    public const int BannerDecodeWidth = 1280;
    public const int DetailImageDecodeWidth = 1440;

    private const int MaxEntries = 160;
    private const int MaxDownloadBytes = 16 * 1024 * 1024;
    private const int PersistentThumbnailMaxDecodeWidth = 256;
    private const int PersistentThumbnailMaxFileBytes = 4 * 1024 * 1024;
    private const long PersistentThumbnailMaxTotalBytes = 256L * 1024 * 1024;
    private static readonly TimeSpan PersistentThumbnailMaxAge = TimeSpan.FromDays(30);
    private const string ActiveAccountIdSettingKey = "ActiveAccountId";

    /// <summary>RAM budget for legacy BitmapImage cache (chat / profile).</summary>
    private const long LegacyCacheMaxBytes = 64L * 1024 * 1024;

    /// <summary>VRAM budget for CanvasBitmap lease cache (posts FastWin2DImage path).</summary>
    private const long GpuCacheMaxBytes = 64L * 1024 * 1024;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenStorage _tokenStorage;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<BitmapImage?>> _inflight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, GpuLoadOperation> _gpuInflight = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _maintainedDiskScopes = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadGate;
    private readonly LeasedLruCache<string, CanvasBitmap> _gpuCache =
        new(GpuCacheMaxBytes, StringComparer.OrdinalIgnoreCase);
    private readonly object _evictGate = new();
    private long _estimatedTotalBytes;
    private int _disposed;

    public DysonFileImageLoader(IHttpClientFactory httpClientFactory, ITokenStorage tokenStorage)
    {
        _httpClientFactory = httpClientFactory;
        _tokenStorage = tokenStorage;
        MaxConcurrency = Math.Max(1, AppSettings.FileThumbnailMaxConcurrency);
        _loadGate = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        Device = CanvasDevice.GetSharedDevice();
        Device.DeviceLost += OnDeviceLost;
    }

    public CanvasDevice Device { get; }

    public int MaxConcurrency { get; }

    public int CacheMaxEntries => MaxEntries;

    public long CacheMaxEstimatedBytes => LegacyCacheMaxBytes;

    public event EventHandler? GpuCacheInvalidated;

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

        return decodeWidth > 0 && TryGet(key, out image);
    }

    public Task<BitmapImage?> LoadAsync(string? fileIdOrUrl, CancellationToken cancellationToken = default)
        => LoadAsync(fileIdOrUrl, decodeWidth: 0, cancellationToken);

    public Task<BitmapImage?> LoadAsync(
        string? fileIdOrUrl,
        int decodeWidth,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
        return task.WaitAsync(cancellationToken);
    }

    public Task<BitmapImage?> LoadSafeAsync(string? fileIdOrUrl, CancellationToken cancellationToken = default)
        => LoadSafeAsync(fileIdOrUrl, decodeWidth: 0, cancellationToken);

    public async Task<BitmapImage?> LoadSafeAsync(
        string? fileIdOrUrl,
        int decodeWidth,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await LoadAsync(fileIdOrUrl, decodeWidth, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public bool TryAcquireCached(
        string? fileIdOrUrl,
        int decodeWidth,
        out CanvasBitmapLease? lease)
    {
        lease = null;
        var key = CacheKey(fileIdOrUrl);
        if (key is null)
        {
            return false;
        }

        if (!_gpuCache.TryAcquire(EffectiveKey(key, decodeWidth), out var cacheLease)
            || cacheLease is null)
        {
            return false;
        }

        lease = new CanvasBitmapLease(cacheLease);
        return true;
    }

    public async Task<CanvasBitmapLease?> LoadImageAsync(
        string? fileIdOrUrl,
        int decodeWidth,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(decodeWidth);

        var key = CacheKey(fileIdOrUrl);
        if (key is null)
        {
            return null;
        }

        var effectiveKey = EffectiveKey(key, decodeWidth);
        if (_gpuCache.TryAcquire(effectiveKey, out var cached) && cached is not null)
        {
            return new CanvasBitmapLease(cached);
        }

        var operation = _gpuInflight.GetOrAdd(effectiveKey, _ => new GpuLoadOperation());
        operation.AddWaiter();
        try
        {
            var loaded = await operation
                .GetOrStart(() => RunGpuLoadAsync(operation, key, effectiveKey, decodeWidth))
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!loaded
                || !_gpuCache.TryAcquire(effectiveKey, out var lease)
                || lease is null)
            {
                return null;
            }

            return new CanvasBitmapLease(lease);
        }
        finally
        {
            operation.ReleaseWaiter();
        }
    }

    private async Task<bool> RunGpuLoadAsync(
        GpuLoadOperation operation,
        string key,
        string effectiveKey,
        int decodeWidth)
    {
        try
        {
            return await LoadGpuCoreAsync(key, effectiveKey, decodeWidth, operation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            operation.MarkCompleted();
            if (_gpuInflight.TryGetValue(effectiveKey, out var current)
                && ReferenceEquals(current, operation))
            {
                _gpuInflight.TryRemove(effectiveKey, out _);
            }
        }
    }

    private async Task<bool> LoadGpuCoreAsync(
        string key,
        string effectiveKey,
        int decodeWidth,
        CancellationToken cancellationToken)
    {
        var gateEntered = false;
        CanvasBitmap? bitmap = null;
        try
        {
            await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            gateEntered = true;
            cancellationToken.ThrowIfCancellationRequested();

            if (_gpuCache.TryAcquire(effectiveKey, out var existing) && existing is not null)
            {
                existing.Dispose();
                return true;
            }

            var bytes = await GetImageBytesAsync(key, decodeWidth, cancellationToken).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
            {
                return false;
            }

            using var stream = await CreateImageStreamAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (stream is null)
            {
                return false;
            }

            var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var sourceWidth = decoder.OrientedPixelWidth;
            var sourceHeight = decoder.OrientedPixelHeight;
            var scale = Math.Min(1d, decodeWidth / (double)Math.Max(1u, sourceWidth));
            var targetWidth = Math.Max(1u, (uint)Math.Round(sourceWidth * scale));
            var targetHeight = Math.Max(1u, (uint)Math.Round(sourceHeight * scale));
            var transform = new BitmapTransform
            {
                ScaledWidth = targetWidth,
                ScaledHeight = targetHeight,
                InterpolationMode = BitmapInterpolationMode.Fant,
            };

            using var softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.RespectExifOrientation,
                    ColorManagementMode.ColorManageToSRgb)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            bitmap = CanvasBitmap.CreateFromSoftwareBitmap(Device, softwareBitmap);
            cancellationToken.ThrowIfCancellationRequested();
            var estimatedBytes = Math.Max(1L, (long)targetWidth * targetHeight * 4);
            var cacheLease = _gpuCache.AddOrAcquire(effectiveKey, bitmap, estimatedBytes);
            bitmap = null;
            if (cacheLease is null)
            {
                return false;
            }

            cacheLease.Dispose();
            return true;
        }
        finally
        {
            bitmap?.Dispose();
            if (gateEntered)
            {
                _loadGate.Release();
            }
        }
    }

    private async Task<byte[]?> GetImageBytesAsync(
        string key,
        int decodeWidth,
        CancellationToken cancellationToken)
    {
        var cachePath = GetPersistentThumbnailPath(key, decodeWidth);
        if (cachePath is not null)
        {
            var cached = await TryReadPersistentThumbnailAsync(cachePath, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }
        }

        var bytes = await DownloadBoundedAsync(key, cancellationToken).ConfigureAwait(false);
        if (cachePath is not null && bytes is { Length: > 0 and <= PersistentThumbnailMaxFileBytes })
        {
            await TryWritePersistentThumbnailAsync(cachePath, bytes, cancellationToken).ConfigureAwait(false);
        }

        return bytes;
    }

    private async Task<byte[]?> DownloadBoundedAsync(string key, CancellationToken cancellationToken)
    {
        if (Path.IsPathRooted(key) && File.Exists(key))
        {
            var info = new FileInfo(key);
            if (info.Length <= 0 || info.Length > MaxDownloadBytes)
            {
                return null;
            }

            return await File.ReadAllBytesAsync(key, cancellationToken).ConfigureAwait(false);
        }

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
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > MaxDownloadBytes)
        {
            return null;
        }

        return await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
    }

    private string? GetPersistentThumbnailPath(string key, int decodeWidth)
    {
        if (decodeWidth <= 0 || decodeWidth > PersistentThumbnailMaxDecodeWidth)
        {
            return null;
        }

        var configuredAccount = SettingsStore.GetString(ActiveAccountIdSettingKey);
        var scope = Guid.TryParse(configuredAccount, out var accountId) && accountId != Guid.Empty
            ? accountId.ToString("N")
            : "anonymous";
        var directory = Path.Combine(AppPaths.CacheDirectory, "image-thumbnails", scope);
        Directory.CreateDirectory(directory);

        if (_maintainedDiskScopes.TryAdd(scope, 0))
        {
            _ = Task.Run(() => MaintainPersistentThumbnailScope(directory));
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(directory, $"{hash}.img");
    }

    private static async Task<byte[]?> TryReadPersistentThumbnailAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return null;
            }

            if (info.Length <= 0
                || info.Length > PersistentThumbnailMaxFileBytes
                || DateTime.UtcNow - info.LastWriteTimeUtc > PersistentThumbnailMaxAge)
            {
                TryDeleteFile(path);
                return null;
            }

            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static async Task TryWritePersistentThumbnailAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryDeleteFile(temporaryPath);
            throw;
        }
        catch
        {
            TryDeleteFile(temporaryPath);
        }
    }

    private static void MaintainPersistentThumbnailScope(string directory)
    {
        try
        {
            var cutoff = DateTime.UtcNow - PersistentThumbnailMaxAge;
            var files = new DirectoryInfo(directory)
                .EnumerateFiles("*.img", SearchOption.TopDirectoryOnly)
                .ToList();

            foreach (var file in files.Where(file => file.LastWriteTimeUtc < cutoff
                                                     || file.Length <= 0
                                                     || file.Length > PersistentThumbnailMaxFileBytes))
            {
                TryDeleteFile(file.FullName);
            }

            files = new DirectoryInfo(directory)
                .EnumerateFiles("*.img", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();
            var retainedBytes = 0L;
            foreach (var file in files)
            {
                retainedBytes += file.Length;
                if (retainedBytes > PersistentThumbnailMaxTotalBytes)
                {
                    TryDeleteFile(file.FullName);
                }
            }
        }
        catch
        {
            // Disk caching is opportunistic and must never block image display.
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best effort for stale or partial cache entries.
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
            var bytes = await GetImageBytesAsync(key, decodeWidth, cancellationToken).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
            {
                return null;
            }

            var stream = await CreateImageStreamAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (stream is null)
            {
                return null;
            }

            var tcs = new TaskCompletionSource<BitmapImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dispatcher = App.DispatcherQueue;
            if (dispatcher is null)
            {
                stream.Dispose();
                return null;
            }

            var queued = dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stream.Seek(0);
                    var image = new BitmapImage();
                    if (decodeWidth > 0)
                    {
                        image.DecodePixelWidth = decodeWidth;
                    }

                    await image.SetSourceAsync(stream).AsTask(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    AddToCache(effectiveKey, image);
                    tcs.TrySetResult(image);
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

            if (!queued)
            {
                stream.Dispose();
                return null;
            }

            return await tcs.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
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
        var stream = new InMemoryRandomAccessStream();
        try
        {
            using var writer = new DataWriter(stream.GetOutputStreamAt(0));
            writer.WriteBytes(bytes);
            await writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
            stream.Seek(0);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
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

            await buffer.WriteAsync(readBuffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
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
            && Interlocked.Read(ref _estimatedTotalBytes) <= LegacyCacheMaxBytes)
        {
            return;
        }

        lock (_evictGate)
        {
            while (!_cache.IsEmpty
                   && (_cache.Count > MaxEntries
                       || Interlocked.Read(ref _estimatedTotalBytes) > LegacyCacheMaxBytes))
            {
                var oldest = _cache.MinBy(pair => Interlocked.Read(ref pair.Value.LastAccessTick));
                if (_cache.TryRemove(oldest.Key, out var removed))
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

    public void Clear()
    {
        foreach (var operation in _gpuInflight.Values)
        {
            operation.Cancel();
        }

        _gpuCache.Clear();
        _cache.Clear();
        _inflight.Clear();
        Interlocked.Exchange(ref _estimatedTotalBytes, 0);
        GpuCacheInvalidated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Device.DeviceLost -= OnDeviceLost;
        foreach (var operation in _gpuInflight.Values)
        {
            operation.Cancel();
        }

        _gpuCache.Dispose();
        _loadGate.Dispose();
    }

    private void OnDeviceLost(CanvasDevice sender, object args)
    {
        _gpuCache.Clear();
        GpuCacheInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private static string EffectiveKey(string key, int decodeWidth)
        => decodeWidth > 0 ? $"{key}#w{decodeWidth}" : key;

    private static string? CacheKey(string? fileIdOrUrl)
    {
        if (string.IsNullOrWhiteSpace(fileIdOrUrl))
        {
            return null;
        }

        var value = fileIdOrUrl.Trim();
        const string marker = "/drive/files/";
        var index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var rest = value[(index + marker.Length)..].Trim('/');
            return rest.Split('?', '#')[0];
        }

        return value;
    }

    public sealed class CanvasBitmapLease : IDisposable
    {
        private LeasedLruCache<string, CanvasBitmap>.Lease? _lease;

        internal CanvasBitmapLease(LeasedLruCache<string, CanvasBitmap>.Lease lease)
        {
            _lease = lease;
        }

        public bool TryGetBitmap(out CanvasBitmap? bitmap)
        {
            var lease = Volatile.Read(ref _lease);
            if (lease is null)
            {
                bitmap = null;
                return false;
            }

            return lease.TryGetValue(out bitmap);
        }

        public void Dispose() => Interlocked.Exchange(ref _lease, null)?.Dispose();
    }

    private sealed class GpuLoadOperation : IDisposable
    {
        private readonly object _gate = new();
        private readonly CancellationTokenSource _cancellation = new();
        private Task<bool>? _task;
        private int _waiters;
        private int _completed;
        private int _disposed;

        public CancellationToken Token => _cancellation.Token;

        public void AddWaiter() => Interlocked.Increment(ref _waiters);

        public Task<bool> GetOrStart(Func<Task<bool>> start)
        {
            lock (_gate)
            {
                return _task ??= start();
            }
        }

        public void ReleaseWaiter()
        {
            if (Interlocked.Decrement(ref _waiters) == 0)
            {
                if (Volatile.Read(ref _completed) == 0)
                {
                    Cancel();
                }
                else
                {
                    Dispose();
                }
            }
        }

        public void MarkCompleted()
        {
            Volatile.Write(ref _completed, 1);
            if (Volatile.Read(ref _waiters) == 0)
            {
                Dispose();
            }
        }

        public void Cancel()
        {
            try
            {
                _cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _cancellation.Dispose();
            }
        }
    }
}
