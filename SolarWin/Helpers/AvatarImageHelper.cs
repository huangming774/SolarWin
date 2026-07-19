using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Services;
using Windows.Storage.Streams;

namespace SolarWin.Helpers;

/// <summary>
/// Load avatar images. Tries public URL first; falls back to authenticated HttpClient download.
/// </summary>
public static class AvatarImageHelper
{
    public static BitmapImage? TryCreateBitmap(string? url)
    {
        if (!CloudFileUrlHelper.TryCreateUri(url, out var uri) || uri is null)
        {
            return null;
        }

        try
        {
            var bmp = new BitmapImage();
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.UriSource = uri;
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Download image with Bearer token (for private drive files) and build BitmapImage.
    /// </summary>
    public static async Task<BitmapImage?> LoadAuthenticatedAsync(
        IHttpClientFactory httpClientFactory,
        ITokenStorage tokenStorage,
        string? url,
        CancellationToken cancellationToken = default)
    {
        if (!CloudFileUrlHelper.TryCreateUri(url, out var uri) || uri is null)
        {
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient(SolarApiClient.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var token = await tokenStorage.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
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

            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync().AsTask(cancellationToken).ConfigureAwait(false);
                await writer.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
            }

            stream.Seek(0);
            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
