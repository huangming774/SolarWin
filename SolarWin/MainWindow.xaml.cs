using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace SolarWin;

/// <summary>
/// Application shell window hosting the root navigation frame.
/// Min client size: 900×600.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int MinWidth = 900;
    private const int MinHeight = 600;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        // Title bar / taskbar icon (from Assets/icon.ico)
        AppWindow.SetIcon("Assets/icon.ico");

        // Comfortable default size; clamp to min on resize.
        AppWindow.Resize(new SizeInt32(1180, 760));
        AppWindow.Changed += AppWindow_OnChanged;

        if (Content is FrameworkElement root)
        {
            root.SizeChanged += Root_OnSizeChanged;
        }
    }

    public void NavigateToStart(Type pageType)
    {
        RootFrame.Navigate(pageType);
    }

    public Frame GetRootFrame() => RootFrame;

    private void AppWindow_OnChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange)
        {
            return;
        }

        EnforceMinSize();
    }

    private void Root_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        EnforceMinSize();
    }

    private void EnforceMinSize()
    {
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
