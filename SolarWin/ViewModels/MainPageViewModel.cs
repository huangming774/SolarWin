using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    public MainPageViewModel(IAuthService authService)
    {
        _authService = authService;
        RefreshAccountDisplay();
        _authService.AuthenticationStateChanged += (_, _) => RefreshAccountDisplay();
    }

    [ObservableProperty]
    public partial string Greeting { get; set; } = "Solar Network";

    [ObservableProperty]
    public partial string AccountSummary { get; set; } = "未登录";

    public event EventHandler? LoggedOut;

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync().ConfigureAwait(true);
        LoggedOut?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshAccountDisplay()
    {
        var account = _authService.CurrentAccount;
        if (account is null || !_authService.IsAuthenticated)
        {
            Greeting = "Solar Network";
            AccountSummary = "未登录";
            return;
        }

        Greeting = $"你好，{account.Nick ?? account.Name ?? "用户"}";
        AccountSummary = $"@{account.Name} · Lv{account.Profile?.Level ?? 0} · Perk {account.PerkLevel}";
    }
}
