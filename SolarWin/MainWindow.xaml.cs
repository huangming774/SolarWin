using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SolarWin.Helpers;
using SolarWin.Services;
using Windows.Graphics;

namespace SolarWin;

/// <summary>
/// Application shell window hosting the root navigation frame.
/// Min client size: 900×600. Supports minimize/close to tray.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int MinWidth = 900;
    private const int MinHeight = 600;
    private bool _forceClose;
    private bool _isInTray;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/icon.ico");

        AppWindow.Resize(new SizeInt32(1180, 760));
        AppWindow.Changed += AppWindow_OnChanged;
        AppWindow.Closing += AppWindow_OnClosing;

        if (Content is FrameworkElement root)
        {
            root.SizeChanged += Root_OnSizeChanged;
        }

        WallpaperHelper.Changed += OnWallpaperChanged;
        Closed += (_, _) => WallpaperHelper.Changed -= OnWallpaperChanged;

        // Apply after first layout so ActualTheme is valid.
        DispatcherQueue.TryEnqueue(ApplyWallpaper);
    }

    /// <summary>Re-read wallpaper settings and paint layers.</summary>
    public void ApplyWallpaper()
    {
        WallpaperHelper.ApplyToLayers(
            WallpaperImage,
            WallpaperEffectOverlay,
            WallpaperBlurWash,
            ChromeRoot,
            this);
    }

    private void OnWallpaperChanged(object? sender, EventArgs e)
        => DispatcherQueue.TryEnqueue(ApplyWallpaper);

    private void RootHost_OnActualThemeChanged(FrameworkElement sender, object args)
        => ApplyWallpaper();

    /// <summary>
    /// Replace the root frame content (login ↔ shell). Always runs on the UI thread and
    /// clears the back stack so the user cannot go “back” into an authenticated shell.
    /// </summary>
    public void NavigateToStart(Type pageType)
    {
        void Go()
        {
            try
            {
                RootFrame.Navigate(pageType);
                // Drop any prior Shell/Login entries so Back won't resurrect a session.
                try
                {
                    RootFrame.BackStack.Clear();
                    RootFrame.ForwardStack.Clear();
                }
                catch
                {
                    // older hosts may not allow clear
                }
            }
            catch
            {
                // last resort: navigate without clearing
                try
                {
                    RootFrame.Navigate(pageType);
                }
                catch
                {
                    // ignore
                }
            }
        }

        // WinUI often has no reliable SynchronizationContext after await — marshal explicitly.
        var dq = DispatcherQueue;
        if (dq is null || dq.HasThreadAccess)
        {
            Go();
        }
        else
        {
            dq.TryEnqueue(Go);
        }
    }

    /// <summary>Logout destination: login page on the root frame.</summary>
    public void NavigateToLogin() => NavigateToStart(typeof(Views.LoginPage));

    public Frame GetRootFrame() => RootFrame;

    /// <summary>Show and activate after tray click / deep link / notification.</summary>
    public void ShowFromTray()
    {
        _isInTray = false;
        try
        {
            AppWindow.Show();
            Activate();
            if (AppWindow.Presenter is OverlappedPresenter op)
            {
                op.Restore();
            }

            AppWindow.MoveInZOrderAtTop();
        }
        catch
        {
            try
            {
                Activate();
            }
            catch
            {
                // ignore
            }
        }
    }

    public void HideToTray()
    {
        if (_isInTray)
        {
            return;
        }

        try
        {
            var tray = App.Services.GetService(typeof(ITrayService)) as ITrayService;
            tray?.EnsureVisible();

            if (tray is null || !tray.IsInitialized)
            {
                var err = tray?.LastError;
                if (App.Services.GetService(typeof(IToastService)) is IToastService toast)
                {
                    toast.Error(string.IsNullOrWhiteSpace(err)
                        ? "托盘图标初始化失败，无法最小化到托盘。"
                        : "托盘失败：" + err);
                }

                return;
            }

            _isInTray = true;
            AppWindow.Hide();

            tray.SetToolTip("Solar Network — 已在托盘运行，点击恢复");
            tray.ShowBalloon("Solar Network", "已最小化到系统托盘。\n点击托盘图标可恢复窗口。");
        }
        catch
        {
            _isInTray = false;
        }
    }

    /// <summary>Really quit (from tray menu).</summary>
    public void ForceClose()
    {
        _forceClose = true;
        _isInTray = false;
        try
        {
            if (App.Services.GetService(typeof(ITrayService)) is ITrayService tray)
            {
                tray.Dispose();
            }
        }
        catch
        {
            // ignore
        }

        Close();
    }

    private void AppWindow_OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_forceClose)
        {
            return;
        }

        if (AppSettings.CloseToTray)
        {
            args.Cancel = true;
            // Defer hide so Closing completes cancel first
            App.DispatcherQueue?.TryEnqueue(() => HideToTray());
        }
    }

    private void AppWindow_OnChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange)
        {
            EnforceMinSize();
        }

        // Minimize to tray
        if (args.DidPresenterChange
            && AppSettings.MinimizeToTray
            && !_isInTray
            && AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized })
        {
            App.DispatcherQueue?.TryEnqueue(() => HideToTray());
        }
    }

    private void Root_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        EnforceMinSize();
    }

    private void EnforceMinSize()
    {
        if (_isInTray)
        {
            return;
        }

        var size = AppWindow.Size;
        var w = size.Width;
        var h = size.Height;
        if (w >= MinWidth && h >= MinHeight)
        {
            return;
        }

        AppWindow.Resize(new SizeInt32(
            w < MinWidth ? MinWidth : w,
            h < MinHeight ? MinHeight : h));
    }
}
