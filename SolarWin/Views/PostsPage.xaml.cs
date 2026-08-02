using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Helpers;
using SolarWin.Services;
using SolarWin.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SolarWin.Views;

public sealed partial class PostsPage : Page
{
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;
    private CancellationTokenSource? _thumbnailLoadCts;

    public PostsViewModel ViewModel { get; }

    public PostsPage()
    {
        ViewModel = App.Services.GetRequiredService<PostsViewModel>();
        _toast = App.Services.GetRequiredService<IToastService>();
        _imageLoader = App.Services.GetRequiredService<DysonFileImageLoader>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ResetThumbnailLoads();
        if (ViewModel.Items.Count == 0 && ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        CancelThumbnailLoads();
        base.OnNavigatedFrom(e);
    }

    private async void OpenComposer_OnClick(object sender, RoutedEventArgs e)
    {
        ComposerDialog.XamlRoot = XamlRoot;
        await ComposerDialog.ShowAsync();
    }

    private void ComposerDialog_OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        ComposerTextBox.Focus(FocusState.Programmatic);
    }

    private async void PublishPost_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CreatePostCommand.CanExecute(null))
        {
            return;
        }

        await ViewModel.CreatePostCommand.ExecuteAsync(null);
        if (string.IsNullOrWhiteSpace(ViewModel.NewPostContent)
            && ViewModel.PendingAttachments.Count == 0)
        {
            ComposerDialog.Hide();
        }
    }

    private async void PickImage_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanAddAttachments)
        {
            _toast.Warning($"每条帖子最多添加 {PostsViewModel.MaxPendingAttachments} 个附件");
            return;
        }

        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(App.Window);
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.ViewMode = PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".heic");

        // Multi-select for posts
        var files = await picker.PickMultipleFilesAsync();
        if (files is null || files.Count == 0)
        {
            return;
        }

        var availableSlots = PostsViewModel.MaxPendingAttachments - ViewModel.PendingAttachments.Count;
        if (files.Count > availableSlots)
        {
            _toast.Warning($"本次只添加前 {availableSlots} 个文件；每条帖子最多 {PostsViewModel.MaxPendingAttachments} 个附件");
        }

        foreach (var file in files.Take(availableSlots))
        {
            try
            {
                var props = await file.GetBasicPropertiesAsync();
                await using var stream = await file.OpenStreamForReadAsync();
                var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "image/jpeg"
                    : file.ContentType;
                await ViewModel.AttachLocalImageAsync(stream, file.Name, contentType, (long)props.Size);
            }
            catch (Exception ex)
            {
                _toast.Error($"添加失败：{ex.Message}");
            }
        }
    }

    private async void PickVideo_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanAddAttachments)
        {
            _toast.Warning($"每条帖子最多添加 {PostsViewModel.MaxPendingAttachments} 个附件");
            return;
        }

        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(App.Window);
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.ViewMode = PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = PickerLocationId.VideosLibrary;
        foreach (var extension in new[] { ".mp4", ".mov", ".mkv", ".webm", ".avi", ".m4v" })
        {
            picker.FileTypeFilter.Add(extension);
        }

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        try
        {
            var properties = await file.GetBasicPropertiesAsync();
            await using var stream = await file.OpenStreamForReadAsync();
            var contentType = ResolveVideoContentType(file.Name, file.ContentType);
            await ViewModel.AttachLocalVideoAsync(stream, file.Name, contentType, (long)properties.Size);
        }
        catch (Exception ex)
        {
            _toast.Error($"添加视频失败：{ex.Message}");
        }
    }

    private static string ResolveVideoContentType(string fileName, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType)
            && contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return contentType;

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".avi" => "video/x-msvideo",
            _ => "video/mp4",
        };
    }

    private void RemovePending_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PendingPostAttachment item }
            && ViewModel.RemovePendingAttachmentCommand.CanExecute(item))
        {
            ViewModel.RemovePendingAttachmentCommand.Execute(item);
        }
    }

    private void PostList_OnContainerContentChanging(
        ListViewBase sender,
        ContainerContentChangingEventArgs args)
    {
        // GpuImage owns viewport enter/leave load & lease release; no ViewModel image window.
    }

    private void PostList_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PostItemViewModel item)
        {
            Frame?.Navigate(typeof(PostDetailPage), item);
        }
    }

    private void AuthorAvatar_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PostItemViewModel item })
        {
            OpenAuthorProfile(item);
        }
    }

    private void AuthorName_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PostItemViewModel item })
        {
            OpenAuthorProfile(item);
        }
    }

    private async void PostImage_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PostItemViewModel item })
        {
            await ShowPostImagePreviewAsync(item).ConfigureAwait(true);
        }
    }

    private async Task ShowPostImagePreviewAsync(PostItemViewModel item)
    {
        var thumbUrl = item.FirstImageUrl;
        var fullUrl = item.FullImageUrls.Count > 0
            ? item.FullImageUrls[0]
            : thumbUrl;
        if (string.IsNullOrWhiteSpace(thumbUrl) && string.IsNullOrWhiteSpace(fullUrl))
        {
            return;
        }

        // Lightbox still uses legacy BitmapImage API (ephemeral dialog, not feed cache).
        await ImagePreviewHelper.ShowAsync(
            XamlRoot,
            imageUrl: fullUrl ?? thumbUrl,
            title: "图片预览",
            fullResUrl: fullUrl,
            imageLoader: _imageLoader).ConfigureAwait(true);
    }

    private async void PostVideo_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: PostItemViewModel item } element
            || !item.HasVideo
            || !string.IsNullOrWhiteSpace(item.VideoThumbnailPath)
            || string.IsNullOrWhiteSpace(item.VideoSourceKey)) return;

        var pageToken = _thumbnailLoadCts?.Token ?? new CancellationToken(canceled: true);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(pageToken);
        void CancelRequest(object sender, RoutedEventArgs args) => requestCancellation.Cancel();
        element.Unloaded += CancelRequest;
        try
        {
            var cache = App.Services.GetRequiredService<VideoMediaCache>();
            var thumbnailPath = await cache.GetThumbnailAsync(
                item.VideoSourceKey, item.VideoName, item.VideoMimeType, 640, 360, requestCancellation.Token).ConfigureAwait(true);
            if (!requestCancellation.IsCancellationRequested
                && element.XamlRoot is not null
                && !string.IsNullOrWhiteSpace(thumbnailPath))
            {
                item.VideoThumbnailPath = thumbnailPath;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the page is left.
        }
        catch
        {
            // Thumbnail is optional; the play overlay remains available.
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
        if (sender is not FrameworkElement { Tag: PostItemViewModel item }
            || string.IsNullOrWhiteSpace(item.VideoSourceKey)) return;

        var cache = App.Services.GetRequiredService<VideoMediaCache>();
        if (!await VideoPreviewHelper.ShowAsync(
                XamlRoot, item.VideoSourceKey, item.VideoName, item.VideoMimeType, cache).ConfigureAwait(true))
        {
            _toast.Error("视频暂时无法播放");
        }
    }

    private void OpenAuthorProfile(PostItemViewModel item)
    {
        var args = item.TryCreateAuthorProfileArgs();
        if (args is null)
        {
            _toast.Warning("无法打开主页：缺少用户名");
            return;
        }

        Frame?.Navigate(typeof(UserProfilePage), args);
    }
}
