using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SolarWin.Models;
using SolarWin.Views;

namespace SolarWin.Services;

public sealed class CaptchaService : ICaptchaService
{
    private readonly ISolarApiClient _api;

    public CaptchaService(ISolarApiClient api)
    {
        _api = api;
    }

    public Task<CaptchaConfigResponse> GetConfigAsync(CancellationToken cancellationToken = default)
        => _api.GetCaptchaConfigAsync(cancellationToken);

    public async Task<string?> SolveAsync(XamlRoot xamlRoot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        cancellationToken.ThrowIfCancellationRequested();

        CaptchaConfigResponse config;
        try
        {
            config = await _api.GetCaptchaConfigAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            throw new SolarApiException("无法获取验证码配置：" + ex.Message, inner: ex);
        }

        var siteKey = config.ResolvedSiteKey;
        if (string.IsNullOrWhiteSpace(siteKey))
        {
            throw new SolarApiException("服务器未返回 captcha site key。");
        }

        var dialog = new CaptchaDialog(siteKey)
        {
            XamlRoot = xamlRoot,
        };

        _ = await dialog.ShowAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return dialog.Token;
    }
}
