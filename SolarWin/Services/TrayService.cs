using H.NotifyIcon;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SolarWin.Services;

/// <summary>
/// System tray icon (H.NotifyIcon.WinUI).
/// Uses a real <c>.ico</c> file from Assets; must call <see cref="TaskbarIcon.ForceCreate"/>.
/// </summary>
public sealed class TrayService : ITrayService
{
    private TaskbarIcon? _icon;
    private bool _disposed;
    private string? _lastError;

    public bool IsInitialized => _icon is not null;

    public string? LastError => _lastError;

    public void Initialize()
    {
        if (_icon is not null || _disposed)
        {
            return;
        }

        try
        {
            _icon = new TaskbarIcon
            {
                ToolTipText = "Solar Network — 点击显示窗口",
                // ImageSource: load .ico from disk (unpackaged-friendly)
                IconSource = CreateTrayImageSource(),
                ContextMenuMode = ContextMenuMode.SecondWindow,
            };

            var menu = new MenuFlyout();

            var show = new MenuFlyoutItem { Text = "显示主窗口" };
            show.Click += (_, _) => ShowMainWindow();
            menu.Items.Add(show);

            menu.Items.Add(new MenuFlyoutSeparator());

            var quit = new MenuFlyoutItem { Text = "退出 SolarWin" };
            quit.Click += (_, _) => QuitApp();
            menu.Items.Add(quit);

            _icon.ContextFlyout = menu;

            _icon.LeftClickCommand = new TrayRelayCommand(ShowMainWindow);
            _icon.NoLeftClickDelay = true;

            // Critical: without ForceCreate the tray icon is never registered with the shell.
            _icon.ForceCreate(enablesEfficiencyMode: false);

            _lastError = null;
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            try { _icon?.Dispose(); } catch { /* ignore */ }
            _icon = null;
        }
    }

    public void ShowBalloon(string title, string text)
    {
        try
        {
            if (_icon is null)
            {
                Initialize();
            }

            _icon?.ShowNotification(
                title: string.IsNullOrWhiteSpace(title) ? "Solar Network" : title,
                message: text ?? string.Empty);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
        }
    }

    public void SetToolTip(string text)
    {
        if (_icon is not null && !string.IsNullOrWhiteSpace(text))
        {
            _icon.ToolTipText = text.Trim();
        }
    }

    /// <summary>Ensure icon exists (e.g. right before hiding window to tray).</summary>
    public void EnsureVisible()
    {
        if (_icon is null)
        {
            Initialize();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _icon?.Dispose();
        }
        catch
        {
            // ignore
        }

        _icon = null;
    }

    /// <summary>
    /// Tray icon from Assets\icon.ico (absolute file path). Fallback: generated glyph.
    /// TaskbarIcon.IconSource is <see cref="ImageSource"/> (not Controls.IconSource).
    /// </summary>
    private static ImageSource CreateTrayImageSource()
    {
        var icoPath = ResolveTrayIcoPath();
        if (icoPath is not null)
        {
            try
            {
                // Absolute file URI → BitmapImage loads multi-size .ico for the shell.
                return new BitmapImage(new Uri(icoPath, UriKind.Absolute));
            }
            catch
            {
                // fall through
            }
        }

        // Last resort if ico missing from output
        return new GeneratedIconSource
        {
            Text = "S",
            FontSize = 52,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White),
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 0, 120, 212)),
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(8),
        };
    }

    private static string? ResolveTrayIcoPath()
    {
        var names = new[]
        {
            Path.Combine("Assets", "icon.ico"),
            Path.Combine("Assets", "AppIcon.ico"),
            "icon.ico",
            "AppIcon.ico",
        };

        var roots = new List<string>();
        try
        {
            var baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                roots.Add(baseDir);
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                var dir = Path.GetDirectoryName(exe);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    roots.Add(dir);
                }
            }
        }
        catch
        {
            // ignore
        }

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var name in names)
            {
                var full = Path.GetFullPath(Path.Combine(root, name));
                if (File.Exists(full))
                {
                    return full;
                }
            }
        }

        return null;
    }

    private static void ShowMainWindow()
    {
        try
        {
            if (App.DispatcherQueue is { } dq && !dq.HasThreadAccess)
            {
                dq.TryEnqueue(() =>
                {
                    if (App.Window is MainWindow mw)
                    {
                        mw.ShowFromTray();
                    }
                });
                return;
            }

            if (App.Window is MainWindow mw)
            {
                mw.ShowFromTray();
            }
        }
        catch
        {
            // ignore
        }
    }

    private static void QuitApp()
    {
        try
        {
            void DoQuit()
            {
                if (App.Services.GetService(typeof(ITrayService)) is ITrayService tray)
                {
                    tray.Dispose();
                }

                if (App.Window is MainWindow mw)
                {
                    mw.ForceClose();
                }
                else
                {
                    App.Window?.Close();
                }
            }

            if (App.DispatcherQueue is { } dq && !dq.HasThreadAccess)
            {
                dq.TryEnqueue(DoQuit);
                return;
            }

            DoQuit();
        }
        catch
        {
            Environment.Exit(0);
        }
    }

    private sealed class TrayRelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;

        public TrayRelayCommand(Action action) => _action = action;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _action();
    }
}
