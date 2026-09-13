using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;
using SolarWin.ViewModels;

namespace SolarWin.Views;

/// <summary>Authenticated shell: left NavigationView + content Frame + toast InfoBar.</summary>
public sealed partial class ShellPage : Page
{
    private readonly IToastService _toast;
    private readonly IIncomingCallService _incomingCalls;
    private DispatcherTimer? _toastTimer;
    private DispatcherTimer? _badgeTimer;

    public MainViewModel ViewModel { get; }

    public ShellPage()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        _toast = App.Services.GetRequiredService<IToastService>();
        _incomingCalls = App.Services.GetRequiredService<IIncomingCallService>();
        InitializeComponent();

        ViewModel.LoggedOut += OnLoggedOut;
        _toast.MessageRaised += OnToastMessage;
        _incomingCalls.Changed += OnIncomingCallChanged;
        Unloaded += OnUnloaded;
    }

    private void NavView_OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.RefreshFromAuth();
        if (ViewModel.RefreshProfileCommand.CanExecute(null))
        {
            _ = ViewModel.RefreshProfileCommand.ExecuteAsync(null);
        }

        _ = ViewModel.RefreshNotificationBadgeAsync();

        // Keep chat WebSocket + background DM notifications alive while shell is open
        try
        {
            App.Services.GetRequiredService<IChatMessageNotifier>().Start();
            App.Services.GetRequiredService<ChatViewModel>().EnsureRealtimeStarted();
            _incomingCalls.Start();
            RefreshIncomingCallBanner();
        }
        catch
        {
            // non-fatal
        }

        try
        {
            // Content can stay clear for wallpaper; the pane gets its own contrast brush.
            ContentFrame.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            ApplyNavChrome();
        }
        catch
        {
            // ignore
        }

        if (App.Window is MainWindow mw)
        {
            mw.ApplyWallpaper();
            // Re-apply after wallpaper changes Mica / chrome
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                ApplyNavChrome();
            });
        }

        ActualThemeChanged += (_, _) =>
        {
            ApplyNavChrome();
        };

        WallpaperHelper.Changed += OnWallpaperSettingsChanged;

        _badgeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _badgeTimer.Tick += async (_, _) => await ViewModel.RefreshNotificationBadgeAsync();
        _badgeTimer.Start();

        if (App.Window is MainWindow mainWindow)
        {
            mainWindow.TrayStateChanged += OnTrayStateChanged;
        }

        ContentFrame.Navigated += ContentFrame_OnNavigated;

        if (NavView.MenuItems.Count > 0 && NavView.SelectedItem is null)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }
    }

    /// <summary>
    /// Cap content-frame history. Pages use the default NavigationCacheMode (instances are
    /// recreated on each visit), so back entries are small records — but their parameters
    /// and any future cached page would otherwise accumulate without bound.
    /// </summary>
    private const int MaxBackStackDepth = 4;

    private void NavigateContent(Type pageType, object? parameter = null)
    {
        if (parameter is null && ContentFrame.CurrentSourcePageType == pageType)
        {
            return;
        }

        if (!ContentFrame.Navigate(pageType, parameter))
        {
            return;
        }

        while (ContentFrame.BackStack.Count > MaxBackStackDepth)
        {
            ContentFrame.BackStack.RemoveAt(0);
        }
    }

    /// <summary>Deep link: open user profile page inside shell content frame.</summary>
    public void NavigateToUserProfile(string name)
    {
        SelectNavTag("home");
        NavigateContent(typeof(UserProfilePage), new UserProfileNavArgs(name));
    }

    /// <summary>Deep link: open chat room detail.</summary>
    public void NavigateToChatRoom(Guid roomId)
    {
        SelectNavTag("chat");
        NavigateContent(typeof(ChatDetailPage), roomId);
    }

    private void SelectNavTag(string tag)
    {
        ViewModel.SelectedTag = tag;
        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.Tag is string t && t == tag)
            {
                NavView.SelectedItem = item;
                break;
            }
        }
    }

    private void ContentFrame_OnNavigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        NavView.IsBackEnabled = ContentFrame.CanGoBack;
    }

    private void OnWallpaperSettingsChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyNavChrome();
        });
    }

    /// <summary>No badge polling while hidden in the tray; refresh once on restore.</summary>
    private void OnTrayStateChanged(object? sender, bool isInTray)
    {
        if (isInTray)
        {
            _badgeTimer?.Stop();
        }
        else
        {
            _badgeTimer?.Start();
            _ = ViewModel.RefreshNotificationBadgeAsync();
        }
    }

    /// <summary>
    /// Keep the left pane legible without covering the wallpaper behind the content area.
    /// </summary>
    private void ApplyNavChrome()
    {
        try
        {
            if (WallpaperHelper.IsEnabled && WallpaperHelper.HasImage)
            {
                var isDark = ActualTheme == ElementTheme.Dark
                             || (ActualTheme == ElementTheme.Default
                                 && Application.Current.RequestedTheme == ApplicationTheme.Dark);
                var pane = isDark
                    ? Windows.UI.Color.FromArgb(0xE6, 0x20, 0x20, 0x24)
                    : Windows.UI.Color.FromArgb(0xE6, 0xF3, 0xF3, 0xF3);
                NavView.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                SetPaneBackground(pane);
            }
            else
            {
                object? brush = null;
                if (Application.Current.Resources.ContainsKey("LayerFillColorDefaultBrush"))
                {
                    brush = Application.Current.Resources["LayerFillColorDefaultBrush"];
                }

                var color = ActualTheme == ElementTheme.Dark
                    ? Windows.UI.Color.FromArgb(255, 32, 32, 32)
                    : Windows.UI.Color.FromArgb(255, 243, 243, 243);
                NavView.Background = brush as Brush ?? new SolidColorBrush(color);
                SetPaneBackground(color);
            }
        }
        catch
        {
            var color = Windows.UI.Color.FromArgb(255, 40, 40, 40);
            NavView.Background = new SolidColorBrush(color);
            SetPaneBackground(color);
        }
    }

    private void SetPaneBackground(Windows.UI.Color color)
    {
        if (NavView.Resources["NavigationViewExpandedPaneBackground"] is SolidColorBrush paneBrush)
        {
            paneBrush.Color = color;
        }
    }

    private void NavView_OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
        }
    }

    private void NavView_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ViewModel.SelectedTag = "settings";
            NavigateContent(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        ViewModel.SelectedTag = tag;
        var pageType = tag switch
        {
            "home" => typeof(HomePage),
            "chat" => typeof(ChatPage),
            "data-center" => typeof(ChatDataCenterPage),
            "posts" => typeof(PostsPage),
            "explore" => typeof(SphereExplorePage),
            "thinking" => typeof(ThinkingPage),
            "weather" => typeof(WeatherPage),
            "ai" => typeof(AiPage),
            "files" => typeof(FilesPage),
            "notifications" => typeof(NotificationsPage),
            "stellar-program" => typeof(StellarProgramPage),
            "wallet" => typeof(WalletPage),
            "order" => typeof(OrderPage),
            "profile" => typeof(ProfilePage),
            _ => typeof(HomePage),
        };

        NavigateContent(pageType);

        if (tag == "notifications")
        {
            _ = ViewModel.RefreshNotificationBadgeAsync();
        }
    }

    private void OnToastMessage(object? sender, ToastMessage message)
    {
        ToastBar.Title = message.Kind switch
        {
            ToastKind.Success => "成功",
            ToastKind.Error => "错误",
            ToastKind.Warning => "提示",
            _ => "消息",
        };
        ToastBar.Message = message.Text;
        ToastBar.Severity = message.Kind switch
        {
            ToastKind.Success => InfoBarSeverity.Success,
            ToastKind.Error => InfoBarSeverity.Error,
            ToastKind.Warning => InfoBarSeverity.Warning,
            _ => InfoBarSeverity.Informational,
        };
        ToastBar.IsOpen = true;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        _toastTimer.Tick += (_, _) =>
        {
            ToastBar.IsOpen = false;
            _toastTimer?.Stop();
        };
        _toastTimer.Start();
    }

    private void OnLoggedOut(object? sender, EventArgs e)
    {
        // 清空跨账号会话状态：OrderViewModel 等 Singleton ViewModel 会保留旧账号的购物车/未支付订单入口，
        // 在切账号前显式 Reset()，避免下一账号继承（详见对抗性审查 F2）。
        try { App.Services.GetRequiredService<OrderViewModel>().Reset(); } catch { }

        // Always leave the shell via the root frame (not ContentFrame).
        if (App.Window is MainWindow mainWindow)
        {
            mainWindow.NavigateToLogin();
            return;
        }

        Frame?.Navigate(typeof(LoginPage));
    }

    private void OnIncomingCallChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshIncomingCallBanner);
    }

    private void RefreshIncomingCallBanner()
    {
        if (_incomingCalls.HasIncoming && _incomingCalls.Current is { } info)
        {
            IncomingCallBanner.Visibility = Visibility.Visible;
            IncomingCallerText.Text = info.DisplayTitle;
            IncomingRoomText.Text = info.DisplaySubtitle;
        }
        else
        {
            IncomingCallBanner.Visibility = Visibility.Collapsed;
        }
    }

    private void DeclineCall_OnClick(object sender, RoutedEventArgs e)
    {
        _incomingCalls.Decline();
        RefreshIncomingCallBanner();
    }

    private async void AcceptCall_OnClick(object sender, RoutedEventArgs e)
    {
        var info = _incomingCalls.Accept();
        RefreshIncomingCallBanner();
        if (info is null || info.RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            SelectNavTag("chat");
            NavigateContent(typeof(ChatDetailPage), info.RoomId);

            // Join after navigation; reuse the page's own VM — resolving another transient
            // ChatDetailViewModel here would leak it on the singleton media/WS services.
            await Task.Delay(200);
            if (ContentFrame.Content is ChatDetailPage detailPage)
            {
                await detailPage.ViewModel.AcceptIncomingAndJoinAsync(info.RoomId, info.RoomTitle);
            }
        }
        catch (Exception ex)
        {
            _toast.Error("接听失败：" + ex.Message);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LoggedOut -= OnLoggedOut;
        _toast.MessageRaised -= OnToastMessage;
        _incomingCalls.Changed -= OnIncomingCallChanged;
        WallpaperHelper.Changed -= OnWallpaperSettingsChanged;
        if (App.Window is MainWindow mainWindow)
        {
            mainWindow.TrayStateChanged -= OnTrayStateChanged;
        }

        ContentFrame.Navigated -= ContentFrame_OnNavigated;
        _badgeTimer?.Stop();
        _toastTimer?.Stop();
        Unloaded -= OnUnloaded;
    }
}
