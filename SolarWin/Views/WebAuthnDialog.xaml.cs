using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace SolarWin.Views;

/// <summary>
/// Runs a WebAuthn ceremony inside WebView2 on https://solian.app (rp_id).
/// Options JSON + mode (create|get) are injected after navigation.
/// Result is posted back as JSON via chrome.webview.postMessage.
/// </summary>
public sealed partial class WebAuthnDialog : ContentDialog
{
    private readonly string _mode;
    private readonly string _optionsJson;
    private bool _started;
    private bool _closing;

    public WebAuthnDialog(string mode, string optionsJson)
    {
        _mode = mode is "create" or "get" ? mode : "get";
        _optionsJson = optionsJson;
        InitializeComponent();
        PrimaryButtonClick += (_, _) => { ResultJson = null; };
        Opened += OnOpened;
        HintText.Text = _mode == "create"
            ? "注册 Passkey：请在弹出的 Windows Hello / 安全密钥提示中确认。"
            : "使用 Passkey 登录：请确认 Windows Hello 或插入安全密钥。";
    }

    /// <summary>Raw JSON result from the page, or null if cancelled/failed.</summary>
    public string? ResultJson { get; private set; }

    public string? ErrorMessage { get; private set; }

    private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        try
        {
            await WebView.EnsureCoreWebView2Async();
            WebView.CoreWebView2.Settings.IsWebMessageEnabled = true;
            WebView.CoreWebView2.WebMessageReceived += OnWebMessage;
            WebView.CoreWebView2.NavigationCompleted += OnNavCompleted;
            // rp_id is solian.app — WebAuthn only works on that effective domain.
            WebView.Source = new Uri("https://solian.app/");
            StatusText.Text = "正在打开 solian.app 以执行 WebAuthn…";
        }
        catch (Exception ex)
        {
            ErrorMessage = "WebView2 初始化失败：" + ex.Message;
            StatusText.Text = ErrorMessage;
        }
    }

    private async void OnNavCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (_started || !args.IsSuccess)
        {
            if (!args.IsSuccess)
            {
                StatusText.Text = "无法加载 solian.app，Passkey 需要该域名的 WebAuthn 环境。";
            }

            return;
        }

        _started = true;
        try
        {
            // Escape options for JS string
            var opts = JsonSerializer.Serialize(_optionsJson);
            var script = $$"""
(async function() {
  function b64urlToBuf(b64) {
    if (!b64) return undefined;
    b64 = b64.replace(/-/g, '+').replace(/_/g, '/');
    while (b64.length % 4) b64 += '=';
    const bin = atob(b64);
    const buf = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) buf[i] = bin.charCodeAt(i);
    return buf.buffer;
  }
  function bufToB64url(buf) {
    const bytes = new Uint8Array(buf);
    let s = '';
    for (let i = 0; i < bytes.length; i++) s += String.fromCharCode(bytes[i]);
    return btoa(s).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
  }
  try {
    if (!window.PublicKeyCredential) {
      chrome.webview.postMessage(JSON.stringify({ error: '此环境不支持 WebAuthn' }));
      return;
    }
    const raw = JSON.parse({{opts}});
    let publicKey = raw.publicKey || raw.public_key || raw;
    // Normalize challenge / ids
    if (publicKey.challenge) publicKey.challenge = b64urlToBuf(publicKey.challenge);
    if (publicKey.user && publicKey.user.id) publicKey.user.id = b64urlToBuf(publicKey.user.id);
    if (publicKey.allowCredentials) {
      publicKey.allowCredentials = publicKey.allowCredentials.map(c => ({
        ...c,
        id: b64urlToBuf(c.id)
      }));
    }
    if (publicKey.excludeCredentials) {
      publicKey.excludeCredentials = publicKey.excludeCredentials.map(c => ({
        ...c,
        id: b64urlToBuf(c.id)
      }));
    }
    let cred;
    if ('{{_mode}}' === 'create') {
      cred = await navigator.credentials.create({ publicKey });
    } else {
      cred = await navigator.credentials.get({ publicKey });
    }
    if (!cred) {
      chrome.webview.postMessage(JSON.stringify({ error: '用户取消或无凭证' }));
      return;
    }
    const r = cred.response;
    const payload = {
      id: cred.id,
      rawId: bufToB64url(cred.rawId),
      type: cred.type,
      clientDataJSON: bufToB64url(r.clientDataJSON),
      attestationObject: r.attestationObject ? bufToB64url(r.attestationObject) : null,
      authenticatorData: r.authenticatorData ? bufToB64url(r.authenticatorData) : null,
      signature: r.signature ? bufToB64url(r.signature) : null,
      userHandle: r.userHandle ? bufToB64url(r.userHandle) : null
    };
    chrome.webview.postMessage(JSON.stringify(payload));
  } catch (e) {
    chrome.webview.postMessage(JSON.stringify({ error: String(e && e.message ? e.message : e) }));
  }
})();
""";
            await WebView.ExecuteScriptAsync(script);
            StatusText.Text = "请在系统提示中确认 Passkey…";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText.Text = "注入 WebAuthn 脚本失败：" + ex.Message;
        }
    }

    private void OnWebMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            var msg = args.TryGetWebMessageAsString();
            if (string.IsNullOrWhiteSpace(msg))
            {
                return;
            }

            using var doc = JsonDocument.Parse(msg);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                ErrorMessage = err.GetString();
                StatusText.Text = "失败：" + ErrorMessage;
                ResultJson = null;
                return;
            }

            ResultJson = msg;
            StatusText.Text = "Passkey 完成。";
            if (!_closing)
            {
                _closing = true;
                Hide();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText.Text = "解析 WebAuthn 结果失败。";
        }
    }
}
