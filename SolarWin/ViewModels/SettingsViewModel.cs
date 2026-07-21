using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;
using Windows.ApplicationModel;
using Windows.Storage.Pickers;

namespace SolarWin.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IToastService _toast;
    private readonly IAccountSessionService _sessions;
    private readonly IDeepLinkService _deepLinks;
    private readonly ISystemNotificationService _systemNotifications;
    private readonly ITrayService _tray;
    private bool _wallpaperReady;

    public SettingsViewModel(
        IAuthService authService,
        IToastService toast,
        IAccountSessionService sessions,
        IDeepLinkService deepLinks,
        ISystemNotificationService systemNotifications,
        ITrayService tray)
    {
        _authService = authService;
        _toast = toast;
        _sessions = sessions;
        _deepLinks = deepLinks;
        _systemNotifications = systemNotifications;
        _tray = tray;
        VersionText = ResolveVersion();
        SelectedThemeIndex = ThemeHelper.GetSavedTheme() switch
        {
            ElementTheme.Light => 1,
            ElementTheme.Dark => 2,
            _ => 0,
        };
        ThemeLabel = ThemeHelper.ToDisplayName(ThemeHelper.GetSavedTheme());
        CloseToTray = AppSettings.CloseToTray;
        MinimizeToTray = AppSettings.MinimizeToTray;
        UseSystemNotifications = AppSettings.UseSystemNotifications;
        ChatMessageNotifications = AppSettings.ChatMessageNotifications;
        LoadWallpaperState();
        RefreshAccounts();
        RefreshCacheStats();
        RefreshTrayStatus();
        _wallpaperReady = true;
    }

    public ObservableCollection<SavedAccountProfile> SavedAccounts { get; } = [];

    [ObservableProperty]
    public partial string VersionText { get; set; } = "1.1.0";

    [ObservableProperty]
    public partial string ThemeLabel { get; set; } = "跟随系统";

    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial bool CloseToTray { get; set; }

    [ObservableProperty]
    public partial bool MinimizeToTray { get; set; }

    [ObservableProperty]
    public partial bool UseSystemNotifications { get; set; }

    [ObservableProperty]
    public partial bool ChatMessageNotifications { get; set; }

    [ObservableProperty]
    public partial string CacheSizeText { get; set; } = "—";

    [ObservableProperty]
    public partial string ProtocolStatusText { get; set; } =
        AppSettings.ProtocolRegisteredOnce ? "solian:// 已尝试注册到当前用户" : "尚未注册协议";

    [ObservableProperty]
    public partial string TrayStatusText { get; set; } = string.Empty;

    // —— Wallpaper ——

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WallpaperStatusText))]
    public partial bool WallpaperEnabled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WallpaperStatusText))]
    public partial string WallpaperPathText { get; set; } = "未选择图片";

    [ObservableProperty]
    public partial BitmapImage? WallpaperPreview { get; set; }

    /// <summary>0–100.</summary>
    [ObservableProperty]
    public partial double WallpaperOpacity { get; set; } = 100;

    /// <summary>0–100.</summary>
    [ObservableProperty]
    public partial double WallpaperBlur { get; set; } = 40;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGlassEffect))]
    [NotifyPropertyChangedFor(nameof(IsFrostedEffect))]
    public partial int WallpaperEffectIndex { get; set; } = 1;

    public bool IsGlassEffect
    {
        get => WallpaperEffectIndex == 1;
        set
        {
            if (value)
            {
                WallpaperEffectIndex = 1;
            }
            else if (WallpaperEffectIndex == 1)
            {
                WallpaperEffectIndex = 0;
            }
        }
    }

    public bool IsFrostedEffect
    {
        get => WallpaperEffectIndex == 2;
        set
        {
            if (value)
            {
                WallpaperEffectIndex = 2;
            }
            else if (WallpaperEffectIndex == 2)
            {
                WallpaperEffectIndex = 0;
            }
        }
    }

    public string WallpaperStatusText =>
        !WallpaperEnabled
            ? "壁纸已关闭"
            : WallpaperHelper.HasImage
                ? $"已启用 · 透明 {WallpaperOpacity:0}% · 模糊 {WallpaperBlur:0}% · {EffectLabel}"
                : "请先选择一张图片";

    private string EffectLabel => WallpaperEffectIndex switch
    {
        1 => "玻璃",
        2 => "磨砂",
        _ => "纯图",
    };

    public event EventHandler? LoggedOut;

    partial void OnCloseToTrayChanged(bool value) => AppSettings.CloseToTray = value;

    partial void OnMinimizeToTrayChanged(bool value) => AppSettings.MinimizeToTray = value;

    partial void OnUseSystemNotificationsChanged(bool value) => AppSettings.UseSystemNotifications = value;

    partial void OnChatMessageNotificationsChanged(bool value) => AppSettings.ChatMessageNotifications = value;

    partial void OnWallpaperEnabledChanged(bool value)
    {
        if (!_wallpaperReady)
        {
            return;
        }

        WallpaperHelper.IsEnabled = value && WallpaperHelper.HasImage;
        if (value && !WallpaperHelper.HasImage)
        {
            WallpaperEnabled = false;
            StatusMessage = "请先选择壁纸图片";
            return;
        }

        PersistWallpaperAndApply();
    }

    partial void OnWallpaperOpacityChanged(double value)
    {
        if (!_wallpaperReady)
        {
            return;
        }

        WallpaperHelper.OpacityPercent = value;
        PersistWallpaperAndApply();
    }

    partial void OnWallpaperBlurChanged(double value)
    {
        if (!_wallpaperReady)
        {
            return;
        }

        WallpaperHelper.BlurPercent = value;
        PersistWallpaperAndApply();
    }

    partial void OnWallpaperEffectIndexChanged(int value)
    {
        if (!_wallpaperReady)
        {
            return;
        }

        WallpaperHelper.EffectMode = value switch
        {
            1 => WallpaperEffectMode.Glass,
            2 => WallpaperEffectMode.Frosted,
            _ => WallpaperEffectMode.None,
        };
        OnPropertyChanged(nameof(IsGlassEffect));
        OnPropertyChanged(nameof(IsFrostedEffect));
        OnPropertyChanged(nameof(WallpaperStatusText));
        PersistWallpaperAndApply();
    }

    private void LoadWallpaperState()
    {
        WallpaperEnabled = WallpaperHelper.IsEnabled && WallpaperHelper.HasImage;
        WallpaperOpacity = WallpaperHelper.OpacityPercent;
        WallpaperBlur = WallpaperHelper.BlurPercent;
        WallpaperEffectIndex = WallpaperHelper.EffectMode switch
        {
            WallpaperEffectMode.Frosted => 2,
            WallpaperEffectMode.None => 0,
            _ => 1,
        };
        RefreshWallpaperPreview();
    }

    private void RefreshWallpaperPreview()
    {
        if (WallpaperHelper.HasImage)
        {
            WallpaperPathText = WallpaperHelper.ImagePath!;
            try
            {
                var bmp = new BitmapImage();
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.UriSource = new Uri(WallpaperHelper.ImagePath!, UriKind.Absolute);
                WallpaperPreview = bmp;
            }
            catch
            {
                WallpaperPreview = null;
            }
        }
        else
        {
            WallpaperPathText = "未选择图片";
            WallpaperPreview = null;
        }

        OnPropertyChanged(nameof(WallpaperStatusText));
    }

    private void PersistWallpaperAndApply()
    {
        OnPropertyChanged(nameof(WallpaperStatusText));
        WallpaperHelper.NotifyChanged();
        if (App.Window is MainWindow mw)
        {
            mw.ApplyWallpaper();
        }
    }

    [RelayCommand]
    private async Task PickWallpaperAsync()
    {
        try
        {
            if (App.Window is null)
            {
                return;
            }

            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".webp");
            picker.FileTypeFilter.Add(".bmp");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            WallpaperHelper.InstallImage(file.Path);
            WallpaperEnabled = true;
            RefreshWallpaperPreview();
            PersistWallpaperAndApply();
            StatusMessage = "壁纸已设置";
            _toast.Success(StatusMessage);
        }
        catch (Exception ex)
        {
            StatusMessage = "选择壁纸失败：" + ex.Message;
            _toast.Error(StatusMessage);
        }
    }

    [RelayCommand]
    private void ClearWallpaper()
    {
        WallpaperHelper.ClearImage();
        WallpaperEnabled = false;
        RefreshWallpaperPreview();
        PersistWallpaperAndApply();
        StatusMessage = "已清除壁纸";
        _toast.Show(StatusMessage);
    }



    [RelayCommand]
    private void ApplyTheme()
    {
        var theme = SelectedThemeIndex switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        ThemeHelper.SaveTheme(theme);
        ThemeHelper.ApplyToWindow(App.Window, theme);
        ThemeLabel = ThemeHelper.ToDisplayName(theme);
        StatusMessage = $"主题已切换为：{ThemeLabel}";
        _toast.Success(StatusMessage);
    }

    [RelayCommand]
    private void RefreshAccounts()
    {
        SavedAccounts.Clear();
        foreach (var p in _sessions.GetProfiles())
        {
            SavedAccounts.Add(p);
        }
    }

    [RelayCommand]
    private async Task SwitchAccountAsync(SavedAccountProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        try
        {
            StatusMessage = $"正在切换到 {profile.DisplayLabel}…";
            await _authService.SwitchAccountAsync(profile.AccountId).ConfigureAwait(true);
            if (_authService.IsAuthenticated)
            {
                StatusMessage = $"已切换到 {profile.DisplayLabel}";
                _toast.Success(StatusMessage);
                _systemNotifications.Show("账号已切换", profile.DisplayLabel);
                RefreshAccounts();
            }
            else
            {
                StatusMessage = "切换失败：该账号没有本地 token，请重新登录。";
                _toast.Error(StatusMessage);
                LoggedOut?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "切换失败：" + ex.Message;
            _toast.Error(StatusMessage);
        }
    }

    [RelayCommand]
    private async Task RemoveAccountAsync(SavedAccountProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        try
        {
            await _sessions.RemoveAsync(profile.AccountId).ConfigureAwait(true);
            RefreshAccounts();
            StatusMessage = $"已移除本地账号 {profile.DisplayLabel}";
            _toast.Show(StatusMessage);
            if (_sessions.ActiveAccountId is null && !_authService.IsAuthenticated)
            {
                LoggedOut?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private void RegisterProtocol()
    {
        _deepLinks.EnsureProtocolRegistered();
        AppSettings.ProtocolRegisteredOnce = true;
        ProtocolStatusText = "solian:// 已写入当前用户注册表";
        StatusMessage = ProtocolStatusText + "。可在浏览器或运行框测试 solian://settings";
        _toast.Success("协议已注册");
    }

    [RelayCommand]
    private void TestSystemNotification()
    {
        _systemNotifications.Show(
            "Solar Network",
            "这是一条 Windows 系统通知测试。",
            launchArgs: "solian://settings");
        StatusMessage = "已发送系统通知（若未显示，请检查系统通知权限）。";
    }

    [RelayCommand]
    private void TestTray()
    {
        _tray.EnsureVisible();
        RefreshTrayStatus();
        if (_tray.IsInitialized)
        {
            _tray.ShowBalloon("Solar Network", "托盘图标已就绪。请看任务栏右下角应用图标。");
            StatusMessage = "托盘已刷新。请查看任务栏右下角（可能在 ^ 隐藏图标里）。";
            _toast.Success(StatusMessage);
        }
        else
        {
            StatusMessage = "托盘仍未初始化：" + (_tray.LastError ?? "未知错误");
            _toast.Error(StatusMessage);
        }
    }

    [RelayCommand]
    private void MinimizeToTrayNow()
    {
        if (App.Window is MainWindow mw)
        {
            mw.HideToTray();
            StatusMessage = "已请求最小化到托盘。";
        }
    }

    [RelayCommand]
    private void RefreshTrayStatus()
    {
        TrayStatusText = _tray.IsInitialized
            ? "托盘状态：已创建（任务栏右下角 .ico 图标）"
            : "托盘状态：未创建" + (string.IsNullOrWhiteSpace(_tray.LastError) ? string.Empty : " — " + _tray.LastError);
    }

    [RelayCommand]
    private void ClearOfflineCache()
    {
        OfflineCache.ClearAll();
        RefreshCacheStats();
        StatusMessage = "离线缓存已清空";
        _toast.Success(StatusMessage);
    }

    [RelayCommand]
    private void RefreshCacheStats()
    {
        var bytes = OfflineCache.EstimateSizeBytes();
        CacheSizeText = bytes < 1024
            ? $"{bytes} B"
            : bytes < 1024 * 1024
                ? $"{bytes / 1024.0:0.#} KB"
                : $"{bytes / (1024.0 * 1024):0.##} MB";
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try
        {
            // ConfigureAwait(false): do not rely on WinUI SynchronizationContext (often missing).
            await _authService.LogoutAsync().ConfigureAwait(false);
        }
        catch
        {
            // Local sign-out still proceeds even if the network logout fails.
        }

        // Stop realtime / tray chat listener while signed out.
        try
        {
            App.Services.GetService<IChatMessageNotifier>()?.Stop();
        }
        catch
        {
            // ignore
        }

        // Navigation + toast must run on the UI thread.
        void FinishOnUi()
        {
            StatusMessage = "已退出登录";
            try
            {
                _toast.Success("已退出登录");
            }
            catch
            {
                // ignore
            }

            // Prefer direct root navigation (Settings lives inside Shell ContentFrame —
            // Page.Frame would only navigate the inner frame and leave the shell visible).
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
        else if (!dq.TryEnqueue(FinishOnUi))
        {
            // Fallback if enqueue fails
            FinishOnUi();
        }
    }

    private static string ResolveVersion()
    {
        try
        {
            var info = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                // Strip optional "+git" / metadata suffix
                var plus = info.IndexOf('+');
                return plus > 0 ? info[..plus] : info;
            }
        }
        catch
        {
            // fall through
        }

        try
        {
            var v = Package.Current.Id.Version;
            return v.Revision == 0
                ? $"{v.Major}.{v.Minor}.{v.Build}"
                : $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
        catch
        {
            var av = Assembly.GetExecutingAssembly().GetName().Version;
            if (av is null)
            {
                return "1.1.0";
            }

            return av.Revision == 0
                ? $"{av.Major}.{av.Minor}.{av.Build}"
                : av.ToString();
        }
    }
}
