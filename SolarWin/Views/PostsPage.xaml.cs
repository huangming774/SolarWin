using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Helpers;
using SolarWin.Services;
using SolarWin.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SolarWin.Views;

public sealed partial class PostsPage : Page
{
    private const int ImagePrefetchItemCount = 16;

    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;
    private readonly HashSet<PostItemViewModel> _visibleImageItems = [];

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
        _visibleImageItems.Clear();
        if (ViewModel.Items.Count == 0 && ViewModel.LoadCommand.CanExecute(null))
        {
            ViewModel.LoadCommand.Execute(null);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _visibleImageItems.Clear();
        ViewModel.ClearVisibleImageWindow();
        base.OnNavigatedFrom(e);
    }

    private async void PickImage_OnClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsComposerEnabled)
        {
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

        foreach (var file in files)
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
        if (args.Item is not PostItemViewModel item)
        {
            return;
        }

        if (args.InRecycleQueue)
        {
            _visibleImageItems.Remove(item);
            ViewModel.UpdateVisibleImageWindow(_visibleImageItems, ImagePrefetchItemCount);
            return;
        }

        _visibleImageItems.RemoveWhere(candidate => !ViewModel.Items.Contains(candidate));
        _visibleImageItems.Add(item);
        ViewModel.UpdateVisibleImageWindow(_visibleImageItems, ImagePrefetchItemCount);
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

    private async void PostImage_OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { Tag: PostItemViewModel item })
        {
            await ShowPostImagePreviewAsync(item).ConfigureAwait(true);
        }
    }

    private async Task ShowPostImagePreviewAsync(PostItemViewModel item)
    {
        if (item.FirstImage is null)
        {
            return;
        }

        var fullUrl = item.FullImageUrls.Count > 0
            ? item.FullImageUrls[0]
            : item.ImageUrls.Count > 0 ? item.ImageUrls[0] : null;

        await ImagePreviewHelper.ShowAsync(
            XamlRoot,
            item.FirstImage,
            title: "图片预览",
            fullResUrl: fullUrl,
            imageLoader: _imageLoader).ConfigureAwait(true);
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
