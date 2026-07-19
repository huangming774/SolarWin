using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;

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

    private void OnRoomSelected(object? sender, ChatRoomListItem item)
    {
        Frame?.Navigate(typeof(ChatDetailPage), item);
    }
}
