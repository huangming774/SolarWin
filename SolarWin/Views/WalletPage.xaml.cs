using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class WalletPage : Page
{
    public WalletViewModel ViewModel { get; }

    public WalletPage()
    {
        ViewModel = App.Services.GetRequiredService<WalletViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }
}
