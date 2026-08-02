using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace SolarWin.Services;

public sealed record LinkPreviewData(
    string Url,
    string SiteName,
    string Title,
    string Description,
    string? ImageUrl);

/// <summary>Loads a small, cached Open Graph preview for public web URLs.</summary>
public sealed class LinkPreviewService
{
    public const string HttpClientName = "LinkPreview";
    private const int MaxHtmlBytes = 512 * 1024;
    private const int MaxRedirects = 3;
    private const int MaxCacheEntries = 256;

    private static readonly Regex MetaTagRegex = new(
        @"<meta\s+[^>]*>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex AttributeRegex = new(
        "(?<name>[\\w:-]+)\\s*=\\s*(?:\"(?<double>[^\"]*)\"|'(?<single>[^']*)'|(?<bare>[^\\s>]+))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TitleRegex = new(
        @"<title(?:\s[^>]*)?>(?<value>[\s\S]*?)</title>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TagRegex = new(
        @"<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<string, Lazy<Task<LinkPreviewData?>>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public LinkPreviewService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public Task<LinkPreviewData?> GetPreviewAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Task.FromResult<LinkPreviewData?>(null);
        }

        if (_cache.Count >= MaxCacheEntries)
        {
            _cache.Clear();
        }

        var key = uri.AbsoluteUri;
        return _cache.GetOrAdd(
            key,
            _ => new Lazy<Task<LinkPreviewData?>>(
                () => LoadPreviewCoreAsync(uri),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private async Task<LinkPreviewData?> LoadPreviewCoreAsync(Uri initialUri)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(7));
        try
        {
            var current = initialUri;
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            HttpResponseMessage? response = null;

            for (var redirect = 0; redirect <= MaxRedirects; redirect++)
            {
                if (!await IsPublicWebUriAsync(current, timeout.Token).ConfigureAwait(false))
                {
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9");
                response = await client.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        timeout.Token)
                    .ConfigureAwait(false);

                if (!IsRedirect(response.StatusCode))
                {
                    break;
                }

                var location = response.Headers.Location;
                response.Dispose();
                response = null;
                if (location is null || redirect == MaxRedirects)
                {
                    return null;
                }

                current = location.IsAbsoluteUri ? location : new Uri(current, location);
            }

            using (response)
            {
                if (response is null || !response.IsSuccessStatusCode)
                {
                    return null;
                }

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (!string.IsNullOrWhiteSpace(mediaType)
                    && !mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var html = await ReadLimitedHtmlAsync(response.Content, timeout.Token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(html))
                {
                    return null;
                }

                return await ParsePreviewAsync(current, html, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   or OperationCanceledException
                                   or IOException
                                   or SocketException)
        {
            return null;
        }
    }

    private static async Task<string?> ReadLimitedHtmlAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream(capacity: 32 * 1024);
        var chunk = new byte[16 * 1024];
        while (buffer.Length <= MaxHtmlBytes)
        {
            var remaining = MaxHtmlBytes + 1 - (int)buffer.Length;
            var read = await stream.ReadAsync(
                    chunk.AsMemory(0, Math.Min(chunk.Length, remaining)),
                    cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        if (buffer.Length > MaxHtmlBytes)
        {
            return null;
        }

        Encoding encoding = Encoding.UTF8;
        var charset = content.Headers.ContentType?.CharSet?.Trim('"', '\'');
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                encoding = Encoding.GetEncoding(charset);
            }
            catch (ArgumentException)
            {
                // Keep UTF-8 fallback.
            }
        }

        return encoding.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static async Task<LinkPreviewData?> ParsePreviewAsync(
        Uri pageUri,
        string html,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match tag in MetaTagRegex.Matches(html))
        {
            string? key = null;
            string? value = null;
            foreach (Match attribute in AttributeRegex.Matches(tag.Value))
            {
                var name = attribute.Groups["name"].Value;
                var attributeValue = attribute.Groups["double"].Success
                    ? attribute.Groups["double"].Value
                    : attribute.Groups["single"].Success
                        ? attribute.Groups["single"].Value
                        : attribute.Groups["bare"].Value;
                if (name.Equals("property", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("name", StringComparison.OrdinalIgnoreCase))
                {
                    key = attributeValue;
                }
                else if (name.Equals("content", StringComparison.OrdinalIgnoreCase))
                {
                    value = attributeValue;
                }
            }

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                metadata.TryAdd(key, CleanText(value, 500));
            }
        }

        var htmlTitle = TitleRegex.Match(html);
        var title = First(metadata, "og:title", "twitter:title")
                    ?? (htmlTitle.Success ? CleanText(htmlTitle.Groups["value"].Value, 160) : null)
                    ?? pageUri.Host;
        var description = First(metadata, "og:description", "twitter:description", "description")
                          ?? string.Empty;
        var siteName = First(metadata, "og:site_name") ?? pageUri.Host;

        string? imageUrl = null;
        var image = First(metadata, "og:image:secure_url", "og:image", "twitter:image");
        if (!string.IsNullOrWhiteSpace(image)
            && Uri.TryCreate(pageUri, WebUtility.HtmlDecode(image), out var imageUri)
            && await IsPublicWebUriAsync(imageUri, cancellationToken).ConfigureAwait(false))
        {
            imageUrl = imageUri.AbsoluteUri;
        }

        return new LinkPreviewData(
            pageUri.AbsoluteUri,
            CleanText(siteName, 80),
            CleanText(title, 160),
            CleanText(description, 320),
            imageUrl);
    }

    private static string? First(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string CleanText(string value, int maxLength)
    {
        var decoded = WebUtility.HtmlDecode(TagRegex.Replace(value, " "));
        var cleaned = WhitespaceRegex.Replace(decoded, " ").Trim();
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength] + "…";
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Moved
            or HttpStatusCode.Redirect
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static async Task<bool> IsPublicWebUriAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.Scheme is not ("http" or "https")
            || !uri.IsDefaultPort
            || string.IsNullOrWhiteSpace(uri.Host)
            || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            return false;
        }

        return addresses.Length > 0 && addresses.All(IsPublicAddress);
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.IsIPv6LinkLocal
            || address.IsIPv6SiteLocal
            || address.IsIPv6Multicast)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return (bytes[0] & 0xFE) != 0xFC;
        }

        return bytes[0] is not (0 or 10 or 127)
               && !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
               && !(bytes[0] == 169 && bytes[1] == 254)
               && !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
               && !(bytes[0] == 192 && bytes[1] == 168)
               && bytes[0] < 224;
    }
}
