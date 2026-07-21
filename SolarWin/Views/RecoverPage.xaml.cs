using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class RecoverPage : Page
{
    public RecoverViewModel ViewModel { get; }

    public RecoverPage()
    {
        ViewModel = App.Services.GetRequiredService<RecoverViewModel>();
        InitializeComponent();
        ViewModel.RecoverSucceeded += OnRecoverSucceeded;
        Unloaded += (_, _) => ViewModel.RecoverSucceeded -= OnRecoverSucceeded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.XamlRoot = XamlRoot;
        if (e.Parameter is string account && !string.IsNullOrWhiteSpace(account))
        {
            ViewModel.Account = account;
        }
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

    private void OnRecoverSucceeded(object? sender, EventArgs e)
    {
        Frame?.Navigate(typeof(ShellPage));
    }
}
