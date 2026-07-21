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
    private DispatcherTimer? _toastTimer;
    private DispatcherTimer? _badgeTimer;

    public MainViewModel ViewModel { get; }

    public ShellPage()
    {
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        _toast = App.Services.GetRequiredService<IToastService>();
        InitializeComponent();

        ViewModel.LoggedOut += OnLoggedOut;
        _toast.MessageRaised += OnToastMessage;
        Unloaded += OnUnloaded;
    }

    private void NavView_OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.RefreshFromAuth();
        _ = ViewModel.RefreshNotificationBadgeAsync();

        // Keep chat WebSocket + background DM notifications alive while shell is open
        try
        {
            App.Services.GetRequiredService<IChatMessageNotifier>().Start();
            App.Services.GetRequiredService<ChatViewModel>().EnsureRealtimeStarted();
        }
        catch
        {
            // non-fatal
        }

        try
        {
            // Content can stay clear for wallpaper; pane keeps a solid theme brush so icons contrast.
            ContentFrame.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            ApplyNavChrome();
            ApplyNavIcons();
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
                ApplyNavIcons();
            });
        }

        ActualThemeChanged += (_, _) =>
        {
            ApplyNavChrome();
            ApplyNavIcons();
        };

        WallpaperHelper.Changed += OnWallpaperSettingsChanged;

        _badgeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _badgeTimer.Tick += async (_, _) => await ViewModel.RefreshNotificationBadgeAsync();
        _badgeTimer.Start();

        ContentFrame.Navigated += ContentFrame_OnNavigated;

        if (NavView.MenuItems.Count > 0 && NavView.SelectedItem is null)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }
    }

    /// <summary>Deep link: open user profile page inside shell content frame.</summary>
    public void NavigateToUserProfile(string name)
    {
        SelectNavTag("home");
        ContentFrame.Navigate(typeof(UserProfilePage), new UserProfileNavArgs(name));
    }

    /// <summary>Deep link: open chat room detail.</summary>
    public void NavigateToChatRoom(Guid roomId)
    {
        SelectNavTag("chat");
        ContentFrame.Navigate(typeof(ChatDetailPage), roomId);
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
            ApplyNavIcons();
        });
    }

    /// <summary>
    /// Keep the left pane legible (especially with custom wallpaper). Never leave it fully transparent.
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
                NavView.Background = new SolidColorBrush(pane);
            }
            else
            {
                // Prefer theme pane brush when present.
                object? brush = null;
                if (Application.Current.Resources.ContainsKey("NavigationViewExpandedPaneBackground"))
                {
                    brush = Application.Current.Resources["NavigationViewExpandedPaneBackground"];
                }
                else if (Application.Current.Resources.ContainsKey("LayerFillColorDefaultBrush"))
                {
                    brush = Application.Current.Resources["LayerFillColorDefaultBrush"];
                }

                NavView.Background = brush as Brush
                    ?? new SolidColorBrush(
                        ActualTheme == ElementTheme.Dark
                            ? Windows.UI.Color.FromArgb(255, 32, 32, 32)
                            : Windows.UI.Color.FromArgb(255, 243, 243, 243));
            }
        }
        catch
        {
            NavView.Background = new SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 40, 40, 40));
        }
    }

    /// <summary>
    /// Assign nav icons in code (most reliable on WinUI self-contained builds).
    /// Prefers <see cref="SymbolIcon"/>; falls back to Segoe Fluent FontIcon; last resort uses app icon image.
    /// Note: Assets/icon.ico is the app identity icon — not one unique glyph per menu item.
    /// </summary>
    private void ApplyNavIcons()
    {
        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
        {
            var tag = item.Tag as string ?? string.Empty;
            item.Icon = CreateNavIcon(tag);
        }
    }

    private static IconElement CreateNavIcon(string tag)
    {
        // 1) SymbolIcon — platform-backed, works without hard-coded glyph fonts.
        // Use only long-stable Symbol values (WinUI Symbol surface varies by SDK).
        Symbol? symbol = tag switch
        {
            "home" => Symbol.Home,
            "chat" => Symbol.Message,
            "posts" => Symbol.Document,
            "explore" => Symbol.Find,
            "thinking" => Symbol.Edit,
            "weather" => Symbol.Globe,
            "files" => Symbol.Folder,
            "notifications" => Symbol.Mail,
            "wallet" => Symbol.Shop,
            "profile" => Symbol.Contact,
            _ => null,
        };

        if (symbol is { } s)
        {
            try
            {
                return new SymbolIcon(s);
            }
            catch
            {
                // fall through
            }
        }

        // 2) FontIcon with explicit Fluent / MDL2 families (common on Windows 10/11).
        var glyph = tag switch
        {
            "home" => "\uE80F",
            "chat" => "\uE8BD",
            "posts" => "\uE8A5",
            "explore" => "\uE721",
            "thinking" => "\uE70F",
            "weather" => "\uE823",
            "files" => "\uE8B7",
            "notifications" => "\uEA8F",
            "wallet" => "\uE8C7",
            "profile" => "\uE77B",
            _ => "\uE8F1",
        };

        try
        {
            return new FontIcon
            {
                Glyph = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 16,
            };
        }
        catch
        {
            // 3) Last resort: show the app icon so the icon slot is never empty.
            try
            {
                return new ImageIcon
                {
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                        new Uri("ms-appx:///Assets/icon.ico")),
                };
            }
            catch
            {
                return new FontIcon { Glyph = "•", FontSize = 16 };
            }
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
            ContentFrame.Navigate(typeof(SettingsPage));
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
            "posts" => typeof(PostsPage),
            "explore" => typeof(SphereExplorePage),
            "thinking" => typeof(ThinkingPage),
            "weather" => typeof(WeatherPage),
            "files" => typeof(FilesPage),
            "notifications" => typeof(NotificationsPage),
            "wallet" => typeof(WalletPage),
            "profile" => typeof(ProfilePage),
            _ => typeof(HomePage),
        };

        ContentFrame.Navigate(pageType);

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
        // Always leave the shell via the root frame (not ContentFrame).
        if (App.Window is MainWindow mainWindow)
        {
            mainWindow.NavigateToLogin();
            return;
        }

        Frame?.Navigate(typeof(LoginPage));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LoggedOut -= OnLoggedOut;
        _toast.MessageRaised -= OnToastMessage;
        WallpaperHelper.Changed -= OnWallpaperSettingsChanged;
        ContentFrame.Navigated -= ContentFrame_OnNavigated;
        _badgeTimer?.Stop();
        _toastTimer?.Stop();
        Unloaded -= OnUnloaded;
    }
}
