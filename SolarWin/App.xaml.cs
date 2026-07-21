using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using SolarWin.Helpers;
using SolarWin.Services;
using SolarWin.Views;

namespace SolarWin;

/// <summary>
/// Application entry: DI + login gate + tray + deep links + shell navigation.
/// </summary>
public partial class App : Application
{
    private readonly IServiceProvider _services;
    private string? _pendingDeepLink;

    public static IServiceProvider Services { get; private set; } = null!;

    public static Window Window { get; private set; } = null!;

    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    public App()
    {
        InitializeComponent();
        AppPaths.EnsureDirectories();
        _services = ConfigureServices();
        Services = _services;
        UnhandledException += (_, e) =>
        {
            // Keep process alive for tray if possible
            e.Handled = true;
            try
            {
                _services.GetService<ISystemNotificationService>()
                    ?.Show("SolarWin 错误", e.Message ?? "未处理异常");
            }
            catch
            {
                // ignore
            }
        };
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSolarWinServices();
        return services.BuildServiceProvider();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // Capture activation URI: CLI (unpackaged) or AppLifecycle (packaged).
        try
        {
            var activated = AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activated.Kind == ExtendedActivationKind.Protocol
                && activated.Data is Windows.ApplicationModel.Activation.ProtocolActivatedEventArgs protocol
                && protocol.Uri is not null)
            {
                _pendingDeepLink = protocol.Uri.AbsoluteUri;
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            var cli = Environment.GetCommandLineArgs();
            for (var i = 1; i < cli.Length; i++)
            {
                if (DeepLinkParser.IsSolarUri(cli[i]))
                {
                    _pendingDeepLink = cli[i];
                    break;
                }
            }
        }
        catch
        {
            // ignore
        }

        SystemNotificationService.EnsureComActivatorRegistered();

        var deep = _services.GetRequiredService<IDeepLinkService>();
        deep.EnsureProtocolRegistered();
        AppSettings.ProtocolRegisteredOnce = true;
        deep.DeepLinkReceived += OnDeepLinkReceived;

        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        ThemeHelper.ApplyToWindow(Window, ThemeHelper.GetSavedTheme());

        var auth = _services.GetRequiredService<IAuthService>();
        try
        {
            await auth.InitializeAsync().ConfigureAwait(true);
        }
        catch
        {
            // Offline / invalid token — stay logged out or use cache inside Initialize.
        }

        Window.Activate();

        // Tray after window is activated (shell needs a live HWND for some hosts)
        try
        {
            var tray = _services.GetRequiredService<ITrayService>();
            tray.Initialize();
            if (!tray.IsInitialized && tray.LastError is { } err)
            {
                _services.GetService<IToastService>()?.Warning("托盘图标未能创建：" + err);
            }
        }
        catch (Exception ex)
        {
            _services.GetService<IToastService>()?.Warning("托盘初始化异常：" + ex.Message);
        }

        if (Window is MainWindow mainWindow)
        {
            mainWindow.NavigateToStart(auth.IsAuthenticated ? typeof(ShellPage) : typeof(LoginPage));
        }

        if (!string.IsNullOrWhiteSpace(_pendingDeepLink))
        {
            deep.HandleUri(_pendingDeepLink);
            _pendingDeepLink = null;
        }
    }

    private void OnDeepLinkReceived(object? sender, DeepLinkAction action)
    {
        if (Window is MainWindow mw)
        {
            mw.ShowFromTray();
        }

        DispatcherQueue?.TryEnqueue(() => ApplyDeepLink(action));
    }

    private void ApplyDeepLink(DeepLinkAction action)
    {
        if (Window is not MainWindow main)
        {
            return;
        }

        var frame = main.GetRootFrame();
        var auth = _services.GetRequiredService<IAuthService>();
        var toast = _services.GetRequiredService<IToastService>();

        switch (action.Kind)
        {
            case DeepLinkKind.Login:
                frame.Navigate(typeof(LoginPage));
                break;
            case DeepLinkKind.Settings:
                if (auth.IsAuthenticated)
                {
                    frame.Navigate(typeof(ShellPage));
                    // Shell will open home; user can open settings — also try nested
                    toast.Show("已打开应用，请从导航进入设置。");
                }
                else
                {
                    frame.Navigate(typeof(LoginPage));
                }

                break;
            case DeepLinkKind.UserProfile when !string.IsNullOrWhiteSpace(action.Value):
                if (!auth.IsAuthenticated)
                {
                    frame.Navigate(typeof(LoginPage));
                    toast.Show("请先登录后再打开用户主页。");
                    break;
                }

                frame.Navigate(typeof(ShellPage));
                toast.Show($"深度链接：用户 @{action.Value}");
                // Navigate into shell content if possible after a tick
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    try
                    {
                        if (frame.Content is ShellPage shell)
                        {
                            shell.NavigateToUserProfile(action.Value!);
                        }
                    }
                    catch
                    {
                        // Shell helper optional
                    }
                });
                break;
            case DeepLinkKind.ChatRoom when !string.IsNullOrWhiteSpace(action.Value):
                if (!auth.IsAuthenticated)
                {
                    frame.Navigate(typeof(LoginPage));
                    toast.Show("请先登录后再打开聊天。");
                    break;
                }

                frame.Navigate(typeof(ShellPage));
                if (Guid.TryParse(action.Value, out var roomId))
                {
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                    {
                        try
                        {
                            if (frame.Content is ShellPage shell)
                            {
                                shell.NavigateToChatRoom(roomId);
                            }
                        }
                        catch
                        {
                            toast.Show($"聊天房间：{action.Value}");
                        }
                    });
                }
                else
                {
                    toast.Show($"聊天链接：{action.Value}");
                }

                break;
            case DeepLinkKind.Post when !string.IsNullOrWhiteSpace(action.Value):
                if (!auth.IsAuthenticated)
                {
                    frame.Navigate(typeof(LoginPage));
                    break;
                }

                frame.Navigate(typeof(ShellPage));
                toast.Show($"帖子链接：{action.Value}");
                break;
            case DeepLinkKind.QrLogin:
                // QR payload is for phone to scan desktop QR — if we receive as client, show login QR page.
                frame.Navigate(typeof(LoginPage));
                toast.Show("收到扫码登录链接。请在登录页使用「扫码登录」生成二维码供手机扫描。");
                break;
            default:
                if (!string.IsNullOrWhiteSpace(action.RawUri))
                {
                    toast.Show($"已接收链接：{action.RawUri}");
                }

                break;
        }
    }
}
