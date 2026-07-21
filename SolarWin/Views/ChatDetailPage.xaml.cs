using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SolarWin.Helpers;
using SolarWin.Services;
using SolarWin.ViewModels;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace SolarWin.Views;

public sealed partial class ChatDetailPage : Page
{
    private readonly IToastService _toast;
    private ScrollViewer? _messageScrollViewer;
    private bool _scrollHooked;

    public ChatDetailViewModel ViewModel { get; }

    public ChatDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<ChatDetailViewModel>();
        _toast = App.Services.GetRequiredService<IToastService>();
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

    private void SenderAvatar_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: MessageItemViewModel item })
        {
            return;
        }

        var args = item.TryCreateSenderProfileArgs();
        if (args is null)
        {
            _toast.Warning("无法打开主页：缺少用户名");
            return;
        }

        Frame?.Navigate(typeof(UserProfilePage), args);
    }

    private void DraftBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Autocomplete keyboard navigation
        if (ViewModel.HasSuggestions)
        {
            if (e.Key == VirtualKey.Up)
            {
                if (ViewModel.MoveSuggestionSelection(-1))
                {
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == VirtualKey.Down)
            {
                if (ViewModel.MoveSuggestionSelection(1))
                {
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key is VirtualKey.Tab)
            {
                if (ViewModel.TryApplySelectedSuggestion())
                {
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == VirtualKey.Escape)
            {
                if (ViewModel.DismissSuggestions())
                {
                    e.Handled = true;
                    return;
                }
            }
            else if (e.Key == VirtualKey.Enter)
            {
                // Enter applies suggestion when popup is open (send with Ctrl+Enter / empty selection)
                if (ViewModel.TryApplySelectedSuggestion())
                {
                    e.Handled = true;
                    return;
                }
            }
        }
        else if (e.Key == VirtualKey.Escape && ViewModel.IsBotPanelOpen)
        {
            ViewModel.DismissSuggestions();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            if (ViewModel.SendCommand.CanExecute(null))
            {
                ViewModel.SendCommand.Execute(null);
            }

            e.Handled = true;
        }
    }

    private void BotCommand_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ChatBotCommandItem item
            && ViewModel.ApplyBotCommandCommand.CanExecute(item))
        {
            ViewModel.ApplyBotCommandCommand.Execute(item);
            DraftBox.Focus(FocusState.Programmatic);
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

    private void StickerGrid_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is StickerPickItem item
            && ViewModel.SendStickerCommand.CanExecute(item))
        {
            ViewModel.SendStickerCommand.Execute(item);
        }
    }

    private void ReplyMessage_OnClick(object sender, RoutedEventArgs e)
    {
        MessageItemViewModel? item = null;
        if (sender is FrameworkElement { Tag: MessageItemViewModel tagged })
        {
            item = tagged;
        }
        else if (sender is MenuFlyoutItem { Tag: MessageItemViewModel menuItem })
        {
            item = menuItem;
        }

        if (item is not null && ViewModel.ReplyToMessageCommand.CanExecute(item))
        {
            ViewModel.ReplyToMessageCommand.Execute(item);
            // Focus composer for typing the reply
            DraftBox?.Focus(FocusState.Programmatic);
        }
    }

    private void ReactMessage_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MessageItemViewModel item }
            && ViewModel.ReactToMessageCommand.CanExecute(item))
        {
            ViewModel.ReactToMessageCommand.Execute(item);
        }
    }

    private void PinMessage_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MessageItemViewModel item }
            && ViewModel.PinMessageCommand.CanExecute(item))
        {
            ViewModel.PinMessageCommand.Execute(item);
        }
    }

    private void EditMessage_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MessageItemViewModel item }
            && ViewModel.EditMessageCommand.CanExecute(item))
        {
            ViewModel.EditMessageCommand.Execute(item);
        }
    }

    private void DeleteMessage_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MessageItemViewModel item }
            && ViewModel.DeleteMessageCommand.CanExecute(item))
        {
            ViewModel.DeleteMessageCommand.Execute(item);
        }
    }

    private void Suggestion_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ChatSuggestionItem item
            && ViewModel.ApplySuggestionCommand.CanExecute(item))
        {
            ViewModel.ApplySuggestionCommand.Execute(item);
            DraftBox.Focus(FocusState.Programmatic);
        }
    }

    private void KickMember_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ChatMemberItemViewModel item }
            && ViewModel.KickMemberCommand.CanExecute(item))
        {
            ViewModel.KickMemberCommand.Execute(item);
        }
    }

    private void TimeoutMember_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ChatMemberItemViewModel item }
            && ViewModel.TimeoutMemberCommand.CanExecute(item))
        {
            ViewModel.TimeoutMemberCommand.Execute(item);
        }
    }

    private void ClearTimeout_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ChatMemberItemViewModel item }
            && ViewModel.ClearTimeoutMemberCommand.CanExecute(item))
        {
            ViewModel.ClearTimeoutMemberCommand.Execute(item);
        }
    }

    private void MuteCall_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: CallParticipantItemViewModel item }
            && ViewModel.MuteInCallCommand.CanExecute(item))
        {
            ViewModel.MuteInCallCommand.Execute(item);
        }
    }

    private void UnmuteCall_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: CallParticipantItemViewModel item }
            && ViewModel.UnmuteInCallCommand.CanExecute(item))
        {
            ViewModel.UnmuteInCallCommand.Execute(item);
        }
    }

    private void KickCall_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: CallParticipantItemViewModel item }
            && ViewModel.KickFromCallCommand.CanExecute(item))
        {
            ViewModel.KickFromCallCommand.Execute(item);
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
