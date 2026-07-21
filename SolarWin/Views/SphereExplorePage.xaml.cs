using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Models;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class SphereExplorePage : Page
{
    public SphereExploreViewModel ViewModel { get; }

    public SphereExplorePage()
    {
        ViewModel = App.Services.GetRequiredService<SphereExploreViewModel>();
        InitializeComponent();
        ViewModel.NavigateToPublisher += OnNavigateToPublisher;
        ViewModel.NavigateToPost += OnNavigateToPost;
        ViewModel.NavigateToFeed += OnNavigateToFeed;
        Unloaded += OnUnloaded;
    }

    private void MyStickerPack_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is StickerPackItemViewModel item)
        {
            ViewModel.OpenStickerPackCommand.Execute(item);
        }
    }

    private void SearchStickerPack_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is StickerPackItemViewModel item)
        {
            ViewModel.OpenStickerPackCommand.Execute(item);
        }
    }

    private void SearchStickerHit_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is StickerItemViewModel item)
        {
            ViewModel.OpenPackFromStickerHit(item);
        }
    }

    private void OwnSticker_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StickerPackItemViewModel item })
        {
            ViewModel.OwnStickerPackCommand.Execute(item);
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    private void Bookmark_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PostItemViewModel item)
        {
            ViewModel.OpenPostCommand.Execute(item);
        }
    }

    private void Draft_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PostItemViewModel item)
        {
            ViewModel.OpenPostCommand.Execute(item);
        }
    }

    private void Featured_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PostItemViewModel item)
        {
            ViewModel.OpenPostCommand.Execute(item);
        }
    }

    private void OnNavigateToPublisher(object? sender, PublisherNavArgs args)
    {
        Frame?.Navigate(typeof(PublisherDetailPage), args);
    }

    private void OnNavigateToPost(object? sender, PostItemViewModel item)
    {
        Frame?.Navigate(typeof(PostDetailPage), item);
    }

    private void OnNavigateToFeed(object? sender, PostFeedNavArgs args)
    {
        Frame?.Navigate(typeof(PostFeedPage), args);
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ViewModel.NavigateToPublisher -= OnNavigateToPublisher;
        ViewModel.NavigateToPost -= OnNavigateToPost;
        ViewModel.NavigateToFeed -= OnNavigateToFeed;
        Unloaded -= OnUnloaded;
    }
}
