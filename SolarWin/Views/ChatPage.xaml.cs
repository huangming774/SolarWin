using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;
using Windows.System;

namespace SolarWin.Views;

public sealed partial class ChatPage : Page
{
    public ChatViewModel ViewModel { get; }

    public ChatPage()
    {
        ViewModel = App.Services.GetRequiredService<ChatViewModel>();
        InitializeComponent();
        ViewModel.RoomSelected += OnRoomSelected;
        Unloaded += (_, _) => ViewModel.RoomSelected -= OnRoomSelected;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        // Uses cache when fresh; silent refresh when stale; full load only if empty.
        if (ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private void RoomList_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ChatRoomListItem item)
        {
            ViewModel.OpenRoomCommand.Execute(item);
        }
    }

    private void AcceptInvite_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ChatRoomListItem item }
            && ViewModel.AcceptInviteCommand.CanExecute(item))
        {
            ViewModel.AcceptInviteCommand.Execute(item);
        }
    }

    private void DeclineInvite_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ChatRoomListItem item }
            && ViewModel.DeclineInviteCommand.CanExecute(item))
        {
            ViewModel.DeclineInviteCommand.Execute(item);
        }
    }

    private void GroupList_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ChatGroupItemViewModel group
            && ViewModel.UseGroupCommand.CanExecute(group))
        {
            ViewModel.UseGroupCommand.Execute(group);
        }
    }

    private void UserSearchBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (ViewModel.SearchUsersCommand.CanExecute(null))
            {
                ViewModel.SearchUsersCommand.Execute(null);
            }

            e.Handled = true;
        }
    }

    private void UserSearch_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is UserSearchResultItem user
            && ViewModel.StartDirectChatWithUserCommand.CanExecute(user))
        {
            ViewModel.StartDirectChatWithUserCommand.Execute(user);
        }
    }

    private void StartDm_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: UserSearchResultItem user }
            && ViewModel.StartDirectChatWithUserCommand.CanExecute(user))
        {
            ViewModel.StartDirectChatWithUserCommand.Execute(user);
        }
    }

    private void OnRoomSelected(object? sender, ChatRoomListItem item)
    {
        Frame?.Navigate(typeof(ChatDetailPage), item);
    }
}
