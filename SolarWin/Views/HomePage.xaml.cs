using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Models;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class HomePage : Page
{
    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        ViewModel = App.Services.GetRequiredService<HomeViewModel>();
        InitializeComponent();
        ViewModel.NavigateToUserProfile += OnNavigateToUserProfile;
        ViewModel.NavigateToRealmDetail += OnNavigateToRealmDetail;
        ViewModel.NavigateToChatDetail += OnNavigateToChatDetail;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private void SearchResult_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is UserSearchResultItem item
            && ViewModel.ViewPersonCommand.CanExecute(item))
        {
            ViewModel.ViewPersonCommand.Execute(item);
        }
    }

    private void OnNavigateToUserProfile(object? sender, UserProfileNavArgs args)
    {
        Frame?.Navigate(typeof(UserProfilePage), args);
    }

    private void OnNavigateToRealmDetail(object? sender, RealmDetailNavArgs args)
    {
        Frame?.Navigate(typeof(RealmDetailPage), args);
    }

    private void OnNavigateToChatDetail(object? sender, ChatRoomListItem item)
    {
        Frame?.Navigate(typeof(ChatDetailPage), item);
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.NavigateToUserProfile -= OnNavigateToUserProfile;
        ViewModel.NavigateToRealmDetail -= OnNavigateToRealmDetail;
        ViewModel.NavigateToChatDetail -= OnNavigateToChatDetail;
        Unloaded -= OnUnloaded;
    }
}
