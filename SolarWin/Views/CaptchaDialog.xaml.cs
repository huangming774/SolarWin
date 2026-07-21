using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace SolarWin.Views;

public sealed partial class CaptchaDialog : ContentDialog
{
    private readonly string _siteKey;
    private string? _token;
    private bool _closing;

    public CaptchaDialog(string siteKey)
    {
        _siteKey = siteKey;
        InitializeComponent();
        PrimaryButtonClick += OnPrimaryCancel;
        Opened += OnOpened;
    }

    /// <summary>Solved captcha token, or null if cancelled.</summary>
    public string? Token => _token;

    private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        try
        {
            await WebView.EnsureCoreWebView2Async();
            WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
            WebView.CoreWebView2.WebMessageReceived += OnWebMessage;
            WebView.CoreWebView2.NavigationCompleted += (_, e) =>
            {
                if (!e.IsSuccess)
                {
                    StatusText.Text = "验证页加载失败，请使用下方手动 token 或重试。";
                }
            };

            var html = BuildHtml(_siteKey);
            WebView.NavigateToString(html);
            StatusText.Text = "请完成下方 hCaptcha 验证…";
        }
        catch (Exception ex)
        {
            StatusText.Text = "WebView2 初始化失败：" + ex.Message + "。请粘贴 token。";
        }
    }

    private void OnWebMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var raw = args.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            // Prefer plain token string; also accept { "token": "..." }
            var token = raw.Trim();
            if (token.StartsWith('{'))
            {
                using var doc = JsonDocument.Parse(token);
                if (doc.RootElement.TryGetProperty("token", out var t))
                {
                    token = t.GetString() ?? string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            _token = token;
            StatusText.Text = "验证成功。";
            if (!_closing)
            {
                _closing = true;
                Hide();
            }
        }
        catch
        {
            // ignore parse errors
        }
    }

    private void UseManual_OnClick(object sender, RoutedEventArgs e)
    {
        var manual = ManualTokenBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(manual))
        {
            StatusText.Text = "请输入 token。";
            return;
        }

        _token = manual;
        _closing = true;
        Hide();
    }

    private void OnPrimaryCancel(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        _token = null;
    }

    private static string BuildHtml(string siteKey)
    {
        var key = siteKey.Replace("\"", "", StringComparison.Ordinal);
        // hCaptcha posts token to host via chrome.webview.postMessage
        return $$"""
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <script src="https://js.hcaptcha.com/1/api.js" async defer></script>
  <style>
    body { font-family: 'Segoe UI', sans-serif; margin: 16px; background: #f6f6f6; color: #111; }
    .box { background: #fff; padding: 16px; border-radius: 12px; }
  </style>
</head>
<body>
  <div class="box">
    <p>请完成人机验证</p>
    <div class="h-captcha" data-sitekey="{{key}}" data-callback="onCaptcha"></div>
  </div>
  <script>
    function onCaptcha(token) {
      try {
        if (window.chrome && window.chrome.webview) {
          window.chrome.webview.postMessage(token);
        }
      } catch (e) {}
    }
  </script>
</body>
</html>
""";
    }
}
