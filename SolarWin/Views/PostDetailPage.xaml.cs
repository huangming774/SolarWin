using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;
using SolarWin.ViewModels;

namespace SolarWin.Views;

public sealed partial class PostDetailPage : Page
{
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;
    private CancellationTokenSource? _thumbnailLoadCts;

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
        ResetThumbnailLoads();
        if (e.Parameter is PostItemViewModel item)
        {
            ViewModel.Initialize(item);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        CancelThumbnailLoads();
        // Cancel in-flight API/image work and clear media bindings before the visual tree tears down.
        ViewModel.Cleanup();
        base.OnNavigatedFrom(e);
    }

    private async void PostImage_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string url } || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        await ImagePreviewHelper.ShowAsync(
            XamlRoot,
            imageUrl: url,
            title: "图片预览",
            fullResUrl: url,
            imageLoader: _imageLoader).ConfigureAwait(true);
    }

    private async void PostVideo_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element
            || string.IsNullOrWhiteSpace(ViewModel.VideoSourceKey)
            || !string.IsNullOrWhiteSpace(ViewModel.VideoThumbnailPath)) return;

        var pageToken = _thumbnailLoadCts?.Token ?? new CancellationToken(canceled: true);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(pageToken);
        void CancelRequest(object sender, RoutedEventArgs args) => requestCancellation.Cancel();
        element.Unloaded += CancelRequest;
        try
        {
            var cache = App.Services.GetRequiredService<VideoMediaCache>();
            var thumbnailPath = await cache.GetThumbnailAsync(
                ViewModel.VideoSourceKey,
                ViewModel.VideoName,
                ViewModel.VideoMimeType,
                960,
                540,
                requestCancellation.Token).ConfigureAwait(true);
            if (!requestCancellation.IsCancellationRequested
                && XamlRoot is not null
                && !string.IsNullOrWhiteSpace(thumbnailPath))
            {
                ViewModel.VideoThumbnailPath = thumbnailPath;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the page is left.
        }
        catch
        {
            // Keep the play overlay when thumbnail extraction fails.
        }
        finally
        {
            element.Unloaded -= CancelRequest;
        }
    }

    private void ResetThumbnailLoads()
    {
        CancelThumbnailLoads();
        _thumbnailLoadCts = new CancellationTokenSource();
    }

    private void CancelThumbnailLoads()
    {
        var cancellation = Interlocked.Exchange(ref _thumbnailLoadCts, null);
        if (cancellation is null) return;
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private async void PostVideo_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.VideoSourceKey)) return;
        var cache = App.Services.GetRequiredService<VideoMediaCache>();
        if (!await VideoPreviewHelper.ShowAsync(
                XamlRoot,
                ViewModel.VideoSourceKey,
                ViewModel.VideoName,
                ViewModel.VideoMimeType,
                cache).ConfigureAwait(true))
        {
            _toast.Error("视频暂时无法播放");
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
            Frame?.Navigate(typeof(PostsPage));
        }
    }

    private void ReplyList_OnContainerContentChanging(
        ListViewBase sender,
        ContainerContentChangingEventArgs args)
    {
        // GpuImage owns reply-avatar load / lease release via EffectiveViewport + Unloaded.
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
        CancelThumbnailLoads();
        Unloaded -= OnUnloaded;
        ViewModel.NavigateToUserProfile -= OnNavigateToUserProfile;
        // Safety net if navigated-from was skipped (rare frame edge cases).
        ViewModel.Cleanup();
    }
}
