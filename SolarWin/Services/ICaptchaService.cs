using Microsoft.UI.Xaml;
using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>Fetch captcha config and collect a solved token (hCaptcha via WebView2).</summary>
public interface ICaptchaService
{
    Task<CaptchaConfigResponse> GetConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Show captcha UI owned by <paramref name="xamlRoot"/>.
    /// Returns hCaptcha response token, or null if cancelled.
    /// </summary>
    Task<string?> SolveAsync(XamlRoot xamlRoot, CancellationToken cancellationToken = default);
}
