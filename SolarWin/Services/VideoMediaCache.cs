using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using SolarWin.Helpers;

namespace SolarWin.Services;

/// <summary>
/// Authenticated on-disk video cache plus native Media Foundation/D3D11 thumbnail extraction.
/// Videos are never decoded on the UI thread.
/// </summary>
public sealed class VideoMediaCache
{
    private const long MaxCachedVideoBytes = 1024L * 1024 * 1024;
    private const long MaxCacheBytes = 2L * 1024 * 1024 * 1024;
    private const int MaxQueuedThumbnailOperations = 24;
    private static readonly TimeSpan MaxCacheAge = TimeSpan.FromDays(14);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenStorage _tokenStorage;
    private readonly IAccountSessionService _accountSession;
    private readonly ConcurrentDictionary<string, SharedOperation> _inflight = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _thumbnailQueueSlots = new(MaxQueuedThumbnailOperations, MaxQueuedThumbnailOperations);
    private readonly SemaphoreSlim _thumbnailWorkGate = new(2, 2);
    private readonly string _videoRoot;
    private readonly string _thumbnailRoot;

    public VideoMediaCache(
        IHttpClientFactory httpClientFactory,
        ITokenStorage tokenStorage,
        IAccountSessionService accountSession)
    {
        _httpClientFactory = httpClientFactory;
        _tokenStorage = tokenStorage;
        _accountSession = accountSession;
        _videoRoot = Path.Combine(AppPaths.CacheDirectory, "videos");
        _thumbnailRoot = Path.Combine(AppPaths.CacheDirectory, "video-thumbnails");
        Directory.CreateDirectory(_videoRoot);
        Directory.CreateDirectory(_thumbnailRoot);
        _ = Task.Run(MaintainCache);
    }

    public async Task<string?> GetThumbnailAsync(
        string source,
        string? fileName,
        string? mimeType,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        var url = NormalizeSource(source);
        if (url is null) return null;

        var key = Hash(url);
        var thumbnailDirectory = GetScopedDirectory(_thumbnailRoot);
        var outputPath = Path.Combine(thumbnailDirectory, $"{key}-{width}x{height}.png");
        if (IsUsable(outputPath)) return Touch(outputPath);

        return await RunSharedAsync(
            $"thumb:{key}:{width}x{height}",
            async sharedToken =>
            {
                if (IsUsable(outputPath)) return Touch(outputPath);
                if (!await _thumbnailQueueSlots.WaitAsync(0, sharedToken).ConfigureAwait(false))
                {
                    // A rapidly scrolled feed must not create an unbounded thumbnail backlog.
                    return null;
                }

                try
                {
                    await _thumbnailWorkGate.WaitAsync(sharedToken).ConfigureAwait(false);
                    try
                    {
                        var localVideo = await GetLocalVideoAsync(url, fileName, mimeType, sharedToken).ConfigureAwait(false);
                        if (localVideo is null) return null;

                        var generated = await NativeVideoThumbnailer.GenerateAsync(
                            localVideo, outputPath, width, height, TimeSpan.FromSeconds(1), sharedToken).ConfigureAwait(false);
                        return generated ? outputPath : null;
                    }
                    finally
                    {
                        _thumbnailWorkGate.Release();
                    }
                }
                finally
                {
                    _thumbnailQueueSlots.Release();
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetLocalVideoAsync(
        string source,
        string? fileName,
        string? mimeType,
        CancellationToken cancellationToken = default)
    {
        var url = NormalizeSource(source);
        if (url is null) return null;
        if (Path.IsPathRooted(url) && File.Exists(url)) return url;

        var key = Hash(url);
        var extension = ResolveExtension(fileName, mimeType, url);
        var outputPath = Path.Combine(GetScopedDirectory(_videoRoot), key + extension);
        if (IsUsable(outputPath)) return Touch(outputPath);

        return await RunSharedAsync("video:" + key, async sharedToken =>
        {
            if (IsUsable(outputPath)) return Touch(outputPath);
            var tempPath = outputPath + ".download-" + Guid.NewGuid().ToString("N");
            try
            {
                var client = _httpClientFactory.CreateClient(SolarApiClient.HttpClientName);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                var token = await _tokenStorage.GetAccessTokenAsync(sharedToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(token))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                using var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, sharedToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > MaxCachedVideoBytes)
                    return null;

                await using var input = await response.Content.ReadAsStreamAsync(sharedToken).ConfigureAwait(false);
                await using var output = new FileStream(
                    tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var buffer = new byte[128 * 1024];
                long total = 0;
                while (true)
                {
                    var read = await input.ReadAsync(buffer.AsMemory(), sharedToken).ConfigureAwait(false);
                    if (read == 0) break;
                    total += read;
                    if (total > MaxCachedVideoBytes) return null;
                    await output.WriteAsync(buffer.AsMemory(0, read), sharedToken).ConfigureAwait(false);
                }

                await output.FlushAsync(sharedToken).ConfigureAwait(false);
                output.Close();
                if (total == 0) return null;
                File.Move(tempPath, outputPath, overwrite: true);
                return outputPath;
            }
            finally
            {
                TryDelete(tempPath);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> RunSharedAsync(
        string key,
        Func<CancellationToken, Task<string?>> factory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SharedOperation? candidate = null;
        candidate = new SharedOperation(
            token => RunAndRemoveAsync(key, candidate!, factory, token));
        var operation = _inflight.GetOrAdd(key, candidate);
        if (!ReferenceEquals(operation, candidate))
        {
            candidate.DisposeUnused();
        }

        return await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> RunAndRemoveAsync(
        string key,
        SharedOperation owner,
        Func<CancellationToken, Task<string?>> factory,
        CancellationToken cancellationToken)
    {
        try
        {
            return await factory(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _inflight.TryRemove(new KeyValuePair<string, SharedOperation>(key, owner));
            owner.MarkCompleted();
        }
    }

    /// <summary>
    /// Deduplicates identical work while allowing the underlying operation to stop once every
    /// interested page/container has canceled its wait.
    /// </summary>
    private sealed class SharedOperation
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Lazy<Task<string?>> _task;
        private int _waiterCount;
        private int _completed;

        public SharedOperation(Func<CancellationToken, Task<string?>> factory)
        {
            _task = new Lazy<Task<string?>>(
                () => factory(_cancellation.Token),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public async Task<string?> WaitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _waiterCount);
            try
            {
                return await _task.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (Interlocked.Decrement(ref _waiterCount) == 0
                    && Volatile.Read(ref _completed) == 0)
                {
                    try { _cancellation.Cancel(); }
                    catch (ObjectDisposedException) { }
                }
            }
        }

        public void MarkCompleted() => Interlocked.Exchange(ref _completed, 1);

        public void DisposeUnused() => _cancellation.Dispose();
    }

    private static string? NormalizeSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        var trimmed = source.Trim();
        if (Path.IsPathRooted(trimmed)) return trimmed;
        return trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : CloudFileUrlHelper.DriveFileUrl(trimmed);
    }

    private static string ResolveExtension(string? fileName, string? mimeType, string url)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            extension = Path.GetExtension(uri.AbsolutePath);
        if (!string.IsNullOrWhiteSpace(extension) && extension.Length <= 8) return extension.ToLowerInvariant();
        return mimeType?.ToLowerInvariant() switch
        {
            "video/webm" => ".webm",
            "video/quicktime" => ".mov",
            "video/x-matroska" => ".mkv",
            "video/x-msvideo" => ".avi",
            _ => ".mp4",
        };
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool IsUsable(string path)
    {
        try { return File.Exists(path) && new FileInfo(path).Length > 0; }
        catch { return false; }
    }

    private static string Touch(string path)
    {
        try { File.SetLastWriteTimeUtc(path, DateTime.UtcNow); } catch { }
        return path;
    }

    private void MaintainCache()
    {
        try
        {
            var files = new DirectoryInfo(_videoRoot).EnumerateFiles("*", SearchOption.AllDirectories)
                .Concat(new DirectoryInfo(_thumbnailRoot).EnumerateFiles("*", SearchOption.AllDirectories))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();
            long retained = 0;
            foreach (var file in files)
            {
                if (DateTime.UtcNow - file.LastWriteTimeUtc > MaxCacheAge || retained + file.Length > MaxCacheBytes)
                    TryDelete(file.FullName);
                else
                    retained += file.Length;
            }
        }
        catch
        {
            // Cache maintenance must never affect app startup.
        }
    }

    private string GetScopedDirectory(string root)
    {
        var account = _accountSession.ActiveAccountId is { } id && id != Guid.Empty
            ? id.ToString("N")
            : "anonymous";
        var path = Path.Combine(root, account);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
