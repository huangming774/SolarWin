using System.Collections.Concurrent;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Services;
using Windows.Storage.Streams;

namespace SolarWin.Helpers;

/// <summary>
/// Authenticated loader + memory cache for DysonFS files
/// (GET https://api.solian.app/drive/files/{id}).
/// </summary>
public sealed class DysonFileImageLoader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenStorage _tokenStorage;
    private readonly ConcurrentDictionary<string, BitmapImage> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<BitmapImage?>> _inflight = new(StringComparer.OrdinalIgnoreCase);

    public DysonFileImageLoader(IHttpClientFactory httpClientFactory, ITokenStorage tokenStorage)
    {
        _httpClientFactory = httpClientFactory;
        _tokenStorage = tokenStorage;
    }

    public bool TryGetCached(string? fileIdOrUrl, out BitmapImage? image)
    {
        image = null;
        var key = CacheKey(fileIdOrUrl);
        if (key is null)
        {
            return false;
        }

        return _cache.TryGetValue(key, out image);
    }

    public Task<BitmapImage?> LoadAsync(string? fileIdOrUrl, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(fileIdOrUrl);
        if (key is null)
        {
            return Task.FromResult<BitmapImage?>(null);
        }

        if (_cache.TryGetValue(key, out var cached))
        {
            return Task.FromResult<BitmapImage?>(cached);
        }

        var task = _inflight.GetOrAdd(key, _ => LoadCoreAsync(key, cancellationToken));
        return task;
    }

    /// <summary>LoadAsync that never faults; null on any failure. Preferred for UI image binding.</summary>
    public async Task<BitmapImage?> LoadSafeAsync(string? fileIdOrUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            return await LoadAsync(fileIdOrUrl, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private async Task<BitmapImage?> LoadCoreAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
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

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                return null;
            }

            // BitmapImage must be created/set on UI thread in WinUI.
            BitmapImage? result = null;
            var tcs = new TaskCompletionSource<BitmapImage?>();
            var dq = App.DispatcherQueue;
            if (dq is null)
            {
                return null;
            }

            var ok = dq.TryEnqueue(async () =>
            {
                try
                {
                    using var stream = new InMemoryRandomAccessStream();
                    using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                    {
                        writer.WriteBytes(bytes);
                        await writer.StoreAsync();
                        await writer.FlushAsync();
                    }

                    stream.Seek(0);
                    var bmp = new BitmapImage();
                    await bmp.SetSourceAsync(stream);
                    result = bmp;
                    _cache[key] = bmp;
                    tcs.TrySetResult(bmp);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            if (!ok)
            {
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
            _inflight.TryRemove(key, out _);
        }
    }

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
