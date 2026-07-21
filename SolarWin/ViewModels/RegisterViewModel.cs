using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly ICaptchaService _captcha;
    private readonly IToastService _toast;

    public RegisterViewModel(IAuthService auth, ICaptchaService captcha, IToastService toast)
    {
        _auth = auth;
        _captcha = captcha;
        _toast = toast;
    }

    /// <summary>Must be set by the page (for captcha dialog).</summary>
    public XamlRoot? XamlRoot { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Nick { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PasswordConfirm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AffiliationSpell { get; set; } = string.Empty;

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

    public event EventHandler<string>? Registered;

    [RelayCommand]
    private async Task SolveCaptchaAsync()
    {
        ErrorMessage = null;
        if (XamlRoot is null)
        {
            ErrorMessage = "界面未就绪，无法打开验证码。";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "打开 hCaptcha…";
            var token = await _captcha.SolveAsync(XamlRoot).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(token))
            {
                StatusMessage = "已取消验证。";
                return;
            }

            CaptchaToken = token;
            StatusMessage = "验证码已完成。";
            _toast.Success("人机验证完成");
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

    [RelayCommand]
    private async Task RegisterAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(Name) || Name.Trim().Length < 2)
        {
            ErrorMessage = "用户名至少 2 位，仅字母数字下划线短横线。";
            return;
        }

        if (string.IsNullOrWhiteSpace(Nick))
        {
            ErrorMessage = "请填写昵称。";
            return;
        }

        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@', StringComparison.Ordinal))
        {
            ErrorMessage = "请填写有效邮箱。";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 4)
        {
            ErrorMessage = "密码至少 4 位。";
            return;
        }

        if (!string.Equals(Password, PasswordConfirm, StringComparison.Ordinal))
        {
            ErrorMessage = "两次输入的密码不一致。";
            return;
        }

        if (string.IsNullOrWhiteSpace(CaptchaToken) && XamlRoot is not null)
        {
            try
            {
                StatusMessage = "需要人机验证…";
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
            StatusMessage = "正在注册…";
            var req = new AccountCreateRequest
            {
                Name = Name.Trim(),
                Nick = Nick.Trim(),
                Email = Email.Trim(),
                Password = Password,
                CaptchaToken = CaptchaToken.Trim(),
                Language = "zh-CN",
                AffiliationSpell = string.IsNullOrWhiteSpace(AffiliationSpell) ? null : AffiliationSpell.Trim(),
            };

            var account = await _auth.RegisterAsync(req).ConfigureAwait(true);
            StatusMessage = "注册成功，请使用密码登录。";
            _toast.Success("注册成功");
            Password = string.Empty;
            PasswordConfirm = string.Empty;
            CaptchaToken = null;
            Registered?.Invoke(this, account.Name ?? Name.Trim());
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.ApiMessage ?? ApiErrorParser.TryGetMessage(ex.ResponseBody) ?? ex.Message;
            // Captcha often single-use
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
