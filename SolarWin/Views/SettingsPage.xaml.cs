using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private void OnLoggedOut(object? sender, EventArgs e)
    {
        if (App.Window is MainWindow mainWindow)
        {
            mainWindow.NavigateToStart(typeof(LoginPage));
        }
    }
}
