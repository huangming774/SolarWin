using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class ChatEncryptionPage : Page
{
    public ChatDetailViewModel ViewModel { get; private set; }

    public ChatEncryptionPage()
    {
        ViewModel = App.Services.GetRequiredService<ChatDetailViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ChatDetailViewModel viewModel)
        {
            ViewModel = viewModel;
            Bindings.Update();
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
            Frame?.Navigate(typeof(ChatPage));
        }
    }
}
