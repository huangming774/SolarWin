using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Helpers;
using SolarWin.ViewModels;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace SolarWin.Views;

public sealed partial class ChatDetailPage : Page
{
    private ScrollViewer? _messageScrollViewer;
    private bool _scrollHooked;

    public ChatDetailViewModel ViewModel { get; }

    public ChatDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<ChatDetailViewModel>();
        InitializeComponent();
        ViewModel.ScrollToBottomRequested += OnScrollToBottomRequested;
        ViewModel.OpenImageRequested += OnOpenImageRequested;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ChatRoomListItem item)
        {
            ViewModel.Initialize(item.RoomId, item.Name);
        }
        else if (e.Parameter is Guid roomId)
        {
            ViewModel.Initialize(roomId, null);
        }

        if (ViewModel.LoadInitialCommand.CanExecute(null))
        {
            ViewModel.LoadInitialCommand.Execute(null);
        }

        ViewModel.StartPolling();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.StopPolling();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => TryHookScrollViewer();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.StopPolling();
        ViewModel.ScrollToBottomRequested -= OnScrollToBottomRequested;
        ViewModel.OpenImageRequested -= OnOpenImageRequested;
        if (_messageScrollViewer is not null)
        {
            _messageScrollViewer.ViewChanged -= MessageScrollViewer_OnViewChanged;
            _messageScrollViewer = null;
            _scrollHooked = false;
        }

        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.StopPolling();
        if (Frame?.CanGoBack == true)
        {
            Frame.GoBack();
        }
        else
        {
            Frame?.Navigate(typeof(ChatPage));
        }
    }

    private void DraftBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            if (ViewModel.SendCommand.CanExecute(null))
            {
                ViewModel.SendCommand.Execute(null);
            }

            e.Handled = true;
        }
    }

    private async void PickImage_OnClick(object sender, RoutedEventArgs e)
    {
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

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

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
            ViewModel.ErrorMessage = ex.Message;
        }
    }

    private void AttachmentImage_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MessageAttachmentViewModel att })
        {
            ViewModel.RequestOpenImage(att);
            e.Handled = true;
        }
    }

    private async void OnOpenImageRequested(object? sender, MessageAttachmentViewModel attachment)
    {
        var image = attachment.Image;
        if (image is null && !string.IsNullOrWhiteSpace(attachment.FileId ?? attachment.Url))
        {
            var loader = App.Services.GetRequiredService<DysonFileImageLoader>();
            image = await loader.LoadAsync(attachment.FileId ?? attachment.Url);
            if (image is not null)
            {
                attachment.Image = image;
                attachment.ImageOpacity = 1;
            }
        }

        if (image is null)
        {
            ViewModel.ErrorMessage = "无法加载图片。";
            return;
        }

        var preview = new Image
        {
            Source = image,
            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
            MaxWidth = 720,
            MaxHeight = 720,
        };

        var dialog = new ContentDialog
        {
            Title = attachment.Name,
            Content = new ScrollViewer
            {
                Content = preview,
                HorizontalScrollMode = ScrollMode.Auto,
                VerticalScrollMode = ScrollMode.Auto,
            },
            CloseButtonText = "关闭",
            XamlRoot = XamlRoot,
        };

        await dialog.ShowAsync();
    }

    private void TryHookScrollViewer()
    {
        if (_scrollHooked)
        {
            return;
        }

        _messageScrollViewer = FindScrollViewer(MessageList);
        if (_messageScrollViewer is null)
        {
            return;
        }

        _messageScrollViewer.ViewChanged += MessageScrollViewer_OnViewChanged;
        _scrollHooked = true;
    }

    private void MessageScrollViewer_OnViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (e.IsIntermediate || _messageScrollViewer is null)
        {
            return;
        }

        if (_messageScrollViewer.VerticalOffset <= 48 && ViewModel.LoadMoreCommand.CanExecute(null))
        {
            ViewModel.LoadMoreCommand.Execute(null);
        }
    }

    private void OnScrollToBottomRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TryHookScrollViewer();
            if (ViewModel.Messages.Count == 0)
            {
                return;
            }

            MessageList.ScrollIntoView(ViewModel.Messages[^1]);
        });
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv)
        {
            return sv;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var result = FindScrollViewer(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }
}
