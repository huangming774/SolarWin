using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SolarWin.Models;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
        ViewModel.LoggedOut += OnLoggedOut;
        Unloaded += (_, _) => ViewModel.LoggedOut -= OnLoggedOut;
    }

    private void Back_OnClick(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true)
        {
            Frame.GoBack();
        }
        else
        {
            Frame?.Navigate(typeof(ProfilePage));
        }
    }

    private void Security_OnClick(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(SecurityPage));
    }

    private void SwitchAccount_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SavedAccountProfile profile })
        {
            ViewModel.SwitchAccountCommand.Execute(profile);
        }
    }

    private void RemoveAccount_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SavedAccountProfile profile })
        {
            ViewModel.RemoveAccountCommand.Execute(profile);
        }
    }

    private void OnLoggedOut(object? sender, EventArgs e)
    {
        // ViewModel already navigates on the UI thread; keep this as a safety net.
        if (App.Window is MainWindow mainWindow)
        {
            mainWindow.NavigateToLogin();
        }
    }
}
