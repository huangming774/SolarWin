using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class PostDetailPage : Page
{
    private const int ReplyAvatarPrefetchItemCount = 12;

    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;
    private readonly HashSet<PostItemViewModel> _visibleReplyItems = [];

    public PostDetailViewModel ViewModel { get; }

    public PostDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<PostDetailViewModel>();
        _toast = App.Services.GetRequiredService<IToastService>();
        _imageLoader = App.Services.GetRequiredService<DysonFileImageLoader>();
        InitializeComponent();
        ViewModel.NavigateToUserProfile += OnNavigateToUserProfile;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _visibleReplyItems.Clear();
        if (e.Parameter is PostItemViewModel item)
        {
            ViewModel.Initialize(item);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _visibleReplyItems.Clear();
        ViewModel.ClearVisibleReplyImageWindow();
        base.OnNavigatedFrom(e);
    }

    private async void PostImage_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: BitmapImage image })
        {
            return;
        }

        await ImagePreviewHelper.ShowAsync(
            XamlRoot,
            image,
            title: "图片预览",
            imageLoader: _imageLoader).ConfigureAwait(true);
    }

    private async void PostImage_OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not FrameworkElement { Tag: BitmapImage image })
        {
            return;
        }

        await ImagePreviewHelper.ShowAsync(
            XamlRoot,
            image,
            title: "图片预览",
            imageLoader: _imageLoader).ConfigureAwait(true);
    }
    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Frame?.CanGoBack == true)
        {
            Frame.GoBack();
        }
        else
        {
            Frame?.Navigate(typeof(PostsPage));
        }
    }

    private void ReplyList_OnContainerContentChanging(
        ListViewBase sender,
        ContainerContentChangingEventArgs args)
    {
        if (args.Item is not PostItemViewModel item)
        {
            return;
        }

        if (args.InRecycleQueue)
        {
            _visibleReplyItems.Remove(item);
            ViewModel.UpdateVisibleReplyImageWindow(
                _visibleReplyItems,
                ReplyAvatarPrefetchItemCount);
            return;
        }

        _visibleReplyItems.RemoveWhere(candidate => !ViewModel.Replies.Contains(candidate));
        _visibleReplyItems.Add(item);
        ViewModel.UpdateVisibleReplyImageWindow(
            _visibleReplyItems,
            ReplyAvatarPrefetchItemCount);
    }

    private void ReplyAuthorAvatar_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PostItemViewModel item })
        {
            OpenReplyAuthor(item);
        }
    }

    private void ReplyAuthorName_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PostItemViewModel item })
        {
            OpenReplyAuthor(item);
        }
    }

    private void OpenReplyAuthor(PostItemViewModel item)
    {
        var args = item.TryCreateAuthorProfileArgs();
        if (args is null)
        {
            _toast.Warning("无法打开主页：缺少用户名");
            return;
        }

        Frame?.Navigate(typeof(UserProfilePage), args);
    }

    private void OnNavigateToUserProfile(object? sender, UserProfileNavArgs args)
    {
        Frame?.Navigate(typeof(UserProfilePage), args);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _visibleReplyItems.Clear();
        ViewModel.ClearVisibleReplyImageWindow();
        ViewModel.NavigateToUserProfile -= OnNavigateToUserProfile;
        Unloaded -= OnUnloaded;
    }
}
