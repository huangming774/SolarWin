using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Services;
using SolarWin.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SolarWin.Views;

public sealed partial class PostsPage : Page
{
    private readonly IToastService _toast;

    public PostsViewModel ViewModel { get; }

    public PostsPage()
    {
        ViewModel = App.Services.GetRequiredService<PostsViewModel>();
        _toast = App.Services.GetRequiredService<IToastService>();
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
