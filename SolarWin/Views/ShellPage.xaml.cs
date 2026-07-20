using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        _badgeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _badgeTimer.Tick += async (_, _) => await ViewModel.RefreshNotificationBadgeAsync();
        _badgeTimer.Start();

        if (NavView.MenuItems.Count > 0 && NavView.SelectedItem is null)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
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
        Frame?.Navigate(typeof(LoginPage));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LoggedOut -= OnLoggedOut;
        _toast.MessageRaised -= OnToastMessage;
        _badgeTimer?.Stop();
        _toastTimer?.Stop();
        Unloaded -= OnUnloaded;
    }
}
