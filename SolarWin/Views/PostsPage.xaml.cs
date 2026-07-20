using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class PostsPage : Page
{
    public PostsViewModel ViewModel { get; }

    public PostsPage()
    {
        ViewModel = App.Services.GetRequiredService<PostsViewModel>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.Items.Count == 0 && ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private void PostList_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PostItemViewModel item)
        {
            Frame?.Navigate(typeof(PostDetailPage), item);
        }
    }
}
