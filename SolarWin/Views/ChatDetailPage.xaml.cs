using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
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
    private bool _windowActivationHooked;
    private bool _windowWasInactive;
    private CancellationTokenSource? _thumbnailLoadCts;

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
        ResetThumbnailLoads();

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
        CancelThumbnailLoads();
        ViewModel.StopPolling();
        ViewModel.CancelPendingLoads();
        ViewModel.Unhook();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TryHookScrollViewer();
        if (!_windowActivationHooked)
        {
            _windowActivationHooked = true;
            App.Window.Activated += Window_OnActivated;
        }
    }

    private void Window_OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            _windowWasInactive = true;
            return;
        }

        if (!_windowWasInactive)
        {
            return;
        }

        _windowWasInactive = false;
        _ = ViewModel.CompensateAfterResumeAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_windowActivationHooked)
        {
            _windowActivationHooked = false;
            App.Window.Activated -= Window_OnActivated;
        }

        _windowWasInactive = false;
        CancelThumbnailLoads();
        ViewModel.StopPolling();
        ViewModel.CancelPendingLoads();
        ViewModel.Unhook();
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
        ViewModel.CancelPendingLoads();
        if (Frame?.CanGoBack == true)
        {
            Frame.GoBack();
        }
        else
        {
            Frame?.Navigate(typeof(ChatPage));
        }
    }

    private void EncryptionSettings_OnClick(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(ChatEncryptionPage), ViewModel);
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

    private void MessageBubble_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Border
            {
                Tag: MessageItemViewModel item,
                ActualWidth: > 0,
                ActualHeight: > 0,
            } bubble
            || !item.ConsumeSendAnimation())
        {
            return;
        }

        var visual = ElementCompositionPreview.GetElementVisual(bubble);
        var compositor = visual.Compositor;
        visual.CenterPoint = new Vector3(
            item.IsMine ? (float)bubble.ActualWidth : 0f,
            (float)bubble.ActualHeight,
            0f);

        SpringVector3NaturalMotionAnimation scale = compositor.CreateSpringVector3Animation();
        scale.InitialValue = new Vector3(0.72f, 0.72f, 1f);
        scale.FinalValue = Vector3.One;
        scale.DampingRatio = 0.72f;
        scale.Period = TimeSpan.FromMilliseconds(180);

        var restingOffset = visual.Offset;
        SpringVector3NaturalMotionAnimation lift = compositor.CreateSpringVector3Animation();
        lift.InitialValue = restingOffset + new Vector3(item.IsMine ? 18f : -18f, 10f, 0f);
        lift.FinalValue = restingOffset;
        lift.DampingRatio = 0.78f;
        lift.Period = TimeSpan.FromMilliseconds(210);

        ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 0f);
        fade.InsertKeyFrame(1f, (float)item.BubbleOpacity);
        fade.Duration = TimeSpan.FromMilliseconds(110);

        visual.StartAnimation(nameof(visual.Scale), scale);
        visual.StartAnimation(nameof(visual.Offset), lift);
        visual.StartAnimation(nameof(visual.Opacity), fade);
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
        picker.FileTypeFilter.Add(".mp4");
        picker.FileTypeFilter.Add(".mov");
        picker.FileTypeFilter.Add(".mkv");
        picker.FileTypeFilter.Add(".webm");
        picker.FileTypeFilter.Add(".avi");
        picker.FileTypeFilter.Add(".m4v");

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            var props = await file.GetBasicPropertiesAsync();
            await using var stream = await file.OpenStreamForReadAsync();
            var contentType = ResolveMediaContentType(file.Name, file.ContentType);
            await ViewModel.AttachLocalMediaAsync(stream, file.Name, contentType, (long)props.Size);
        }
        catch (Exception ex)
        {
            ViewModel.ErrorMessage = ex.Message;
        }
    }

    private static string ResolveMediaContentType(string fileName, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(contentType)
            && !string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return contentType;
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".mp4" or ".m4v" => "video/mp4",
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".avi" => "video/x-msvideo",
            _ => "image/jpeg",
        };
    }

    private void AttachmentImage_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MessageAttachmentViewModel att })
        {
            ViewModel.RequestOpenImage(att);
            e.Handled = true;
        }
    }

    private async void AttachmentVideo_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: MessageAttachmentViewModel att } element
            || !att.IsVideo
            || !string.IsNullOrWhiteSpace(att.VideoThumbnailPath)
            || string.IsNullOrWhiteSpace(att.VideoSourceKey)
            || att.File.Size > 128L * 1024 * 1024)
        {
            return;
        }

        var pageToken = _thumbnailLoadCts?.Token ?? new CancellationToken(canceled: true);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(pageToken);
        void CancelRequest(object sender, RoutedEventArgs args) => requestCancellation.Cancel();
        element.Unloaded += CancelRequest;
        try
        {
            var cache = App.Services.GetRequiredService<VideoMediaCache>();
            var thumbnailPath = await cache.GetThumbnailAsync(
                att.VideoSourceKey, att.Name, att.MimeType, 640, 360, requestCancellation.Token).ConfigureAwait(true);
            if (!requestCancellation.IsCancellationRequested
                && element.XamlRoot is not null
                && !string.IsNullOrWhiteSpace(thumbnailPath))
            {
                att.VideoThumbnailPath = thumbnailPath;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the page is left or a recycled container no longer needs the result.
        }
        catch
        {
            // The play button remains available even when this codec has no thumbnail decoder.
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

    private async void AttachmentVideo_OnTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MessageAttachmentViewModel att }
            && !string.IsNullOrWhiteSpace(att.VideoSourceKey))
        {
            var cache = App.Services.GetRequiredService<VideoMediaCache>();
            if (!await VideoPreviewHelper.ShowAsync(
                    XamlRoot, att.VideoSourceKey, att.Name, att.MimeType, cache).ConfigureAwait(true))
            {
                _toast.Error("视频下载或解码失败");
            }
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

    private void VideoTile_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CallVideoTile tile
            && ViewModel.FocusVideoTileCommand.CanExecute(tile))
        {
            ViewModel.FocusVideoTileCommand.Execute(tile);
        }
    }

    private void MicDevice_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.ApplyMicDeviceCommand.CanExecute(null))
        {
            ViewModel.ApplyMicDeviceCommand.Execute(null);
        }
    }

    private void SpeakerDevice_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.ApplySpeakerDeviceCommand.CanExecute(null))
        {
            ViewModel.ApplySpeakerDeviceCommand.Execute(null);
        }
    }

    private void CameraDevice_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.ApplyCameraDeviceCommand.CanExecute(null))
        {
            ViewModel.ApplyCameraDeviceCommand.Execute(null);
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
        var loader = App.Services.GetRequiredService<DysonFileImageLoader>();
        var key = attachment.ImageSourceKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            ViewModel.ErrorMessage = "无法加载图片。";
            return;
        }

        // Lightbox uses legacy BitmapImage path; single-dialog gate in ImagePreviewHelper.
        await ImagePreviewHelper.ShowAsync(
            XamlRoot,
            imageUrl: key,
            title: string.IsNullOrWhiteSpace(attachment.Name) ? "图片预览" : attachment.Name,
            fullResUrl: key,
            imageLoader: loader).ConfigureAwait(true);
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

        // Top → load older history.
        if (_messageScrollViewer.VerticalOffset <= 48 && ViewModel.LoadMoreCommand.CanExecute(null))
        {
            ViewModel.LoadMoreCommand.Execute(null);
        }

        // Bottom → Phase 5 rehydrate detached newer tail from L2.
        var distanceFromBottom =
            _messageScrollViewer.ScrollableHeight - _messageScrollViewer.VerticalOffset;
        if (distanceFromBottom <= 72 && ViewModel.HasDetachedNewer)
        {
            ViewModel.NotifyNearBottom();
        }
    }

    private void OnScrollToBottomRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TryHookScrollViewer();

            // Phase 5: restore trimmed newest side from SQLite before scrolling.
            if (ViewModel.HasDetachedNewer)
            {
                ViewModel.NotifyNearBottom();
            }

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
