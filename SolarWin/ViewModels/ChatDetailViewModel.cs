using System.Collections.Concurrent;
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

    /// <summary>Session cache: <c>:prefix+slug:</c> or <c>prefix+slug</c> → DysonFS file id.</summary>
    private static readonly ConcurrentDictionary<string, string> StickerFileIdCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ISolarApiClient _api;
    private readonly IAuthService _authService;
    private readonly IToastService _toast;
    private readonly IVoiceRecorderService _voiceRecorder;
    private readonly IChatWebSocketService _ws;
    private readonly IChatMessageNotifier _messageNotifier;
    private readonly IChatDataCache _cache;
    private readonly DysonFileImageLoader _imageLoader;
    private readonly ChatViewModel _chatList;
    private readonly HashSet<Guid> _knownMessageIds = [];
    private readonly List<ChatBotCommand> _botCommands = [];

    private CancellationTokenSource? _syncCts;
    private CancellationTokenSource? _suggestCts;
    private int _offset;
    private bool _hasMore = true;
    private long _lastSyncTimestamp;
    private Guid? _lastSyncMessageId;
    private bool _markedRead;
    private bool _wsHooked;
    private JoinCallResponse? _activeCall;

    public ChatDetailViewModel(
        ISolarApiClient api,
        IAuthService authService,
        IToastService toast,
        IVoiceRecorderService voiceRecorder,
        IChatWebSocketService ws,
        IChatMessageNotifier messageNotifier,
        IChatDataCache cache,
        DysonFileImageLoader imageLoader,
        ChatViewModel chatList)
    {
        _api = api;
        _authService = authService;
        _toast = toast;
        _voiceRecorder = voiceRecorder;
        _ws = ws;
        _messageNotifier = messageNotifier;
        _cache = cache;
        _imageLoader = imageLoader;
        _chatList = chatList;
        _voiceRecorder.ElapsedChanged += OnVoiceElapsedChanged;
    }

    public ObservableCollection<MessageItemViewModel> Messages { get; } = [];

    public ObservableCollection<ChatSuggestionItem> Suggestions { get; } = [];

    /// <summary>All bot commands for this room (panel + slash filter).</summary>
    public ObservableCollection<ChatBotCommandItem> BotCommands { get; } = [];

    public ObservableCollection<ChatMemberItemViewModel> Members { get; } = [];

    public ObservableCollection<CallParticipantItemViewModel> CallParticipants { get; } = [];

    public Guid RoomId { get; private set; }

    [ObservableProperty]
    public partial string RoomTitle { get; set; } = "聊天";

    [ObservableProperty]
    public partial string Draft { get; set; } = string.Empty;

    /// <summary>Message being replied to (right-click / 回复).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReplyTarget))]
    [NotifyPropertyChangedFor(nameof(ReplyTargetVisibility))]
    [NotifyPropertyChangedFor(nameof(ReplyBannerText))]
    public partial MessageItemViewModel? ReplyTarget { get; set; }

    public bool HasReplyTarget => ReplyTarget is not null;

    public Microsoft.UI.Xaml.Visibility ReplyTargetVisibility =>
        HasReplyTarget ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public string ReplyBannerText =>
        ReplyTarget is null
            ? string.Empty
            : $"回复 {ReplyTarget.SenderName}: {TruncatePreview(ReplyTarget.Content)}";

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

    [ObservableProperty]
    public partial string OnlineStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MembersSummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PinnedSummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NotifyModeText { get; set; } = "通知：全部";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    [NotifyPropertyChangedFor(nameof(RecordingVisibility))]
    [NotifyPropertyChangedFor(nameof(IsComposerEnabled))]
    public partial bool IsRecording { get; set; }

    [ObservableProperty]
    public partial string RecordingElapsedText { get; set; } = "0:00";

    [ObservableProperty]
    public partial bool IsSendingVoice { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SuggestionsVisibility))]
    public partial bool HasSuggestions { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BotPanelVisibility))]
    public partial bool IsBotPanelOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StickerPanelVisibility))]
    public partial bool IsStickerPanelOpen { get; set; }

    public Microsoft.UI.Xaml.Visibility StickerPanelVisibility =>
        IsStickerPanelOpen ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public ObservableCollection<StickerPackTabItem> StickerPacks { get; } = [];

    public ObservableCollection<StickerPickItem> StickerGrid { get; } = [];

    [ObservableProperty]
    public partial int SelectedStickerPackIndex { get; set; } = -1;

    [ObservableProperty]
    public partial string StickerPanelStatus { get; set; } = string.Empty;

    private List<(StickerPack Pack, List<SnSticker> Stickers)> _stickerPackCache = [];

    [ObservableProperty]
    public partial string BotPanelStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SuggestionHintText { get; set; } = "输入 / 命令 · @ 提及 · 任意文本服务端联想";

    [ObservableProperty]
    public partial int SelectedSuggestionIndex { get; set; } = -1;

    public string RecordButtonText => IsRecording ? "停止并发送" : "录音";

    public Microsoft.UI.Xaml.Visibility SuggestionsVisibility =>
        HasSuggestions ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility BotPanelVisibility =>
        IsBotPanelOpen ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility RecordingVisibility =>
        IsRecording ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MembersPanelVisibility))]
    public partial bool IsMembersPanelOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CallPanelVisibility))]
    public partial bool IsCallPanelOpen { get; set; }

    [ObservableProperty]
    public partial string CallStatusText { get; set; } = "未加入通话";

    [ObservableProperty]
    public partial string CallEndpointText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MyRoomNick { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TimeoutMinutesText { get; set; } = "30";

    [ObservableProperty]
    public partial string InviteAccountIdText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string E2eeStatusText { get; set; } = "E2EE: 未知";

    [ObservableProperty]
    public partial string RedirectMessageIdsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RealtimeModeText { get; set; } = "实时: 轮询";

    public Microsoft.UI.Xaml.Visibility MembersPanelVisibility =>
        IsMembersPanelOpen ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility CallPanelVisibility =>
        IsCallPanelOpen ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public event EventHandler? ScrollToBottomRequested;
    public event EventHandler? OlderMessagesLoaded;
    public event EventHandler<MessageAttachmentViewModel>? OpenImageRequested;

    public void Initialize(Guid roomId, string? title)
    {
        if (IsRecording)
        {
            _ = _voiceRecorder.CancelAsync();
            IsRecording = false;
        }

        RoomId = roomId;
        _messageNotifier.ActiveRoomId = roomId != Guid.Empty ? roomId : null;
        _messageNotifier.Start();
        RoomTitle = string.IsNullOrWhiteSpace(title) ? "聊天" : title!;
        Messages.Clear();
        _knownMessageIds.Clear();
        Suggestions.Clear();
        HasSuggestions = false;
        SelectedSuggestionIndex = -1;
        BotCommands.Clear();
        IsBotPanelOpen = false;
        BotPanelStatusText = string.Empty;
        Members.Clear();
        CallParticipants.Clear();
        IsMembersPanelOpen = false;
        IsCallPanelOpen = false;
        _activeCall = null;
        CallStatusText = "未加入通话";
        CallEndpointText = string.Empty;
        _botCommands.Clear();
        _offset = 0;
        _hasMore = true;
        _lastSyncTimestamp = 0;
        _lastSyncMessageId = null;
        Draft = string.Empty;
        ReplyTarget = null;
        ClearPendingImage();
        ErrorMessage = null;
        _markedRead = false;
        RecordingElapsedText = "0:00";
        UpdateCanSend();

        // Clear unread badge immediately when opening room
        _chatList.MarkRoomReadLocal(roomId);
    }

    partial void OnDraftChanged(string value)
    {
        UpdateCanSend();
        _ = RefreshSuggestionsAsync(value);
    }

    partial void OnIsSendingChanged(bool value) => UpdateCanSend();

    partial void OnPendingImageFileIdChanged(string? value) => UpdateCanSend();

    partial void OnIsSendingVoiceChanged(bool value) => UpdateCanSend();

    private void UpdateCanSend()
    {
        CanSend = !IsSending
            && !IsUploadingImage
            && !IsRecording
            && !IsSendingVoice
            && (!string.IsNullOrWhiteSpace(Draft) || !string.IsNullOrWhiteSpace(PendingImageFileId));
        IsComposerEnabled = !IsSending && !IsUploadingImage && !IsRecording && !IsSendingVoice;
        PendingImagePanelOpacity = string.IsNullOrWhiteSpace(PendingImageFileId) && string.IsNullOrWhiteSpace(PendingImageName)
            ? 0.0
            : 1.0;
    }

    private void OnVoiceElapsedChanged(object? sender, TimeSpan elapsed)
    {
        // Marshal to UI if needed
        var text = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
        if (App.DispatcherQueue is { } dq && !dq.HasThreadAccess)
        {
            dq.TryEnqueue(() => RecordingElapsedText = text);
            return;
        }

        RecordingElapsedText = text;
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

            // Instant paint from singleton message cache when available
            var paintedFromCache = TryPaintMessagesFromCache();

            try
            {
                var batch = await _api.GetMessagesAsync(RoomId.ToString(), offset: 0, take: PageSize).ConfigureAwait(true);
                var ordered = NormalizeOrder(batch);

                Messages.Clear();
                _knownMessageIds.Clear();
                foreach (var msg in ordered)
                {
                    AddMessageInternal(msg, append: true);
                }

                _offset = batch.Count;
                _hasMore = batch.Count >= PageSize;
                UpdateSyncCursorFromMessages();
                PersistMessagesToCache();
            }
            catch (SolarApiException ex)
            {
                if (!paintedFromCache)
                {
                    ErrorMessage = ex.ApiMessage ?? ex.Message;
                }
                // else keep cached messages on screen
            }

            ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
            _ = LoadMediaAsync();
            _ = MarkRoomReadIfNeededAsync();
            _ = LoadRoomMetaAsync();
            _ = LoadBotCommandsAsync();
            _ = RefreshMembersAsync();
            HookWebSocket();
            try
            {
                await _api.MarkDeviceJoinedRoomAsync(RoomId).ConfigureAwait(true);
            }
            catch
            {
                // optional presence
            }

            try
            {
                await _ws.ConnectAsync().ConfigureAwait(true);
                RealtimeModeText = _ws.State == ChatWsConnectionState.Connected ? "实时: WebSocket" : "实时: 轮询";
            }
            catch
            {
                RealtimeModeText = "实时: 轮询";
            }
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

    private async Task LoadRoomMetaAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            var online = await _api.GetOnlineMembersAsync(RoomId).ConfigureAwait(true);
            OnlineStatusText = online.OnlineCount > 0
                ? $"在线 {online.OnlineCount}"
                : "在线 —";
        }
        catch
        {
            OnlineStatusText = string.Empty;
        }

        try
        {
            var members = await _api.GetChatMembersAsync(RoomId).ConfigureAwait(true);
            MembersSummaryText = $"成员 {members.Count}";
        }
        catch
        {
            MembersSummaryText = string.Empty;
        }

        try
        {
            var pins = await _api.GetPinnedMessagesAsync(RoomId).ConfigureAwait(true);
            PinnedSummaryText = pins.Count > 0 ? $"置顶 {pins.Count}" : string.Empty;
        }
        catch
        {
            PinnedSummaryText = string.Empty;
        }

        try
        {
            var me = await _api.GetMyChatMembershipAsync(RoomId).ConfigureAwait(true);
            NotifyModeText = me.Notify switch
            {
                ChatMemberNotify.Mentions => "通知：仅提及",
                ChatMemberNotify.None => "通知：关闭",
                _ => "通知：全部",
            };
        }
        catch
        {
            // ignore
        }
    }

    [RelayCommand]
    private async Task DeleteMessageAsync(MessageItemViewModel? item)
    {
        if (item is null || item.Message.Id == Guid.Empty)
        {
            return;
        }

        try
        {
            await _api.DeleteMessageAsync(RoomId, item.Message.Id).ConfigureAwait(true);
            Messages.Remove(item);
            _knownMessageIds.Remove(item.Message.Id);
            _toast.Success("已删除");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"删除失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task EditMessageAsync(MessageItemViewModel? item)
    {
        if (item is null || item.Message.Id == Guid.Empty)
        {
            return;
        }

        // Simple edit: use current draft as new content if set, else no-op.
        var text = Draft?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            _toast.Show("请在输入框写入新内容后点「改选中」");
            return;
        }

        try
        {
            await _api.EditMessageAsync(RoomId, item.Message.Id, new SendMessageRequest
            {
                Content = text,
            }).ConfigureAwait(true);

            item.ApplyEditedContent(text);
            Draft = string.Empty;
            _toast.Success("已修改");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"修改失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ReactToMessageAsync(MessageItemViewModel? item)
    {
        if (item is null || item.Message.Id == Guid.Empty)
        {
            return;
        }

        try
        {
            await _api.ReactToMessageAsync(
                RoomId,
                item.Message.Id,
                new MessageReactionRequest
                {
                    Symbol = "thumb_up",
                    Attitude = (int)MessageReactionAttitude.Positive,
                }).ConfigureAwait(true);

            item.Message.ReactionsMade ??= new Dictionary<string, bool>(StringComparer.Ordinal);
            item.Message.ReactionsMade["thumb_up"] = true;
            item.Message.ReactionsCount ??= new Dictionary<string, int>(StringComparer.Ordinal);
            item.Message.ReactionsCount["thumb_up"] =
                item.Message.ReactionsCount.GetValueOrDefault("thumb_up") + 1;
            item.RefreshReactionText();
            _toast.Success("已表态");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"表态失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PinMessageAsync(MessageItemViewModel? item)
    {
        if (item is null || item.Message.Id == Guid.Empty)
        {
            return;
        }

        try
        {
            await _api.PinMessageAsync(RoomId, item.Message.Id).ConfigureAwait(true);
            await LoadRoomMetaAsync().ConfigureAwait(true);
            _toast.Success("已置顶");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"置顶失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CycleNotifyAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            var next = NotifyModeText switch
            {
                "通知：全部" => (int)ChatMemberNotify.Mentions,
                "通知：仅提及" => (int)ChatMemberNotify.None,
                _ => (int)ChatMemberNotify.All,
            };

            await _api.UpdateMyChatNotifyAsync(RoomId, new ChatMemberNotifyRequest
            {
                NotifyLevel = next,
            }).ConfigureAwait(true);

            NotifyModeText = next switch
            {
                (int)ChatMemberNotify.Mentions => "通知：仅提及",
                (int)ChatMemberNotify.None => "通知：关闭",
                _ => "通知：全部",
            };
            _toast.Success(NotifyModeText);
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"通知设置失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LeaveRoomAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _api.LeaveChatRoomAsync(RoomId).ConfigureAwait(true);
            _toast.Success("已退出会话");
            StopPolling();
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"退出失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private void ToggleMembersPanel()
    {
        IsMembersPanelOpen = !IsMembersPanelOpen;
        if (IsMembersPanelOpen)
        {
            _ = RefreshMembersAsync();
        }
    }

    [RelayCommand]
    private void ToggleCallPanel()
    {
        IsCallPanelOpen = !IsCallPanelOpen;
        if (IsCallPanelOpen)
        {
            _ = RefreshCallAsync();
        }
    }

    [RelayCommand]
    private async Task RefreshMembersAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        // Paint from cache while refreshing
        if (Members.Count == 0
            && _cache.TryGetRoomMembers(RoomId, out var cachedMembers, out _)
            && cachedMembers.Count > 0)
        {
            foreach (var m in cachedMembers)
            {
                Members.Add(new ChatMemberItemViewModel(m));
            }

            MembersSummaryText = $"成员 {Members.Count}";
        }

        if (_cache.IsRoomMembersFresh(RoomId) && Members.Count > 0)
        {
            return;
        }

        try
        {
            var list = await _api.GetChatMembersAsync(RoomId).ConfigureAwait(true);
            _cache.SetRoomMembers(RoomId, list);
            Members.Clear();
            foreach (var m in list)
            {
                Members.Add(new ChatMemberItemViewModel(m));
            }

            MembersSummaryText = $"成员 {Members.Count}";

            try
            {
                var me = await _api.GetMyChatMembershipAsync(RoomId).ConfigureAwait(true);
                MyRoomNick = me.Nick ?? string.Empty;
            }
            catch
            {
                // ignore
            }
        }
        catch (SolarApiException ex)
        {
            if (Members.Count == 0)
            {
                _toast.Error($"成员列表失败：{ex.ApiMessage ?? ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task SaveMyRoomNickAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _api.UpdateMyChatProfileAsync(RoomId, new ChatMemberProfileRequest
            {
                Nick = string.IsNullOrWhiteSpace(MyRoomNick) ? null : MyRoomNick.Trim(),
            }).ConfigureAwait(true);
            _toast.Success("本群昵称已保存");
            await RefreshMembersAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"保存昵称失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task KickMemberAsync(ChatMemberItemViewModel? item)
    {
        if (item is null || RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _api.RemoveChatMemberAsync(RoomId, item.MemberId).ConfigureAwait(true);
            Members.Remove(item);
            MembersSummaryText = $"成员 {Members.Count}";
            _toast.Success($"已移出 {item.DisplayName}");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"踢人失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TimeoutMemberAsync(ChatMemberItemViewModel? item)
    {
        if (item is null || RoomId == Guid.Empty)
        {
            return;
        }

        if (!int.TryParse(TimeoutMinutesText.Trim(), out var minutes) || minutes <= 0)
        {
            minutes = 30;
        }

        try
        {
            await _api.TimeoutChatMemberAsync(RoomId, item.MemberId, new ChatTimeoutRequest
            {
                Reason = "timeout from SolarWin",
                TimeoutUntil = DateTimeOffset.UtcNow.AddMinutes(minutes),
            }).ConfigureAwait(true);
            _toast.Success($"已禁言 {item.DisplayName} {minutes} 分钟");
            await RefreshMembersAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"禁言失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ClearTimeoutMemberAsync(ChatMemberItemViewModel? item)
    {
        if (item is null || RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _api.ClearChatMemberTimeoutAsync(RoomId, item.MemberId).ConfigureAwait(true);
            _toast.Success($"已解除禁言 {item.DisplayName}");
            await RefreshMembersAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"解除禁言失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task JoinVoiceAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            IsCallPanelOpen = true;
            var call = await _api.JoinRealtimeCallAsync(RoomId).ConfigureAwait(true);
            _activeCall = call;
            CallStatusText = $"已加入 · {call.Provider ?? "provider?"} · call {call.CallId}";
            CallEndpointText = string.IsNullOrWhiteSpace(call.Endpoint)
                ? (call.Token is { Length: > 0 } ? $"token: {call.Token[..Math.Min(24, call.Token.Length)]}…" : "无 endpoint")
                : call.Endpoint!;
            CallParticipants.Clear();
            foreach (var p in call.Participants ?? [])
            {
                CallParticipants.Add(new CallParticipantItemViewModel(p));
            }

            _toast.Success($"通话会话已建立（参与者 {CallParticipants.Count}）。媒体层需 WebRTC/LiveKit 客户端。");
            await RefreshCallAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"加入通话失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshCallAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            var parts = await _api.GetRealtimeParticipantsAsync(RoomId).ConfigureAwait(true);
            CallParticipants.Clear();
            foreach (var p in parts)
            {
                CallParticipants.Add(new CallParticipantItemViewModel(p));
            }

            if (CallParticipants.Count > 0 && CallStatusText.StartsWith("未加入", StringComparison.Ordinal))
            {
                CallStatusText = $"进行中 · 参与者 {CallParticipants.Count}";
            }
        }
        catch
        {
            // optional
        }
    }

    [RelayCommand]
    private async Task InviteToCallAsync()
    {
        if (RoomId == Guid.Empty || !Guid.TryParse(InviteAccountIdText.Trim(), out var accountId))
        {
            _toast.Show("请填写要邀请的账号 UUID");
            return;
        }

        try
        {
            await _api.InviteToRealtimeCallAsync(RoomId, accountId).ConfigureAwait(true);
            _toast.Success("已发送通话邀请");
            InviteAccountIdText = string.Empty;
            await RefreshCallAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"邀请失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task KickFromCallAsync(CallParticipantItemViewModel? item)
    {
        if (item is null || item.AccountId == Guid.Empty || RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _api.KickFromRealtimeCallAsync(RoomId, item.AccountId).ConfigureAwait(true);
            CallParticipants.Remove(item);
            _toast.Success($"已踢出 {item.DisplayName}");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"踢出失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task MuteInCallAsync(CallParticipantItemViewModel? item)
    {
        if (item is null || item.AccountId == Guid.Empty || RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _api.MuteRealtimeParticipantAsync(RoomId, item.AccountId).ConfigureAwait(true);
            _toast.Success($"已静音 {item.DisplayName}");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"静音失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task UnmuteInCallAsync(CallParticipantItemViewModel? item)
    {
        if (item is null || item.AccountId == Guid.Empty || RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _api.UnmuteRealtimeParticipantAsync(RoomId, item.AccountId).ConfigureAwait(true);
            _toast.Success($"已取消静音 {item.DisplayName}");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"取消静音失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task EnableE2eeAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _api.EnableRoomE2eeAsync(RoomId).ConfigureAwait(true);
            E2eeStatusText = "E2EE: 已请求启用（本地密钥交换未实现，加密内容仍可能无法解密）";
            _toast.Success("已调用 E2EE enable");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"E2EE 失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task EnableMlsAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _api.EnableRoomMlsAsync(RoomId).ConfigureAwait(true);
            E2eeStatusText = "MLS: 已请求启用（本地 MLS 状态机未实现）";
            _toast.Success("已调用 MLS enable");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"MLS 失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SendTypingPlaceholderAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            var msg = await _api.SendPlaceholderMessageAsync(RoomId, "typing").ConfigureAwait(true);
            AddMessageInternal(msg, append: true);
            ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
            _toast.Success("已发送 placeholder(typing)");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"placeholder 失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RedirectMessagesAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        var ids = new List<Guid>();
        foreach (var part in RedirectMessageIdsText.Split([',', ' ', ';', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(part.Trim(), out var id))
            {
                ids.Add(id);
            }
        }

        if (ids.Count == 0)
        {
            // fallback: last mine message
            var last = Messages.LastOrDefault(m => m.IsMine && m.Message.Id != Guid.Empty);
            if (last is not null)
            {
                ids.Add(last.Message.Id);
            }
        }

        if (ids.Count == 0)
        {
            _toast.Show("请填写 message id，或先发送一条自己的消息");
            return;
        }

        try
        {
            var msg = await _api.RedirectMessagesAsync(RoomId, ids).ConfigureAwait(true);
            AddMessageInternal(msg, append: true);
            ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
            RedirectMessageIdsText = string.Empty;
            _toast.Success($"已 redirect {ids.Count} 条");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"redirect 失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    private void HookWebSocket()
    {
        if (_wsHooked)
        {
            return;
        }

        _wsHooked = true;
        _ws.PacketReceived += OnRoomWsPacket;
        _ws.StateChanged += (_, s) =>
        {
            void Apply() => RealtimeModeText = s == ChatWsConnectionState.Connected ? "实时: WebSocket" : "实时: 轮询";
            if (App.DispatcherQueue is { } dq && !dq.HasThreadAccess)
            {
                dq.TryEnqueue(Apply);
            }
            else
            {
                Apply();
            }
        };
    }

    private void OnRoomWsPacket(object? sender, ChatWsPacket packet)
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        // Solian: messages.new / messages.update carry SnChatMessage in data
        if (!packet.Type.StartsWith("messages.", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(packet.Type, "system.e2ee.enabled", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        void Handle()
        {
            if (string.Equals(packet.Type, "system.e2ee.enabled", StringComparison.OrdinalIgnoreCase))
            {
                E2eeStatusText = "E2EE: 服务端已启用";
                return;
            }

            if (packet.Data is not { } data || data.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                // still trigger a soft sync
                _ = SyncOnceAsync();
                return;
            }

            try
            {
                var msg = System.Text.Json.JsonSerializer.Deserialize<SnChatMessage>(data.GetRawText(), JsonDefaults.Options);
                if (msg is null)
                {
                    return;
                }

                if (msg.ChatRoomId != Guid.Empty && msg.ChatRoomId != RoomId)
                {
                    return;
                }

                // Infer room if missing
                if (msg.ChatRoomId == Guid.Empty)
                {
                    msg.ChatRoomId = RoomId;
                }

                var type = msg.Type ?? packet.Type;
                if (type.Contains("delete", StringComparison.OrdinalIgnoreCase))
                {
                    var targetId = msg.Id;
                    if (msg.Meta is not null && msg.Meta.TryGetValue("message_id", out var midEl))
                    {
                        if (midEl.ValueKind == System.Text.Json.JsonValueKind.String &&
                            Guid.TryParse(midEl.GetString(), out var mid))
                        {
                            targetId = mid;
                        }
                    }

                    var existing = Messages.FirstOrDefault(m => m.Message.Id == targetId);
                    if (existing is not null)
                    {
                        existing.ApplyEditedContent("（已删除）");
                    }

                    return;
                }

                if (AddMessageInternal(msg, append: true))
                {
                    ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
                    _ = LoadMediaAsync();
                }
                else
                {
                    // update existing bubble if same id
                    var existing = Messages.FirstOrDefault(m => m.Message.Id == msg.Id && msg.Id != Guid.Empty);
                    if (existing is not null && !string.IsNullOrWhiteSpace(msg.Content))
                    {
                        existing.ApplyEditedContent(msg.Content!);
                        existing.RefreshReactionText();
                    }
                }
            }
            catch
            {
                _ = SyncOnceAsync();
            }
        }

        if (App.DispatcherQueue is { } dq && !dq.HasThreadAccess)
        {
            dq.TryEnqueue(Handle);
            return;
        }

        Handle();
    }

    [RelayCommand]
    private async Task ToggleVoiceRecordAsync()
    {
        if (RoomId == Guid.Empty || IsSendingVoice)
        {
            return;
        }

        if (!IsRecording)
        {
            try
            {
                await _voiceRecorder.StartAsync().ConfigureAwait(true);
                IsRecording = true;
                RecordingElapsedText = "0:00";
                UpdateCanSend();
                _toast.Show("正在录音… 再点一次停止并发送");
            }
            catch (Exception ex)
            {
                _toast.Error($"无法开始录音：{ex.Message}（请检查麦克风权限）");
            }

            return;
        }

        // Stop + send
        try
        {
            IsSendingVoice = true;
            IsRecording = false;
            UpdateCanSend();

            var clip = await _voiceRecorder.StopAsync().ConfigureAwait(true);
            if (clip is null)
            {
                _toast.Show("录音太短或未采集到声音");
                return;
            }

            await using var stream = new MemoryStream(clip.WavBytes, writable: false);
            var sent = await _api.SendVoiceMessageAsync(
                    RoomId,
                    stream,
                    clip.FileName,
                    clip.ContentType,
                    clip.DurationMs)
                .ConfigureAwait(true);

            AddMessageInternal(sent, append: true);
            ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
            _ = LoadMediaAsync();
            _toast.Success($"语音已发送（{clip.DurationMs / 1000.0:0.0}s）");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"发送语音失败：{ex.ApiMessage ?? ex.Message}");
            await _voiceRecorder.CancelAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _toast.Error($"发送语音失败：{ex.Message}");
            await _voiceRecorder.CancelAsync().ConfigureAwait(true);
        }
        finally
        {
            IsSendingVoice = false;
            IsRecording = false;
            RecordingElapsedText = "0:00";
            UpdateCanSend();
        }
    }

    [RelayCommand]
    private async Task CancelVoiceRecordAsync()
    {
        if (!IsRecording)
        {
            return;
        }

        await _voiceRecorder.CancelAsync().ConfigureAwait(true);
        IsRecording = false;
        RecordingElapsedText = "0:00";
        UpdateCanSend();
        _toast.Show("已取消录音");
    }

    [RelayCommand]
    private void ToggleBotPanel()
    {
        IsBotPanelOpen = !IsBotPanelOpen;
        if (IsBotPanelOpen)
        {
            IsStickerPanelOpen = false;
        }

        if (IsBotPanelOpen && BotCommands.Count == 0)
        {
            _ = LoadBotCommandsAsync();
        }
    }

    [RelayCommand]
    private void ToggleStickerPanel()
    {
        IsStickerPanelOpen = !IsStickerPanelOpen;
        if (IsStickerPanelOpen)
        {
            IsBotPanelOpen = false;
            if (StickerPacks.Count == 0)
            {
                _ = LoadStickerPacksAsync();
            }
        }
    }

    private async Task LoadStickerPacksAsync()
    {
        try
        {
            StickerPanelStatus = "加载贴纸包…";
            var ownerships = await _api.GetMyStickerPacksAsync().ConfigureAwait(true);
            _stickerPackCache.Clear();
            StickerPacks.Clear();
            StickerGrid.Clear();

            foreach (var o in ownerships)
            {
                var pack = o.Pack;
                if (pack is null || pack.Id == Guid.Empty)
                {
                    if (o.PackId == Guid.Empty)
                    {
                        continue;
                    }

                    pack = new StickerPack { Id = o.PackId, Name = "贴纸包" };
                }

                List<SnSticker> stickers;
                if (pack.Stickers is { Count: > 0 })
                {
                    stickers = pack.Stickers;
                }
                else
                {
                    try
                    {
                        stickers = await _api.GetStickerPackContentAsync(pack.Id).ConfigureAwait(true);
                    }
                    catch
                    {
                        stickers = [];
                    }
                }

                if (string.IsNullOrWhiteSpace(pack.Prefix) && stickers.Count == 0)
                {
                    continue;
                }

                _stickerPackCache.Add((pack, stickers));
                StickerPacks.Add(new StickerPackTabItem
                {
                    PackId = pack.Id,
                    Title = pack.Name ?? pack.Prefix ?? "贴纸包",
                    Prefix = pack.Prefix ?? string.Empty,
                });
            }

            if (StickerPacks.Count == 0)
            {
                StickerPanelStatus = "暂无贴纸包，请到探索页添加";
                SelectedStickerPackIndex = -1;
                return;
            }

            SelectedStickerPackIndex = 0;
            await ShowStickerPackAsync(0).ConfigureAwait(true);
            StickerPanelStatus = $"贴纸包 {StickerPacks.Count} 个";
        }
        catch (SolarApiException ex)
        {
            StickerPanelStatus = ex.ApiMessage ?? ex.Message;
            _toast.Error("贴纸加载失败");
        }
    }

    partial void OnSelectedStickerPackIndexChanged(int value)
    {
        if (value >= 0 && value < _stickerPackCache.Count)
        {
            _ = ShowStickerPackAsync(value);
        }
    }

    private async Task ShowStickerPackAsync(int index)
    {
        if (index < 0 || index >= _stickerPackCache.Count)
        {
            return;
        }

        var (pack, stickers) = _stickerPackCache[index];
        StickerGrid.Clear();
        foreach (var s in stickers.OrderBy(x => x.Order).ThenBy(x => x.Name))
        {
            var fileId = CloudFileUrlHelper.ResolveFileId(s.Image)
                ?? CloudFileUrlHelper.Resolve(s.Image);
            var item = new StickerPickItem
            {
                PackPrefix = pack.Prefix ?? string.Empty,
                Slug = s.Slug ?? s.Id.ToString("N")[..8],
                Title = s.Name ?? s.Slug ?? "贴纸",
                Mode = s.Mode,
                ImageFileId = fileId,
            };
            StickerGrid.Add(item);
            if (!string.IsNullOrWhiteSpace(fileId))
            {
                _ = LoadStickerThumbAsync(item, fileId);
            }
        }

        await Task.CompletedTask.ConfigureAwait(true);
    }

    private async Task LoadStickerThumbAsync(StickerPickItem item, string fileId)
    {
        try
        {
            item.Image = await _imageLoader.LoadSafeAsync(fileId).ConfigureAwait(true);
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// Solian: stickers send as text placeholder <c>:{prefix}+{slug}:</c>.
    /// mode 0 = send immediately as sticker-only; mode 1 = insert into draft as emote.
    /// </summary>
    [RelayCommand]
    private async Task SendStickerAsync(StickerPickItem? item)
    {
        if (item is null || RoomId == Guid.Empty)
        {
            return;
        }

        var prefix = item.PackPrefix?.Trim() ?? string.Empty;
        var slug = item.Slug?.Trim() ?? string.Empty;
        if (prefix.Length == 0 || slug.Length == 0)
        {
            _toast.Show("贴纸数据不完整");
            return;
        }

        var placeholder = $":{prefix}+{slug}:";

        // Emote mode: insert into draft
        if (item.Mode == 1)
        {
            Draft = string.IsNullOrEmpty(Draft) ? placeholder : Draft + placeholder;
            return;
        }

        // Sticker mode: send as message content immediately
        try
        {
            IsSending = true;
            ErrorMessage = null;
            var clientMessageId = Guid.NewGuid().ToString("N");
            var request = new SendMessageRequest
            {
                Content = placeholder,
                ClientMessageId = clientMessageId,
                Nonce = Guid.NewGuid().ToString("N")[..16],
                RepliedMessageId = ReplyTarget?.Message.Id is { } rid && rid != Guid.Empty ? rid : null,
            };

            await _api.SendMessageAsync(RoomId.ToString(), request).ConfigureAwait(true);
            var replied = ReplyTarget;
            ReplyTarget = null;

            // Cache file id so history reloads can paint without another lookup.
            if (!string.IsNullOrWhiteSpace(item.ImageFileId))
            {
                RememberStickerFileId(placeholder, item.ImageFileId!);
            }

            AddLocalEcho(placeholder, null, clientMessageId, replied);

            // Paint the sticker image immediately (don't wait for server lookup).
            if (Messages.Count > 0 && !string.IsNullOrWhiteSpace(item.ImageFileId))
            {
                var echoVm = Messages[^1];
                if (string.Equals(echoVm.Message.ClientMessageId, clientMessageId, StringComparison.Ordinal))
                {
                    echoVm.SeedStickerFromFileId(placeholder, item.ImageFileId!);
                    if (item.Image is not null)
                    {
                        var slot = echoVm.Stickers.FirstOrDefault(s =>
                            string.Equals(s.Placeholder, placeholder, StringComparison.OrdinalIgnoreCase));
                        if (slot is not null)
                        {
                            slot.Image = item.Image;
                            slot.ImageOpacity = 1.0;
                        }
                    }
                }
            }

            ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
            _ = LoadMediaAsync();
            _ = ReconcileAfterSendAsync();
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("贴纸发送失败：" + (ex.ApiMessage ?? ex.Message));
        }
        finally
        {
            IsSending = false;
        }
    }

    private static void RememberStickerFileId(string placeholder, string fileId)
    {
        if (string.IsNullOrWhiteSpace(placeholder) || string.IsNullOrWhiteSpace(fileId))
        {
            return;
        }

        StickerFileIdCache[placeholder] = fileId;
        var key = placeholder.Trim();
        if (key.StartsWith(':') && key.EndsWith(':') && key.Length > 2)
        {
            StickerFileIdCache[key[1..^1]] = fileId;
        }
    }

    private static bool TryGetCachedStickerFileId(string placeholder, out string? fileId)
    {
        if (StickerFileIdCache.TryGetValue(placeholder, out fileId))
        {
            return true;
        }

        var key = placeholder.Trim();
        if (key.StartsWith(':') && key.EndsWith(':') && key.Length > 2
            && StickerFileIdCache.TryGetValue(key[1..^1], out fileId))
        {
            return true;
        }

        fileId = null;
        return false;
    }

    [RelayCommand]
    private void ApplyBotCommand(ChatBotCommandItem? item)
    {
        if (item is null)
        {
            return;
        }

        Draft = item.InsertText;
        IsBotPanelOpen = false;
        Suggestions.Clear();
        HasSuggestions = false;
        SelectedSuggestionIndex = -1;
    }

    [RelayCommand]
    private void ApplySuggestion(ChatSuggestionItem? item)
    {
        if (item is null)
        {
            return;
        }

        ApplyInsertText(item.InsertText, item.Kind);
        Suggestions.Clear();
        HasSuggestions = false;
        SelectedSuggestionIndex = -1;
    }

    /// <summary>Keyboard: move highlight in suggestion list. Returns true if handled.</summary>
    public bool MoveSuggestionSelection(int delta)
    {
        if (!HasSuggestions || Suggestions.Count == 0)
        {
            return false;
        }

        var next = SelectedSuggestionIndex + delta;
        if (SelectedSuggestionIndex < 0)
        {
            next = delta > 0 ? 0 : Suggestions.Count - 1;
        }

        next = Math.Clamp(next, 0, Suggestions.Count - 1);
        SelectedSuggestionIndex = next;
        for (var i = 0; i < Suggestions.Count; i++)
        {
            Suggestions[i].IsSelected = i == next;
        }

        return true;
    }

    /// <summary>Keyboard: Tab/Enter apply highlighted suggestion. Returns true if applied.</summary>
    public bool TryApplySelectedSuggestion()
    {
        if (!HasSuggestions || Suggestions.Count == 0)
        {
            return false;
        }

        var idx = SelectedSuggestionIndex >= 0 ? SelectedSuggestionIndex : 0;
        if (idx >= Suggestions.Count)
        {
            return false;
        }

        ApplySuggestion(Suggestions[idx]);
        return true;
    }

    public bool DismissSuggestions()
    {
        if (!HasSuggestions && !IsBotPanelOpen)
        {
            return false;
        }

        Suggestions.Clear();
        HasSuggestions = false;
        SelectedSuggestionIndex = -1;
        IsBotPanelOpen = false;
        return true;
    }

    private void ApplyInsertText(string insert, string kind)
    {
        var draft = Draft ?? string.Empty;
        var trimmedInsert = insert ?? string.Empty;
        if (trimmedInsert.Length == 0)
        {
            return;
        }

        // Slash command / bot: replace whole draft start token
        if (kind == "bot" || draft.TrimStart().StartsWith('/'))
        {
            // Keep leading spaces if any
            var lead = draft.Length - draft.TrimStart().Length;
            var prefix = lead > 0 ? draft[..lead] : string.Empty;
            Draft = prefix + (trimmedInsert.EndsWith(' ') ? trimmedInsert : trimmedInsert + " ");
            return;
        }

        // @mention: replace the trailing @token
        var at = draft.LastIndexOf('@');
        if (at >= 0 && (at == 0 || char.IsWhiteSpace(draft[at - 1])))
        {
            var after = draft[(at + 1)..];
            if (!after.Contains(' ', StringComparison.Ordinal) && !after.Contains('\n', StringComparison.Ordinal))
            {
                var mention = trimmedInsert.StartsWith('@') ? trimmedInsert : "@" + trimmedInsert.TrimStart('@');
                Draft = draft[..at] + mention + (mention.EndsWith(' ') ? string.Empty : " ");
                return;
            }
        }

        // Default: replace last whitespace-separated token
        var lastSpace = draft.LastIndexOfAny([' ', '\n', '\t']);
        if (lastSpace < 0)
        {
            Draft = trimmedInsert;
        }
        else
        {
            Draft = draft[..(lastSpace + 1)] + trimmedInsert;
        }

        if (!Draft.EndsWith(' ') && kind is "user" or "member" or "mention" or "account")
        {
            Draft += " ";
        }
    }

    private async Task LoadBotCommandsAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        try
        {
            BotPanelStatusText = "加载机器人命令…";
            var cmds = await _api.GetBotCommandsAsync(RoomId).ConfigureAwait(true);
            _botCommands.Clear();
            _botCommands.AddRange(cmds);
            BotCommands.Clear();
            foreach (var c in cmds.OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                BotCommands.Add(new ChatBotCommandItem(c));
            }

            BotPanelStatusText = cmds.Count == 0
                ? "此房间暂无机器人命令"
                : $"共 {cmds.Count} 条命令 · 点选插入，或输入 / 筛选";
        }
        catch (Exception ex)
        {
            BotPanelStatusText = "加载命令失败：" + (ex is SolarApiException se
                ? (se.ApiMessage ?? se.Message)
                : ex.Message);
        }
    }

    private async Task RefreshSuggestionsAsync(string? draft)
    {
        var raw = draft ?? string.Empty;
        var text = raw.Trim();
        if (RoomId == Guid.Empty || text.Length == 0)
        {
            Suggestions.Clear();
            HasSuggestions = false;
            SelectedSuggestionIndex = -1;
            return;
        }

        _suggestCts?.Cancel();
        _suggestCts = new CancellationTokenSource();
        var token = _suggestCts.Token;

        try
        {
            await Task.Delay(200, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            var list = new List<ChatSuggestionItem>();

            // 1) Slash → local bot command filter
            if (text.StartsWith('/'))
            {
                var q = text[1..];
                // strip after first space for matching command name
                var qName = q.Split(' ', 2)[0];
                foreach (var cmd in _botCommands)
                {
                    var name = (cmd.Name ?? string.Empty).TrimStart('/');
                    if (name.Length == 0)
                    {
                        continue;
                    }

                    if (qName.Length == 0 ||
                        name.StartsWith(qName, StringComparison.OrdinalIgnoreCase) ||
                        name.Contains(qName, StringComparison.OrdinalIgnoreCase) ||
                        (cmd.Usage?.Contains(qName, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (cmd.Description?.Contains(qName, StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        list.Add(new ChatSuggestionItem
                        {
                            Kind = "bot",
                            Title = cmd.DisplayName,
                            Subtitle = cmd.DisplayDetail,
                            InsertText = cmd.DisplayName + " ",
                            Group = cmd.BotKey,
                        });
                    }
                }
            }

            // 2) @ → local members + server
            var mentionQ = TryGetMentionQuery(raw);
            if (mentionQ is not null)
            {
                foreach (var m in Members)
                {
                    var handle = m.Member.Account?.Name
                        ?? m.Member.Username
                        ?? m.DisplayName;
                    if (string.IsNullOrWhiteSpace(handle))
                    {
                        continue;
                    }

                    if (mentionQ.Length == 0 ||
                        handle.Contains(mentionQ, StringComparison.OrdinalIgnoreCase) ||
                        m.DisplayName.Contains(mentionQ, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(new ChatSuggestionItem
                        {
                            Kind = "member",
                            Title = "@" + handle.TrimStart('@'),
                            Subtitle = m.DisplayName == handle ? "房间成员" : m.DisplayName + " · 房间成员",
                            InsertText = "@" + handle.TrimStart('@'),
                            Group = "本地成员",
                        });
                    }
                }
            }

            // 3) Server autocomplete (mentions, stickers, …)
            try
            {
                // API requires minLength 1 content
                var remote = await _api.AutocompleteChatAsync(RoomId, text, token).ConfigureAwait(true);
                foreach (var ac in remote)
                {
                    var insert = ac.ResolveInsertText();
                    if (string.IsNullOrWhiteSpace(insert))
                    {
                        continue;
                    }

                    if (list.Any(x => string.Equals(x.InsertText.Trim(), insert.Trim(), StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var kind = (ac.Type ?? "suggest").ToLowerInvariant();
                    list.Add(new ChatSuggestionItem
                    {
                        Kind = kind,
                        Title = ac.ResolveTitle(),
                        Subtitle = ac.ResolveSubtitle(),
                        InsertText = insert,
                        Group = "服务端",
                    });
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // keep local suggestions
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            Suggestions.Clear();
            foreach (var s in list.Take(16))
            {
                Suggestions.Add(s);
            }

            HasSuggestions = Suggestions.Count > 0;
            SelectedSuggestionIndex = HasSuggestions ? 0 : -1;
            for (var i = 0; i < Suggestions.Count; i++)
            {
                Suggestions[i].IsSelected = i == 0;
            }

            SuggestionHintText = HasSuggestions
                ? $"↑↓ 选择 · Tab/Enter 填入 · Esc 关闭（{Suggestions.Count}）"
                : "输入 / 命令 · @ 提及 · 任意文本服务端联想";
        }
        catch (OperationCanceledException)
        {
            // debounce
        }
    }

    private static string? TryGetMentionQuery(string draft)
    {
        var at = draft.LastIndexOf('@');
        if (at < 0)
        {
            return null;
        }

        if (at > 0 && !char.IsWhiteSpace(draft[at - 1]))
        {
            // email-like, ignore
            return null;
        }

        var after = draft[(at + 1)..];
        if (after.Contains(' ', StringComparison.Ordinal) || after.Contains('\n', StringComparison.Ordinal))
        {
            return null;
        }

        return after;
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
            PersistMessagesToCache();
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

    [RelayCommand]
    private void ReplyToMessage(MessageItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        // Prefer real server id; empty id (local echo) cannot be replied to.
        if (item.MessageId == Guid.Empty && item.Message.Id == Guid.Empty)
        {
            _toast.Show("该消息尚未同步，稍后再回复");
            return;
        }

        ReplyTarget = item;
        OnPropertyChanged(nameof(HasReplyTarget));
        OnPropertyChanged(nameof(ReplyTargetVisibility));
        OnPropertyChanged(nameof(ReplyBannerText));
    }

    [RelayCommand]
    private void CancelReply()
    {
        ReplyTarget = null;
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

            var replyId = ReplyTarget?.Message.Id;
            if (replyId is null || replyId == Guid.Empty)
            {
                replyId = null;
            }

            var clientMessageId = Guid.NewGuid().ToString("N");
            var request = new SendMessageRequest
            {
                Content = string.IsNullOrWhiteSpace(text) ? null : text,
                ClientMessageId = clientMessageId,
                Nonce = Guid.NewGuid().ToString("N")[..16],
                AttachmentsId = string.IsNullOrWhiteSpace(imageId) ? null : [imageId!],
                RepliedMessageId = replyId,
            };

            await _api.SendMessageAsync(RoomId.ToString(), request).ConfigureAwait(true);
            Draft = string.Empty;
            var repliedSnapshot = ReplyTarget;
            ReplyTarget = null;
            ClearPendingImage();

            // Show immediately; the sync copy replaces the echo in place (matched by client_message_id).
            AddLocalEcho(text, imageId, clientMessageId, repliedSnapshot);
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

    private void AddLocalEcho(
        string? text,
        string? imageId,
        string clientMessageId,
        MessageItemViewModel? repliedTo = null)
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
            RepliedMessageId = repliedTo?.Message.Id is { } rid && rid != Guid.Empty ? rid : null,
            RepliedMessage = repliedTo?.Message,
        };

        AddMessageInternal(echo, append: true);
    }

    private static string TruncatePreview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "[消息]";
        }

        var t = text.Trim().Replace('\n', ' ');
        return t.Length <= 48 ? t : t[..48] + "…";
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
        // Leaving conversation → allow notifications for this room again
        if (_messageNotifier.ActiveRoomId == RoomId)
        {
            _messageNotifier.ActiveRoomId = null;
        }

        if (IsRecording)
        {
            _ = _voiceRecorder.CancelAsync();
            IsRecording = false;
        }

        _suggestCts?.Cancel();

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
            _cache.UpdateRoomSyncCursor(RoomId, _lastSyncTimestamp, _lastSyncMessageId);
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
                    _cache.UpsertRoomMessage(RoomId, message);
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
                    _cache.UpsertRoomMessage(RoomId, message);
                    return true;
                }
            }

            Messages.Add(item);
        }
        else
        {
            Messages.Insert(0, item);
        }

        _cache.UpsertRoomMessage(RoomId, message);
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

        _cache.UpdateRoomSyncCursor(RoomId, _lastSyncTimestamp, _lastSyncMessageId);
    }

    private bool TryPaintMessagesFromCache()
    {
        if (!_cache.TryGetRoomMessages(RoomId, out var entry) || entry.Messages.Count == 0)
        {
            return false;
        }

        Messages.Clear();
        _knownMessageIds.Clear();
        foreach (var msg in entry.Messages)
        {
            // Paint only — cache already holds data; avoid re-upsert loop noise.
            if (msg.Id != Guid.Empty)
            {
                _knownMessageIds.Add(msg.Id);
            }

            Messages.Add(new MessageItemViewModel(msg, _authService.CurrentAccount?.Id, _imageLoader));
        }

        _offset = entry.Offset > 0 ? entry.Offset : entry.Messages.Count;
        _hasMore = entry.HasMore;
        _lastSyncTimestamp = entry.LastSyncTimestamp;
        _lastSyncMessageId = entry.LastSyncMessageId;
        return true;
    }

    private void PersistMessagesToCache()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        var list = Messages.Select(m => m.Message).ToList();
        _cache.SetRoomMessages(
            RoomId,
            list,
            _lastSyncTimestamp,
            _lastSyncMessageId,
            _hasMore,
            _offset);
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
            // Resolve :prefix+slug: stickers → file ids before downloading bitmaps.
            await ResolveStickerFileIdsAsync().ConfigureAwait(true);

            var avatarTasks = new List<(MessageItemViewModel Msg, Task<BitmapImage?> Task)>();
            var attachmentTasks = new List<(MessageAttachmentViewModel Att, Task<BitmapImage?> Task)>();
            var stickerTasks = new List<(MessageStickerViewModel Sticker, Task<BitmapImage?> Task)>();

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

                foreach (var sticker in msg.Stickers.Where(s => s.Image is null && !string.IsNullOrWhiteSpace(s.FileId)))
                {
                    sticker.IsLoading = true;
                    stickerTasks.Add((sticker, _imageLoader.LoadSafeAsync(sticker.FileId)));
                }
            }

            if (avatarTasks.Count == 0 && attachmentTasks.Count == 0 && stickerTasks.Count == 0)
            {
                return;
            }

            // Download in parallel, then apply in one UI turn: avatars popping in one by
            // one down the list reads as flickering.
            await Task.WhenAll(
                avatarTasks.Select(t => t.Task)
                    .Concat(attachmentTasks.Select(t => t.Task))
                    .Concat(stickerTasks.Select(t => t.Task))).ConfigureAwait(true);

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

            foreach (var (sticker, task) in stickerTasks)
            {
                if (task.Result is { } bmp)
                {
                    sticker.Image = bmp;
                    sticker.ImageOpacity = 1.0;
                }

                sticker.IsLoading = false;
            }
        }
        finally
        {
            _mediaLoading = false;
        }
    }

    /// <summary>
    /// Fill sticker slots with DysonFS file ids from cache / owned packs / API batch lookup.
    /// </summary>
    private async Task ResolveStickerFileIdsAsync()
    {
        var pending = new List<(MessageItemViewModel Msg, string Placeholder)>();
        foreach (var msg in Messages.ToList())
        {
            if (msg.StickerPlaceholders.Count == 0)
            {
                continue;
            }

            var large = msg.IsStickerOnly || msg.StickerPlaceholders.Count <= 1;
            foreach (var ph in msg.StickerPlaceholders)
            {
                // Already have a slot with a file id
                var existing = msg.Stickers.FirstOrDefault(s =>
                    string.Equals(s.Placeholder, ph, StringComparison.OrdinalIgnoreCase));
                if (existing is { FileId: { Length: > 0 } })
                {
                    continue;
                }

                // Session / local pack cache
                if (TryGetCachedStickerFileId(ph, out var cached) && !string.IsNullOrWhiteSpace(cached))
                {
                    msg.EnsureStickerSlot(ph, cached, large);
                    continue;
                }

                var fromPack = TryResolveFromOwnedPacks(ph);
                if (!string.IsNullOrWhiteSpace(fromPack))
                {
                    RememberStickerFileId(ph, fromPack!);
                    msg.EnsureStickerSlot(ph, fromPack, large);
                    continue;
                }

                msg.EnsureStickerSlot(ph, fileId: null, large);
                pending.Add((msg, ph));
            }
        }

        if (pending.Count == 0)
        {
            return;
        }

        try
        {
            var placeholders = pending.Select(p => p.Placeholder).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var hits = await _api.LookupStickersBatchAsync(placeholders).ConfigureAwait(true);
            foreach (var hit in hits)
            {
                if (hit.Sticker is null)
                {
                    continue;
                }

                var fileId = CloudFileUrlHelper.ResolveFileId(hit.Sticker.Image)
                    ?? CloudFileUrlHelper.Resolve(hit.Sticker.Image);
                if (string.IsNullOrWhiteSpace(fileId))
                {
                    continue;
                }

                // Match keys: returned placeholder, resource_identifier, or normalized form
                var keySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                void AddKey(string? k)
                {
                    if (string.IsNullOrWhiteSpace(k))
                    {
                        return;
                    }

                    keySet.Add(k);
                    keySet.Add(NormalizeStickerKey(k));
                    if (!k.StartsWith(':'))
                    {
                        keySet.Add($":{NormalizeStickerKey(k)}:");
                    }
                }

                AddKey(hit.Placeholder);
                AddKey(hit.Sticker.ResourceIdentifier);

                foreach (var key in keySet)
                {
                    RememberStickerFileId(key, fileId);
                }

                foreach (var (msg, ph) in pending)
                {
                    var nk = NormalizeStickerKey(ph);
                    if (!keySet.Contains(ph) && !keySet.Contains(nk))
                    {
                        continue;
                    }

                    RememberStickerFileId(ph, fileId);
                    msg.EnsureStickerSlot(ph, fileId, msg.IsStickerOnly || msg.StickerPlaceholders.Count <= 1);
                }
            }

            // Any remaining: single lookup (batch may omit unmatched)
            foreach (var (msg, ph) in pending)
            {
                var slot = msg.Stickers.FirstOrDefault(s =>
                    string.Equals(s.Placeholder, ph, StringComparison.OrdinalIgnoreCase));
                if (slot is { FileId: { Length: > 0 } })
                {
                    continue;
                }

                if (TryGetCachedStickerFileId(ph, out var cached2) && !string.IsNullOrWhiteSpace(cached2))
                {
                    msg.EnsureStickerSlot(ph, cached2, msg.IsStickerOnly);
                    continue;
                }

                var sticker = await _api.LookupStickerAsync(ph).ConfigureAwait(true);
                if (sticker is null)
                {
                    continue;
                }

                var fileId = CloudFileUrlHelper.ResolveFileId(sticker.Image)
                    ?? CloudFileUrlHelper.Resolve(sticker.Image);
                if (string.IsNullOrWhiteSpace(fileId))
                {
                    continue;
                }

                RememberStickerFileId(ph, fileId);
                msg.EnsureStickerSlot(ph, fileId, msg.IsStickerOnly || msg.StickerPlaceholders.Count <= 1);
            }
        }
        catch
        {
            // Stickers stay as blank slots; raw placeholder already stripped from text.
        }
    }

    private string? TryResolveFromOwnedPacks(string placeholder)
    {
        var key = NormalizeStickerKey(placeholder);
        var plus = key.IndexOf('+');
        if (plus <= 0 || plus >= key.Length - 1)
        {
            return null;
        }

        var prefix = key[..plus];
        var slug = key[(plus + 1)..];

        foreach (var (pack, stickers) in _stickerPackCache)
        {
            if (!string.Equals(pack.Prefix, prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var s in stickers)
            {
                if (!string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return CloudFileUrlHelper.ResolveFileId(s.Image)
                    ?? CloudFileUrlHelper.Resolve(s.Image);
            }
        }

        return null;
    }

    private static string NormalizeStickerKey(string placeholder)
    {
        var key = placeholder.Trim();
        if (key.StartsWith(':') && key.EndsWith(':') && key.Length > 2)
        {
            key = key[1..^1];
        }

        return key;
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

/// <summary>Bot command row for the room command panel.</summary>
public sealed class ChatBotCommandItem
{
    public ChatBotCommandItem(ChatBotCommand command)
    {
        Command = command;
        Title = command.DisplayName;
        Subtitle = command.DisplayDetail;
        InsertText = command.DisplayName + " ";
    }

    public ChatBotCommand Command { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string InsertText { get; }
}

/// <summary>Pack tab in sticker picker.</summary>
public sealed class StickerPackTabItem
{
    public Guid PackId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Prefix { get; init; } = string.Empty;
}

/// <summary>Sticker cell — send as <c>:{prefix}+{slug}:</c>.</summary>
public partial class StickerPickItem : ObservableObject
{
    public string PackPrefix { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    /// <summary>0 = sticker (send alone), 1 = emote (insert into draft).</summary>
    public int Mode { get; init; }

    public string? ImageFileId { get; init; }

    [ObservableProperty]
    public partial BitmapImage? Image { get; set; }
}
