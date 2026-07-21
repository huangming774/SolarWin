using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;
using SolarWin.Views;

namespace SolarWin.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private CancellationTokenSource? _loginCts;
    private Guid _mfaChallengeId;
    private Guid _selectedFactorId;
    private int _qrUiGeneration;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        OAuthClientId = OAuthSettings.GetClientId();
    }

    /// <summary>Set by LoginPage for WebAuthn dialogs.</summary>
    public XamlRoot? XamlRoot { get; set; }

    public event EventHandler? NavigateToRegister;
    public event EventHandler? NavigateToRecover;

    [ObservableProperty]
    public partial string Account { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OAuthClientId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? DeviceUserCode { get; set; }

    [ObservableProperty]
    public partial string? DeviceVerificationUrl { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    public partial bool IsBusy { get; set; }

    public bool IsNotBusy => !IsBusy;

    public bool CanCancel => IsBusy || IsMfaRequired;

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    // —— MFA ——

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPasswordForm))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    public partial bool IsMfaRequired { get; set; }

    [ObservableProperty]
    public partial string? MfaHint { get; set; }

    [ObservableProperty]
    public partial string FactorCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedFactorLabel { get; set; }

    [ObservableProperty]
    public partial bool CanRequestCode { get; set; }

    [ObservableProperty]
    public partial bool ShowInAppWait { get; set; }

    public ObservableCollection<FactorOptionItem> FactorOptions { get; } = [];

    // —— QR ——

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPasswordForm))]
    public partial bool IsQrMode { get; set; }

    [ObservableProperty]
    public partial string? QrData { get; set; }

    [ObservableProperty]
    public partial string? QrStatusText { get; set; }

    /// <summary>Rendered QR for phone camera scan (solian://auth/qr/…).</summary>
    [ObservableProperty]
    public partial BitmapImage? QrImage { get; set; }

    /// <summary>Primary password/OAuth form visible when not in MFA / QR mode.</summary>
    public bool ShowPasswordForm => !IsMfaRequired && !IsQrMode;

    public event EventHandler? LoginSucceeded;

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        DeviceUserCode = null;
        DeviceVerificationUrl = null;
        ClearQrUi();

        if (string.IsNullOrWhiteSpace(Account) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "请输入账户名和密码。";
            return;
        }

        using var cts = BeginBusy();
        try
        {
            StatusMessage = "正在登录（Padlock 挑战）…";

            _ = await _authService.LoginAsync(Account.Trim(), Password, cts.Token).ConfigureAwait(true);

            StatusMessage = "登录成功";
            Password = string.Empty;
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (MultiFactorRequiredException mfa)
        {
            EnterMfa(mfa);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            ErrorMessage = "已取消登录。";
            StatusMessage = null;
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = MapApiError(ex);
            StatusMessage = null;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "网络错误，请检查网络连接后重试。";
            StatusMessage = null;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "请求超时，请稍后重试。";
            StatusMessage = null;
        }
        catch (Exception)
        {
            ErrorMessage = "登录失败，请稍后重试。";
            StatusMessage = null;
        }
        finally
        {
            EndBusy(cts);
        }
    }

    [RelayCommand]
    private async Task SubmitFactorAsync()
    {
        ErrorMessage = null;

        if (_mfaChallengeId == Guid.Empty || _selectedFactorId == Guid.Empty)
        {
            ErrorMessage = "请选择验证方式。";
            return;
        }

        if (string.IsNullOrWhiteSpace(FactorCode))
        {
            ErrorMessage = "请输入验证码。";
            return;
        }

        using var cts = BeginBusy();
        try
        {
            StatusMessage = "正在提交验证…";
            _ = await _authService
                .CompleteFactorAsync(_mfaChallengeId, _selectedFactorId, FactorCode.Trim(), cts.Token)
                .ConfigureAwait(true);

            ClearMfa();
            Password = string.Empty;
            StatusMessage = "登录成功";
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (MultiFactorRequiredException mfa)
        {
            EnterMfa(mfa);
            StatusMessage = "还需要一步验证。";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "已取消。";
            StatusMessage = null;
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = MapApiError(ex);
            StatusMessage = null;
        }
        catch (Exception)
        {
            ErrorMessage = "验证失败，请重试。";
            StatusMessage = null;
        }
        finally
        {
            EndBusy(cts);
        }
    }

    [RelayCommand]
    private async Task RequestFactorCodeAsync()
    {
        ErrorMessage = null;
        if (_mfaChallengeId == Guid.Empty || _selectedFactorId == Guid.Empty)
        {
            return;
        }

        using var cts = BeginBusy();
        try
        {
            StatusMessage = "正在发送验证码…";
            await _authService.RequestFactorCodeAsync(_mfaChallengeId, _selectedFactorId, cts.Token)
                .ConfigureAwait(true);
            StatusMessage = "验证码已发送，请查收邮箱或短信。";
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = MapApiError(ex);
            StatusMessage = null;
        }
        catch (Exception)
        {
            ErrorMessage = "发送验证码失败。";
            StatusMessage = null;
        }
        finally
        {
            EndBusy(cts);
        }
    }

    [RelayCommand]
    private async Task WaitInAppApprovalAsync()
    {
        ErrorMessage = null;
        if (_mfaChallengeId == Guid.Empty)
        {
            return;
        }

        using var cts = BeginBusy();
        try
        {
            StatusMessage = "请在已登录的设备上批准本次登录…";
            _ = await _authService.WaitForChallengeCompletionAsync(_mfaChallengeId, cts.Token)
                .ConfigureAwait(true);

            ClearMfa();
            Password = string.Empty;
            StatusMessage = "登录成功";
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "已取消等待。";
            StatusMessage = null;
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = MapApiError(ex);
            StatusMessage = null;
        }
        catch (Exception)
        {
            ErrorMessage = "等待确认失败。";
            StatusMessage = null;
        }
        finally
        {
            EndBusy(cts);
        }
    }

    [RelayCommand]
    private void SelectFactor(FactorOptionItem? item)
    {
        if (item is null)
        {
            return;
        }

        _selectedFactorId = item.Id;
        SelectedFactorLabel = item.Label;
        CanRequestCode = item.CanRequestCode;
        ShowInAppWait = item.IsInApp;
        FactorCode = string.Empty;
        StatusMessage = item.IsInApp
            ? "请在已登录设备上确认，或点击下方「等待确认」。"
            : item.CanRequestCode
                ? "可先点击「发送验证码」，再输入收到的代码。"
                : "请输入验证器中的 6 位数字或恢复码。";
    }

    [RelayCommand]
    private async Task LoginWithOAuthAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        DeviceUserCode = null;
        DeviceVerificationUrl = null;
        ClearMfa();
        ClearQrUi();

        if (string.IsNullOrWhiteSpace(OAuthClientId))
        {
            ErrorMessage = "请先填写 OAuth client_id（在 Padlock 注册的公共客户端）。";
            return;
        }

        OAuthSettings.SetClientId(OAuthClientId);

        using var cts = BeginBusy();
        try
        {
            StatusMessage = "正在申请设备码…";

            _ = await _authService.LoginWithDeviceCodeAsync(
                    OAuthClientId.Trim(),
                    OnDeviceCodeIssued,
                    cts.Token).ConfigureAwait(true);

            StatusMessage = "OAuth 登录成功";
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "已取消 OAuth 登录。";
            StatusMessage = null;
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = MapApiError(ex);
            StatusMessage = null;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "网络错误，请检查网络连接后重试。";
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(ex.Message) ? "OAuth 登录失败。" : ex.Message;
            StatusMessage = null;
        }
        finally
        {
            EndBusy(cts);
            DeviceUserCode = null;
            DeviceVerificationUrl = null;
        }
    }

    [RelayCommand]
    private async Task LoginWithQrAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        DeviceUserCode = null;
        DeviceVerificationUrl = null;
        ClearMfa();

        using var cts = BeginBusy();
        try
        {
            RunOnUi(() =>
            {
                IsQrMode = true;
                QrImage = null;
                QrData = null;
                QrStatusText = "正在生成二维码…";
                StatusMessage = "请使用已登录的手机 Solar / Solian App 扫描二维码";
            });

            _ = await _authService.LoginWithQrAsync(OnQrGenerated, cts.Token).ConfigureAwait(true);

            RunOnUi(() =>
            {
                ClearQrUi();
                StatusMessage = "扫码登录成功";
            });
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            RunOnUi(() =>
            {
                ErrorMessage = "已取消扫码登录。";
                StatusMessage = null;
                ClearQrUi();
            });
        }
        catch (SolarApiException ex)
        {
            RunOnUi(() =>
            {
                ErrorMessage = MapApiError(ex);
                StatusMessage = null;
                ClearQrUi();
            });
        }
        catch (Exception ex)
        {
            RunOnUi(() =>
            {
                ErrorMessage = string.IsNullOrWhiteSpace(ex.Message) ? "扫码登录失败。" : ex.Message;
                StatusMessage = null;
                ClearQrUi();
            });
        }
        finally
        {
            EndBusy(cts);
        }
    }

    [RelayCommand]
    private void CancelLogin()
    {
        try
        {
            _loginCts?.Cancel();
        }
        catch
        {
            // ignore
        }

        ClearMfa();
        ClearQrUi();
        DeviceUserCode = null;
        DeviceVerificationUrl = null;
        StatusMessage = null;
        ErrorMessage = "已取消。";
        IsBusy = false;
    }

    [RelayCommand]
    private void GoRegister() => NavigateToRegister?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void GoRecover() => NavigateToRecover?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task LoginWithPasskeyAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        ClearMfa();
        ClearQrUi();

        if (XamlRoot is null)
        {
            ErrorMessage = "界面未就绪，无法打开 Passkey。";
            return;
        }

        using var cts = BeginBusy();
        try
        {
            StatusMessage = "Passkey 登录：准备 WebAuthn…";
            _ = await _authService.LoginWithPasskeyAsync(RunPasskeyGetCeremonyAsync, cts.Token)
                .ConfigureAwait(true);
            StatusMessage = "Passkey 登录成功";
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (MultiFactorRequiredException mfa)
        {
            EnterMfa(mfa);
            StatusMessage = "Passkey 通过，还需额外验证。";
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "已取消 Passkey 登录。";
            StatusMessage = null;
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = MapApiError(ex);
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(ex.Message) ? "Passkey 登录失败。" : ex.Message;
            StatusMessage = null;
        }
        finally
        {
            EndBusy(cts);
        }
    }

    [RelayCommand]
    private async Task LoginWithSocialAsync(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        ClearMfa();
        ClearQrUi();

        using var cts = BeginBusy();
        try
        {
            StatusMessage = $"正在打开 {SocialLoginProviders.DisplayName(provider)} 登录…请在浏览器完成授权。";
            _ = await _authService.LoginWithSocialAsync(provider, cts.Token).ConfigureAwait(true);
            StatusMessage = "社交登录成功";
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "已取消社交登录。";
            StatusMessage = null;
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = MapApiError(ex);
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = null;
        }
        finally
        {
            EndBusy(cts);
        }
    }

    private async Task<string?> RunPasskeyGetCeremonyAsync(string optionsJson, CancellationToken cancellationToken)
    {
        if (XamlRoot is null)
        {
            return null;
        }

        var dialog = new WebAuthnDialog("get", optionsJson) { XamlRoot = XamlRoot };
        _ = await dialog.ShowAsync().AsTask(cancellationToken).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(dialog.ErrorMessage) && string.IsNullOrWhiteSpace(dialog.ResultJson))
        {
            throw new SolarApiException(dialog.ErrorMessage);
        }

        return dialog.ResultJson;
    }

    private void EnterMfa(MultiFactorRequiredException mfa)
    {
        IsMfaRequired = true;
        _mfaChallengeId = mfa.ChallengeId;
        FactorOptions.Clear();
        FactorCode = string.Empty;

        foreach (var f in mfa.Factors)
        {
            FactorOptions.Add(new FactorOptionItem(f));
        }

        MfaHint =
            $"需要额外验证（剩余 {mfa.Challenge.StepRemain}/{Math.Max(1, mfa.Challenge.StepTotal)} 步）。请选择一种方式：";

        var first = FactorOptions.FirstOrDefault();
        if (first is not null)
        {
            SelectFactor(first);
        }
        else
        {
            SelectedFactorLabel = null;
            CanRequestCode = false;
            ShowInAppWait = false;
            ErrorMessage = "没有可用的第二因素。请在其他设备上批准，或联系支持。";
        }

        StatusMessage = null;
        OnPropertyChanged(nameof(ShowPasswordForm));
    }

    private void ClearMfa()
    {
        IsMfaRequired = false;
        _mfaChallengeId = Guid.Empty;
        _selectedFactorId = Guid.Empty;
        FactorOptions.Clear();
        FactorCode = string.Empty;
        MfaHint = null;
        SelectedFactorLabel = null;
        CanRequestCode = false;
        ShowInAppWait = false;
        OnPropertyChanged(nameof(ShowPasswordForm));
    }

    private void ClearQrUi()
    {
        _qrUiGeneration++;
        IsQrMode = false;
        QrData = null;
        QrStatusText = null;
        QrImage = null;
        OnPropertyChanged(nameof(ShowPasswordForm));
    }

    private void OnQrGenerated(QrGenerateResponse qr)
    {
        var payload = string.IsNullOrWhiteSpace(qr.QrData)
            ? $"solian://auth/qr/{qr.QrChallengeId:D}"
            : qr.QrData.Trim();

        var expires = qr.ExpiresAt?.ToLocalTime().ToString("t")
            ?? (qr.ExpiresInSeconds > 0 ? $"{qr.ExpiresInSeconds} 秒后" : "—");

        // Encode PNG off the UI thread; assign BitmapImage on the UI thread.
        byte[]? png = null;
        try
        {
            png = QrCodeImageHelper.CreatePng(payload);
        }
        catch
        {
            // Image render failed — still show raw payload for debugging.
        }

        var generation = ++_qrUiGeneration;
        var pngBytes = png;

        RunOnUi(() =>
        {
            if (generation != _qrUiGeneration)
            {
                return;
            }

            IsQrMode = true;
            QrData = payload;
            QrStatusText = $"请用手机 App 扫描下方二维码（有效至 {expires}）";
            StatusMessage = "等待手机确认扫码登录…";
            OnPropertyChanged(nameof(ShowPasswordForm));

            if (pngBytes is null)
            {
                QrImage = null;
                ErrorMessage = "二维码图像生成失败，可尝试复制下方链接到手机打开。";
                return;
            }

            _ = ApplyQrImageAsync(generation, pngBytes);
        });
    }

    private async Task ApplyQrImageAsync(int generation, byte[] pngBytes)
    {
        try
        {
            var image = await QrCodeImageHelper.ToBitmapImageAsync(pngBytes).ConfigureAwait(true);
            if (generation != _qrUiGeneration)
            {
                return;
            }

            QrImage = image;
        }
        catch (Exception ex)
        {
            if (generation != _qrUiGeneration)
            {
                return;
            }

            QrImage = null;
            ErrorMessage = "二维码图像显示失败：" + ex.Message;
        }
    }

    private static void RunOnUi(Action action)
    {
        var dq = App.DispatcherQueue;
        if (dq is null || dq.HasThreadAccess)
        {
            action();
            return;
        }

        // Prefer sync wait so callers that just finished ConfigureAwait(false) still get UI updated
        // before the next line. Fall back to fire-and-forget if queue is busy.
        var done = new ManualResetEventSlim(false);
        Exception? error = null;
        if (!dq.TryEnqueue(DispatcherQueuePriority.Normal, () =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    done.Set();
                }
            }))
        {
            action();
            return;
        }

        if (!done.Wait(TimeSpan.FromSeconds(5)))
        {
            // Timed out waiting for UI — run inline as last resort.
            action();
            return;
        }

        if (error is not null)
        {
            throw error;
        }
    }

    private void OnDeviceCodeIssued(DeviceAuthorizationResponse device)
    {
        DeviceUserCode = device.UserCode;
        DeviceVerificationUrl = device.VerificationUriComplete
            ?? device.VerificationUri
            ?? "https://id.solian.app";
        StatusMessage =
            $"请在浏览器打开：{DeviceVerificationUrl}\n输入代码：{DeviceUserCode}\n等待授权中…";

        // Best-effort open browser
        try
        {
            var url = DeviceVerificationUrl;
            if (!string.IsNullOrWhiteSpace(url))
            {
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri(url));
            }
        }
        catch
        {
            // ignore
        }
    }

    private CancellationTokenSource BeginBusy()
    {
        _loginCts?.Cancel();
        _loginCts?.Dispose();
        _loginCts = new CancellationTokenSource();
        IsBusy = true;
        return _loginCts;
    }

    private void EndBusy(CancellationTokenSource cts)
    {
        if (ReferenceEquals(_loginCts, cts))
        {
            IsBusy = false;
        }
    }

    private static string MapApiError(SolarApiException ex)
    {
        // Prefer parsed server message from ApiMessage / ResponseBody.
        var server = ex.ApiMessage ?? ApiErrorParser.TryGetMessage(ex.ResponseBody);

        if (!string.IsNullOrWhiteSpace(server))
        {
            if (server.Contains("Account was not found", StringComparison.OrdinalIgnoreCase)
                || server.Contains("AUTH_ACCOUNT_NOT_FOUND", StringComparison.OrdinalIgnoreCase))
            {
                return "账户不存在。";
            }

            if (server.Contains("password", StringComparison.OrdinalIgnoreCase)
                || server.Contains("credential", StringComparison.OrdinalIgnoreCase)
                || server.Contains("AUTH_FACTOR", StringComparison.OrdinalIgnoreCase))
            {
                return $"密码错误或验证失败。\n{server}";
            }

            if (server.Contains("Client not found", StringComparison.OrdinalIgnoreCase)
                || server.Contains("unauthorized_client", StringComparison.OrdinalIgnoreCase))
            {
                return "OAuth client_id 无效：客户端未注册。请到 Padlock 创建公共应用并填写正确的 client_id。";
            }

            // Full message including HTTP context when useful
            if (ex.StatusCode is HttpStatusCode.BadRequest)
            {
                return $"请求被拒绝 (400)。\n{server}";
            }

            return server;
        }

        if (ex.Message.Contains("账户不存在", StringComparison.Ordinal)
            || ex.StatusCode is HttpStatusCode.NotFound)
        {
            return "账户不存在。";
        }

        if (ex.Message.Contains("密码错误", StringComparison.Ordinal)
            || ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return "密码错误。";
        }

        if (ex.StatusCode is HttpStatusCode.BadRequest)
        {
            return "请求被拒绝 (400)。请检查账号密码，或改用扫码 / OAuth 登录。";
        }

        if (ex.StatusCode is HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or HttpStatusCode.RequestTimeout)
        {
            return "网络错误，服务器暂时不可用。";
        }

        if (!string.IsNullOrWhiteSpace(ex.Message)
            && !ex.Message.Contains("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return ex.Message;
        }

        return "登录失败，请检查账户与密码。";
    }
}

/// <summary>UI option for a challenge factor during MFA.</summary>
public sealed class FactorOptionItem
{
    public FactorOptionItem(SnAccountAuthFactor factor)
    {
        Id = factor.Id;
        Type = factor.Type;
        Label = factor.TypeDisplayName;
        CanRequestCode = factor.Type is AccountAuthFactorType.Email or AccountAuthFactorType.PhoneCode;
        IsInApp = factor.Type is AccountAuthFactorType.InApp;
    }

    public Guid Id { get; }

    public AccountAuthFactorType Type { get; }

    public string Label { get; }

    public bool CanRequestCode { get; }

    public bool IsInApp { get; }
}
