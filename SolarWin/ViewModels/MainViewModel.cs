using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>Shell-level state for the authenticated main frame.</summary>
public partial class MainViewModel : ObservableObject
{
    private static readonly TimeSpan ShellProfileCacheDuration = TimeSpan.FromMinutes(30);

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
    public partial string UserInitials { get; set; } = "?";

    [ObservableProperty]
    public partial string? UserAvatarUrl { get; set; }

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
            UserInitials = "?";
            UserAvatarUrl = null;
            StatusText = "未登录";
            UnreadNotificationCount = 0;
            return;
        }

        ApplyAccount(account);
        if (account.Id != Guid.Empty
            && OfflineCache.TryGetJson<SnAccount>(GetShellProfileCacheKey(account.Id), out var cached)
            && cached?.Id == account.Id)
        {
            ApplyAccount(cached);
        }
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
        try
        {
            await _authService.LogoutAsync().ConfigureAwait(false);
        }
        catch
        {
            // still leave locally
        }

        void FinishOnUi()
        {
            if (App.Window is MainWindow mainWindow)
            {
                mainWindow.NavigateToLogin();
            }

            LoggedOut?.Invoke(this, EventArgs.Empty);
        }

        var dq = App.DispatcherQueue;
        if (dq is null || dq.HasThreadAccess)
        {
            FinishOnUi();
        }
        else
        {
            dq.TryEnqueue(FinishOnUi);
        }
    }

    [RelayCommand]
    private async Task RefreshProfileAsync()
    {
        try
        {
            var accountId = _authService.CurrentAccount?.Id;
            if (accountId is { } id
                && id != Guid.Empty
                && OfflineCache.TryGetJson<SnAccount>(GetShellProfileCacheKey(id), out var cached)
                && cached?.Id == id)
            {
                ApplyAccount(cached);
                return;
            }

            StatusText = "同步中…";
            var me = await _api.GetMeAsync().ConfigureAwait(true);
            try
            {
                me.Profile = await _api.GetMyProfileAsync().ConfigureAwait(true);
            }
            catch (SolarApiException)
            {
                // The account endpoint may already contain enough profile data.
            }

            ApplyAccount(me);
            if (me.Id != Guid.Empty)
            {
                try
                {
                    OfflineCache.SetJson(
                        GetShellProfileCacheKey(me.Id),
                        me,
                        ShellProfileCacheDuration);
                }
                catch
                {
                    // Profile display must not fail when the optional disk cache is unavailable.
                }
            }

            await RefreshNotificationBadgeAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            StatusText = ex.Message;
        }
    }

    private void ApplyAccount(SnAccount account)
    {
        var displayName = account.Nick ?? account.Name ?? "用户";
        UserDisplayName = displayName;
        UserHandle = account.Name is null ? null : $"@{account.Name}";
        UserInitials = displayName.Length > 0 ? displayName[..1].ToUpperInvariant() : "?";
        UserAvatarUrl = CloudFileUrlHelper.ResolveAccountAvatar(account);
        StatusText = $"Perk {account.PerkLevel}";
    }

    private static string GetShellProfileCacheKey(Guid accountId)
        => $"shell_profile_{accountId:N}";
}
