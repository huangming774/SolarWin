using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SolarWin.Helpers;
using SolarWin.Services;
using SolarWin.Views;

namespace SolarWin;

/// <summary>
/// Application entry: DI + login gate + shell navigation.
/// </summary>
public partial class App : Application
{
    private readonly IServiceProvider _services;

    public static IServiceProvider Services { get; private set; } = null!;

    public static Window Window { get; private set; } = null!;

    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    public App()
    {
        InitializeComponent();
        _services = ConfigureServices();
        Services = _services;
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Node 3 DI layout (IAuthService + HttpClient + MainViewModel)
        services.AddSolarWinServices();

        return services.BuildServiceProvider();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
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
            // Offline / invalid token — stay logged out.
        }

        Window.Activate();

        if (Window is MainWindow mainWindow)
        {
            // 登录前：LoginPage；登录后：ShellPage（NavigationView 主框架）
            mainWindow.NavigateToStart(auth.IsAuthenticated ? typeof(ShellPage) : typeof(LoginPage));
        }
    }
}
