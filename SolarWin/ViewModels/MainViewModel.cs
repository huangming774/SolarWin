using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>Shell-level state for the authenticated main frame.</summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ISolarApiClient _api;

    public MainViewModel(IAuthService authService, ISolarApiClient api)
    {
        _authService = authService;
        _api = api;
        _authService.AuthenticationStateChanged += (_, _) => RefreshFromAuth();
        RefreshFromAuth();
    }

    [ObservableProperty]
    public partial string? UserDisplayName { get; set; }

    [ObservableProperty]
    public partial string? UserHandle { get; set; }

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    [ObservableProperty]
    public partial string SelectedTag { get; set; } = "home";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnreadNotifications))]
    [NotifyPropertyChangedFor(nameof(NotificationBadgeOpacity))]
    [NotifyPropertyChangedFor(nameof(NotificationBadgeText))]
    public partial int UnreadNotificationCount { get; set; }

    public bool HasUnreadNotifications => UnreadNotificationCount > 0;

    public double NotificationBadgeOpacity => HasUnreadNotifications ? 1.0 : 0.0;

    public string NotificationBadgeText =>
        UnreadNotificationCount > 99 ? "99+" : UnreadNotificationCount.ToString();

    public event EventHandler? LoggedOut;

    public void RefreshFromAuth()
    {
        var account = _authService.CurrentAccount;
        if (account is null || !_authService.IsAuthenticated)
        {
            UserDisplayName = null;
            UserHandle = null;
            StatusText = "未登录";
            UnreadNotificationCount = 0;
            return;
        }

        UserDisplayName = account.Nick ?? account.Name ?? "用户";
        UserHandle = account.Name is null ? null : $"@{account.Name}";
        StatusText = $"Perk {account.PerkLevel}";
    }

    public async Task RefreshNotificationBadgeAsync()
    {
        if (!_authService.IsAuthenticated)
        {
            UnreadNotificationCount = 0;
            return;
        }

        try
        {
            UnreadNotificationCount = Math.Max(0, await _api.GetNotificationCountAsync().ConfigureAwait(true));
        }
        catch (SolarApiException)
        {
            // Keep previous badge value on transient failure.
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync().ConfigureAwait(true);
        LoggedOut?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task RefreshProfileAsync()
    {
        try
        {
            StatusText = "同步中…";
            var me = await _api.GetMeAsync().ConfigureAwait(true);
            UserDisplayName = me.Nick ?? me.Name ?? "用户";
            UserHandle = me.Name is null ? null : $"@{me.Name}";
            StatusText = $"Perk {me.PerkLevel}";
            await RefreshNotificationBadgeAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            StatusText = ex.Message;
        }
    }
}
