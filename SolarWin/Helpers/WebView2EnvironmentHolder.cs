using Microsoft.Web.WebView2.Core;

namespace SolarWin.Helpers;

/// <summary>
/// Shared WebView2 environment for the login-flow dialogs (captcha / WebAuthn).
/// Creating an environment spins up the browser process group; reusing one avoids
/// paying that cost on every dialog open. Browser processes still exit when the
/// last WebView using the environment closes, so idle memory stays at zero.
/// </summary>
public static class WebView2EnvironmentHolder
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static CoreWebView2Environment? _environment;

    public static async Task<CoreWebView2Environment> GetOrCreateAsync()
    {
        if (_environment is not null)
        {
            return _environment;
        }

        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _environment ??= await CoreWebView2Environment.CreateAsync();
            return _environment;
        }
        finally
        {
            Gate.Release();
        }
    }
}
