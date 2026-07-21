using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Models;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class UserProfilePage : Page
{
    public UserProfileViewModel ViewModel { get; }

    public UserProfilePage()
    {
        ViewModel = App.Services.GetRequiredService<UserProfileViewModel>();
        InitializeComponent();
        ViewModel.DirectChatReady += OnDirectChatReady;
        Unloaded += (_, _) => ViewModel.DirectChatReady -= OnDirectChatReady;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        switch (e.Parameter)
        {
            case UserProfileNavArgs args:
                ViewModel.Initialize(args);
                break;
            case string name when !string.IsNullOrWhiteSpace(name):
                ViewModel.Initialize(new UserProfileNavArgs(name));
                break;
            case SnAccount acc when !string.IsNullOrWhiteSpace(acc.Name):
                ViewModel.Initialize(new UserProfileNavArgs(acc.Name, acc.Id, acc.Nick));
                break;
        }
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true)
        {
            Frame.GoBack();
        }
        else
        {
            Frame?.Navigate(typeof(HomePage));
        }
    }

    private void OnDirectChatReady(object? sender, ChatRoomListItem item)
    {
        Frame?.Navigate(typeof(ChatDetailPage), item);
    }
}
