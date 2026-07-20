using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class ChatDetailViewModel : ObservableObject
{
    private const int PageSize = 30;
    private static readonly TimeSpan SyncInterval = TimeSpan.FromSeconds(10);

    private readonly ISolarApiClient _api;
    private readonly IAuthService _authService;
    private readonly DysonFileImageLoader _imageLoader;
    private readonly ChatViewModel _chatList;
    private readonly HashSet<Guid> _knownMessageIds = [];

    private CancellationTokenSource? _syncCts;
    private int _offset;
    private bool _hasMore = true;
    private long _lastSyncTimestamp;
    private Guid? _lastSyncMessageId;
    private bool _markedRead;

    public ChatDetailViewModel(
        ISolarApiClient api,
        IAuthService authService,
        DysonFileImageLoader imageLoader,
        ChatViewModel chatList)
    {
        _api = api;
        _authService = authService;
        _imageLoader = imageLoader;
        _chatList = chatList;
    }

    public ObservableCollection<MessageItemViewModel> Messages { get; } = [];

    public Guid RoomId { get; private set; }

    [ObservableProperty]
    public partial string RoomTitle { get; set; } = "聊天";

    [ObservableProperty]
    public partial string Draft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? PendingImageName { get; set; }

    [ObservableProperty]
    public partial string? PendingImagePath { get; set; }

    [ObservableProperty]
    public partial string? PendingImageFileId { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    [ObservableProperty]
    public partial bool IsSending { get; set; }

    [ObservableProperty]
    public partial bool IsUploadingImage { get; set; }

    [ObservableProperty]
    public partial double UploadProgress { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool CanSend { get; set; }

    [ObservableProperty]
    public partial bool IsComposerEnabled { get; set; } = true;

    [ObservableProperty]
    public partial double PendingImagePanelOpacity { get; set; }

    public event EventHandler? ScrollToBottomRequested;
    public event EventHandler? OlderMessagesLoaded;
    public event EventHandler<MessageAttachmentViewModel>? OpenImageRequested;

    public void Initialize(Guid roomId, string? title)
    {
        RoomId = roomId;
        RoomTitle = string.IsNullOrWhiteSpace(title) ? "聊天" : title!;
        Messages.Clear();
        _knownMessageIds.Clear();
        _offset = 0;
        _hasMore = true;
        _lastSyncTimestamp = 0;
        _lastSyncMessageId = null;
        Draft = string.Empty;
        ClearPendingImage();
        ErrorMessage = null;
        _markedRead = false;
        UpdateCanSend();

        // Clear unread badge immediately when opening room
        _chatList.MarkRoomReadLocal(roomId);
    }

    partial void OnDraftChanged(string value) => UpdateCanSend();

    partial void OnIsSendingChanged(bool value) => UpdateCanSend();

    partial void OnPendingImageFileIdChanged(string? value) => UpdateCanSend();

    private void UpdateCanSend()
    {
        CanSend = !IsSending
            && !IsUploadingImage
            && (!string.IsNullOrWhiteSpace(Draft) || !string.IsNullOrWhiteSpace(PendingImageFileId));
        IsComposerEnabled = !IsSending && !IsUploadingImage;
        PendingImagePanelOpacity = string.IsNullOrWhiteSpace(PendingImageFileId) && string.IsNullOrWhiteSpace(PendingImageName)
            ? 0.0
            : 1.0;
    }

    [RelayCommand]
    private async Task LoadInitialAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            Messages.Clear();
            _knownMessageIds.Clear();
            _offset = 0;
            _hasMore = true;

            var batch = await _api.GetMessagesAsync(RoomId.ToString(), offset: 0, take: PageSize).ConfigureAwait(true);
            var ordered = NormalizeOrder(batch);
            foreach (var msg in ordered)
            {
                AddMessageInternal(msg, append: true);
            }

            _offset = batch.Count;
            _hasMore = batch.Count >= PageSize;
            UpdateSyncCursorFromMessages();
            ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
            _ = LoadMediaAsync();
            _ = MarkRoomReadIfNeededAsync();
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task MarkRoomReadIfNeededAsync()
    {
        if (_markedRead || RoomId == Guid.Empty)
        {
            return;
        }

        _markedRead = true;
        try
        {
            await _chatList.MarkRoomReadAsync(RoomId).ConfigureAwait(true);
        }
        catch
        {
            // local already cleared
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (RoomId == Guid.Empty || IsLoadingMore || IsBusy || !_hasMore)
        {
            return;
        }

        try
        {
            IsLoadingMore = true;
            var batch = await _api.GetMessagesAsync(RoomId.ToString(), offset: _offset, take: PageSize).ConfigureAwait(true);
            if (batch.Count == 0)
            {
                _hasMore = false;
                return;
            }

            var ordered = NormalizeOrder(batch);
            for (var i = ordered.Count - 1; i >= 0; i--)
            {
                AddMessageInternal(ordered[i], append: false);
            }

            _offset += batch.Count;
            _hasMore = batch.Count >= PageSize;
            OlderMessagesLoaded?.Invoke(this, EventArgs.Empty);
            _ = LoadMediaAsync();
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (RoomId == Guid.Empty || IsSending)
        {
            return;
        }

        var text = Draft?.Trim();
        var imageId = PendingImageFileId;
        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(imageId))
        {
            return;
        }

        try
        {
            IsSending = true;
            ErrorMessage = null;

            var clientMessageId = Guid.NewGuid().ToString("N");
            var request = new SendMessageRequest
            {
                Content = string.IsNullOrWhiteSpace(text) ? null : text,
                ClientMessageId = clientMessageId,
                Nonce = Guid.NewGuid().ToString("N")[..16],
                AttachmentsId = string.IsNullOrWhiteSpace(imageId) ? null : [imageId!],
            };

            await _api.SendMessageAsync(RoomId.ToString(), request).ConfigureAwait(true);
            Draft = string.Empty;
            ClearPendingImage();

            // Show immediately; the sync copy replaces the echo in place (matched by client_message_id).
            AddLocalEcho(text, imageId, clientMessageId);
            ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
            _ = LoadMediaAsync();
            _ = ReconcileAfterSendAsync();
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSending = false;
        }
    }

    private void AddLocalEcho(string? text, string? imageId, string clientMessageId)
    {
        var account = _authService.CurrentAccount;
        var echo = new SnChatMessage
        {
            Id = Guid.Empty,
            ClientMessageId = clientMessageId,
            ChatRoomId = RoomId,
            Type = string.IsNullOrWhiteSpace(imageId) ? "text" : "image",
            Content = text,
            CreatedAt = DateTimeOffset.Now,
            SenderId = account?.Id ?? Guid.Empty,
            Sender = account is null
                ? null
                : new SnChatMember
                {
                    AccountId = account.Id,
                    Account = account,
                    Nick = account.Nick,
                },
            Attachments = string.IsNullOrWhiteSpace(imageId)
                ? null
                : [new SnCloudFile { Id = imageId, MimeType = "image/jpeg" }],
        };

        AddMessageInternal(echo, append: true);
    }

    private async Task ReconcileAfterSendAsync()
    {
        try
        {
            await SyncOnceAsync().ConfigureAwait(true);
        }
        catch
        {
            // Poll loop retries; the echo stays until the server copy arrives.
        }
    }

    /// <summary>Upload local image to DysonFS then keep file id for send.</summary>
    public async Task AttachLocalImageAsync(Stream stream, string fileName, string contentType, long size)
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            IsUploadingImage = true;
            UploadProgress = 0;
            ErrorMessage = null;
            PendingImageName = fileName;
            PendingImageFileId = null;

            var progress = new Progress<double>(p => UploadProgress = p);
            var file = await _api.UploadFileDirectAsync(
                stream,
                fileName,
                contentType,
                size,
                parentId: null,
                progress,
                CancellationToken.None).ConfigureAwait(true);

            var id = file.Id ?? CloudFileUrlHelper.ResolveFileId(file);
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new SolarApiException("上传成功但未返回文件 id。");
            }

            PendingImageFileId = id;
            PendingImageName = file.Name ?? fileName;
            UploadProgress = 1;
            UpdateCanSend();
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            ClearPendingImage();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ClearPendingImage();
        }
        finally
        {
            IsUploadingImage = false;
            UpdateCanSend();
        }
    }

    [RelayCommand]
    private void ClearPendingImage()
    {
        PendingImageName = null;
        PendingImagePath = null;
        PendingImageFileId = null;
        UploadProgress = 0;
        UpdateCanSend();
    }

    public void RequestOpenImage(MessageAttachmentViewModel attachment)
        => OpenImageRequested?.Invoke(this, attachment);

    public void StartPolling()
    {
        StopPolling();
        _syncCts = new CancellationTokenSource();
        _ = PollLoopAsync(_syncCts.Token);
    }

    public void StopPolling()
    {
        if (_syncCts is null)
        {
            return;
        }

        try
        {
            _syncCts.Cancel();
        }
        catch
        {
            // ignore
        }

        _syncCts.Dispose();
        _syncCts = null;
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SyncInterval, cancellationToken).ConfigureAwait(true);
                await SyncOnceAsync(cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // keep polling
            }
        }
    }

    private async Task SyncOnceAsync(CancellationToken cancellationToken = default)
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        var request = new SyncRequest
        {
            LastSyncTimestamp = _lastSyncTimestamp,
            LastSyncMessageId = _lastSyncMessageId,
        };

        var response = await _api.SyncRoomMessagesAsync(RoomId.ToString(), request, cancellationToken)
            .ConfigureAwait(true);

        var incoming = response.Messages ?? [];
        var added = false;
        foreach (var msg in NormalizeOrder(incoming))
        {
            if (AddMessageInternal(msg, append: true))
            {
                added = true;
            }
        }

        if (response.CurrentTimestamp is { } serverTs)
        {
            _lastSyncTimestamp = serverTs.ToUnixTimeMilliseconds();
        }
        else if (incoming.Count > 0)
        {
            UpdateSyncCursorFromMessages();
        }

        if (added)
        {
            ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
            _ = LoadMediaAsync();
        }
    }

    private bool AddMessageInternal(SnChatMessage message, bool append)
    {
        var currentId = _authService.CurrentAccount?.Id;

        // Reconcile optimistic echoes: the server copy carries the same client_message_id.
        if (!string.IsNullOrEmpty(message.ClientMessageId))
        {
            for (var i = 0; i < Messages.Count; i++)
            {
                var existing = Messages[i].Message;
                if (existing.Id == Guid.Empty
                    && string.Equals(existing.ClientMessageId, message.ClientMessageId, StringComparison.Ordinal))
                {
                    if (message.Id != Guid.Empty)
                    {
                        _knownMessageIds.Add(message.Id);
                    }

                    Messages[i] = new MessageItemViewModel(message, currentId, _imageLoader);
                    return true;
                }
            }
        }

        if (message.Id != Guid.Empty && !_knownMessageIds.Add(message.Id))
        {
            return false;
        }

        var item = new MessageItemViewModel(message, currentId, _imageLoader);

        if (append)
        {
            if (Messages.Count > 0 && message.RoomSequence > 0)
            {
                var last = Messages[^1];
                if (message.RoomSequence < last.RoomSequence)
                {
                    var idx = Messages.Count - 1;
                    while (idx >= 0 && Messages[idx].RoomSequence > message.RoomSequence)
                    {
                        idx--;
                    }

                    Messages.Insert(idx + 1, item);
                    return true;
                }
            }

            Messages.Add(item);
        }
        else
        {
            Messages.Insert(0, item);
        }

        return true;
    }

    private void UpdateSyncCursorFromMessages()
    {
        if (Messages.Count == 0)
        {
            return;
        }

        var last = Messages[^1].Message;
        _lastSyncMessageId = last.Id == Guid.Empty ? null : last.Id;
        if (last.CreatedAt is { } created)
        {
            _lastSyncTimestamp = created.ToUnixTimeMilliseconds();
        }
        else if (last.RoomSequence > _lastSyncTimestamp)
        {
            _lastSyncTimestamp = last.RoomSequence;
        }
    }

    private bool _mediaLoading;

    private async Task LoadMediaAsync()
    {
        // Single-flight: send/poll/load-more can all trigger this concurrently.
        if (_mediaLoading)
        {
            return;
        }

        _mediaLoading = true;
        try
        {
            var avatarTasks = new List<(MessageItemViewModel Msg, Task<BitmapImage?> Task)>();
            var attachmentTasks = new List<(MessageAttachmentViewModel Att, Task<BitmapImage?> Task)>();

            foreach (var msg in Messages.ToList())
            {
                if (!msg.AvatarAuthenticated && !string.IsNullOrWhiteSpace(msg.AvatarUrl))
                {
                    avatarTasks.Add((msg, _imageLoader.LoadSafeAsync(msg.AvatarUrl)));
                }

                foreach (var att in msg.Attachments.Where(a => a.IsImage && a.Image is null))
                {
                    var key = att.FileId ?? att.Url;
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    att.IsLoading = true;
                    attachmentTasks.Add((att, _imageLoader.LoadSafeAsync(key)));
                }
            }

            if (avatarTasks.Count == 0 && attachmentTasks.Count == 0)
            {
                return;
            }

            // Download in parallel, then apply in one UI turn: avatars popping in one by
            // one down the list reads as flickering.
            await Task.WhenAll(
                avatarTasks.Select(t => t.Task)
                    .Concat(attachmentTasks.Select(t => t.Task))).ConfigureAwait(true);

            foreach (var (msg, task) in avatarTasks)
            {
                if (task.Result is { } bmp)
                {
                    msg.SetAuthenticatedAvatar(bmp);
                }
            }

            foreach (var (att, task) in attachmentTasks)
            {
                if (task.Result is { } bmp)
                {
                    att.Image = bmp;
                    att.ImageOpacity = 1.0;
                }

                att.IsLoading = false;
            }
        }
        finally
        {
            _mediaLoading = false;
        }
    }

    private static List<SnChatMessage> NormalizeOrder(List<SnChatMessage> batch)
    {
        if (batch.Count <= 1)
        {
            return batch;
        }

        var first = batch[0];
        var last = batch[^1];

        var firstKey = first.RoomSequence != 0
            ? first.RoomSequence
            : first.CreatedAt?.ToUnixTimeMilliseconds() ?? 0;
        var lastKey = last.RoomSequence != 0
            ? last.RoomSequence
            : last.CreatedAt?.ToUnixTimeMilliseconds() ?? 0;

        if (firstKey > lastKey)
        {
            return batch.AsEnumerable().Reverse().ToList();
        }

        if (batch.Any(m => m.RoomSequence != 0))
        {
            return batch.OrderBy(m => m.RoomSequence).ThenBy(m => m.CreatedAt).ToList();
        }

        return batch.OrderBy(m => m.CreatedAt).ToList();
    }
}
