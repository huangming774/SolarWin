using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
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

    /// <summary>
    /// DataTemplate actions: avoid classic {Binding ElementName=…} reflection.
    /// Tag = item, AutomationProperties.Name = command property name on ViewModel.
    /// </summary>
    private void SocialAction_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe
            || fe.Tag is not SocialListItemViewModel item)
        {
            return;
        }

        var cmdName = Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(fe);
        if (string.IsNullOrWhiteSpace(cmdName))
        {
            return;
        }

        var prop = typeof(HomeViewModel).GetProperty(cmdName);
        if (prop?.GetValue(ViewModel) is IRelayCommand cmd && cmd.CanExecute(item))
        {
            cmd.Execute(item);
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync();
    }

    private async void Sections_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is Pivot pivot)
        {
            await ViewModel.LoadSectionAsync(pivot.SelectedIndex);
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
