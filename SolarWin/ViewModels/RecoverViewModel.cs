using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class RecoverViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly ICaptchaService _captcha;
    private readonly IToastService _toast;

    public RecoverViewModel(IAuthService auth, ICaptchaService captcha, IToastService toast)
    {
        _auth = auth;
        _captcha = captcha;
        _toast = toast;
    }

    public XamlRoot? XamlRoot { get; set; }

    [ObservableProperty]
    public partial string Account { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RecoveryCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? CaptchaToken { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    public bool IsNotBusy => !IsBusy;

    public event EventHandler? RecoverSucceeded;

    [RelayCommand]
    private async Task SolveCaptchaAsync()
    {
        ErrorMessage = null;
        if (XamlRoot is null)
        {
            ErrorMessage = "界面未就绪。";
            return;
        }

        try
        {
            IsBusy = true;
            var token = await _captcha.SolveAsync(XamlRoot).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(token))
            {
                StatusMessage = "已取消验证。";
                return;
            }

            CaptchaToken = token;
            StatusMessage = "验证码已完成。";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RecoverAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(Account))
        {
            ErrorMessage = "请输入账户名 / 邮箱。";
            return;
        }

        if (string.IsNullOrWhiteSpace(RecoveryCode))
        {
            ErrorMessage = "请输入恢复码（Recovery code）。";
            return;
        }

        if (string.IsNullOrWhiteSpace(CaptchaToken) && XamlRoot is not null)
        {
            try
            {
                CaptchaToken = await _captcha.SolveAsync(XamlRoot).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(CaptchaToken))
        {
            ErrorMessage = "请先完成人机验证。";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "正在用恢复码登录…";
            var req = new RecoveryRequest
            {
                Account = Account.Trim(),
                RecoveryCode = RecoveryCode.Trim(),
                CaptchaToken = CaptchaToken.Trim(),
                DeviceId = DeviceInfoHelper.GetDeviceId(),
                DeviceName = DeviceInfoHelper.GetDeviceName(),
                Platform = ClientPlatform.Windows,
            };

            _ = await _auth.RecoverAsync(req).ConfigureAwait(true);
            StatusMessage = "恢复成功，已登录。";
            _toast.Success("已通过恢复码登录");
            RecoveryCode = string.Empty;
            CaptchaToken = null;
            RecoverSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.ApiMessage ?? ApiErrorParser.TryGetMessage(ex.ResponseBody) ?? ex.Message;
            CaptchaToken = null;
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = null;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
