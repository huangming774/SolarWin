using System.Net;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        OAuthClientId = OAuthSettings.GetClientId();
    }

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
    public partial bool IsBusy { get; set; }

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    public event EventHandler? LoginSucceeded;

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        DeviceUserCode = null;
        DeviceVerificationUrl = null;

        if (string.IsNullOrWhiteSpace(Account) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "请输入账户名和密码。";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "正在登录（Padlock 挑战）…";

            _ = await _authService.LoginAsync(Account.Trim(), Password).ConfigureAwait(true);

            StatusMessage = "登录成功";
            Password = string.Empty;
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
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
        catch (TaskCanceledException)
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
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoginWithOAuthAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        DeviceUserCode = null;
        DeviceVerificationUrl = null;

        if (string.IsNullOrWhiteSpace(OAuthClientId))
        {
            ErrorMessage = "请先填写 OAuth client_id（在 Padlock 注册的公共客户端）。";
            return;
        }

        OAuthSettings.SetClientId(OAuthClientId);

        try
        {
            IsBusy = true;
            StatusMessage = "正在申请设备码…";

            _ = await _authService.LoginWithDeviceCodeAsync(
                    OAuthClientId.Trim(),
                    OnDeviceCodeIssued).ConfigureAwait(true);

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
            IsBusy = false;
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
                return $"请求被拒绝 (400)。\n{server}\n\n若使用密码登录：已改为官方 grant_type=authorization_code；请重试。";
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
            return "请求被拒绝 (400)。请检查账号密码，或改用 OAuth 设备码登录。";
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
