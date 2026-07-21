using System.Net;
using System.Text;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// Desktop OIDC: open system browser to Padlock login/{provider}, listen on loopback for returnUrl.
/// </summary>
public sealed class SocialLoginService
{
    private readonly ISolarApiClient _api;

    public SocialLoginService(ISolarApiClient api)
    {
        _api = api;
    }

    /// <summary>
    /// Opens browser and waits for redirect to 127.0.0.1 with token query params.
    /// Returns tokens if present; otherwise throws with guidance.
    /// </summary>
    public async Task<TokenExchangeResponse> LoginWithProviderAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        var port = GetFreePort();
        var redirect = $"http://127.0.0.1:{port}/callback/";
        var deviceId = DeviceInfoHelper.GetDeviceId();
        var loginUrl = _api.BuildSocialLoginUrl(provider, redirect, deviceId);

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();

        try
        {
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri(loginUrl));

            using var reg = cancellationToken.Register(() =>
            {
                try { listener.Stop(); } catch { /* ignore */ }
            });

            // Wait until user completes OAuth and Solian redirects to our loopback.
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }

                var req = ctx.Request;
                var query = req.Url?.Query ?? string.Empty;
                var tokens = TryParseTokens(query, req.Url?.Fragment);

                var html = tokens is not null
                    ? BuildHtmlPage("登录成功", "可以关闭此窗口，返回 SolarWin。")
                    : BuildHtmlPage(
                        "已收到回调",
                        "若应用未自动登录，请回到 SolarWin 查看提示。回调地址：" + (req.Url?.ToString() ?? ""));

                var bytes = Encoding.UTF8.GetBytes(html);
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                ctx.Response.OutputStream.Close();

                if (tokens is not null)
                {
                    return tokens;
                }

                // Keep listening — first hit might be favicon etc.
                if (req.Url?.AbsolutePath.Contains("callback", StringComparison.OrdinalIgnoreCase) == true
                    && string.IsNullOrEmpty(query)
                    && string.IsNullOrEmpty(req.Url.Fragment))
                {
                    // Callback without tokens: Solian may only set cookies on its domain.
                    throw new SolarApiException(
                        "社交登录已回调，但未返回 token。"
                        + " Padlock 可能只支持网页端会话。请改用密码登录、扫码或 OAuth 设备码。");
                }
            }

            throw new OperationCanceledException();
        }
        finally
        {
            try { listener.Stop(); } catch { /* ignore */ }
        }
    }

    private static TokenExchangeResponse? TryParseTokens(string query, string? fragment)
    {
        var combined = query ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(fragment))
        {
            combined += (fragment.StartsWith('#') ? fragment[1..] : fragment);
        }

        if (string.IsNullOrWhiteSpace(combined))
        {
            return null;
        }

        var map = ParseQuery(combined.TrimStart('?', '#'));
        map.TryGetValue("token", out var token);
        map.TryGetValue("access_token", out var accessToken);
        map.TryGetValue("accessToken", out var accessTokenAlt);
        var access = token ?? accessToken ?? accessTokenAlt;
        if (string.IsNullOrWhiteSpace(access))
        {
            return null;
        }

        map.TryGetValue("refresh_token", out var refresh);
        if (string.IsNullOrWhiteSpace(refresh))
        {
            map.TryGetValue("refreshToken", out refresh);
        }

        map.TryGetValue("expires_in", out var expRaw);
        if (string.IsNullOrWhiteSpace(expRaw))
        {
            map.TryGetValue("expiresIn", out expRaw);
        }

        _ = long.TryParse(expRaw, out var exp);
        return new TokenExchangeResponse
        {
            Token = access,
            RefreshToken = refresh,
            ExpiresIn = exp,
        };
    }

    private static Dictionary<string, string> ParseQuery(string q)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(part[..idx]);
            var val = Uri.UnescapeDataString(part[(idx + 1)..]);
            dict[key] = val;
        }

        return dict;
    }

    private static string BuildHtmlPage(string title, string body)
    {
        var t = System.Net.WebUtility.HtmlEncode(title);
        var b = System.Net.WebUtility.HtmlEncode(body);
        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><title>" + t
            + "</title><style>body{font-family:Segoe UI,sans-serif;padding:2rem;}</style></head><body><h2>"
            + t + "</h2><p>" + b + "</p></body></html>";
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
