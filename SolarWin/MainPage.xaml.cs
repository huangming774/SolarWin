using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Services;
using SolarWin.ViewModels;
using SolarWin.Views;

namespace SolarWin;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; }

    public MainPage()
    {
        ViewModel = App.Services.GetRequiredService<MainPageViewModel>();
        InitializeComponent();
        ViewModel.LoggedOut += OnLoggedOut;
        Unloaded += (_, _) => ViewModel.LoggedOut -= OnLoggedOut;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (!App.Services.GetRequiredService<IAuthService>().IsAuthenticated)
        {
            Frame?.Navigate(typeof(LoginPage));
        }
    }

    private void OnLoggedOut(object? sender, EventArgs e)
    {
        Frame?.Navigate(typeof(LoginPage));
    }
}
