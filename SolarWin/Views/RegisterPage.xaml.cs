using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class RegisterPage : Page
{
    public RegisterViewModel ViewModel { get; }

    public RegisterPage()
    {
        ViewModel = App.Services.GetRequiredService<RegisterViewModel>();
        InitializeComponent();
        ViewModel.Registered += OnRegistered;
        Unloaded += (_, _) => ViewModel.Registered -= OnRegistered;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.XamlRoot = XamlRoot;
    }

    private void Back_OnClick(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true)
        {
            Frame.GoBack();
        }
        else
        {
            Frame?.Navigate(typeof(LoginPage));
        }
    }

    private void Password_OnChanged(object sender, RoutedEventArgs e)
        => ViewModel.Password = PasswordBox.Password;

    private void PasswordConfirm_OnChanged(object sender, RoutedEventArgs e)
        => ViewModel.PasswordConfirm = PasswordConfirmBox.Password;

    private void OnRegistered(object? sender, string accountName)
    {
        Frame?.Navigate(typeof(LoginPage), accountName);
    }
}
