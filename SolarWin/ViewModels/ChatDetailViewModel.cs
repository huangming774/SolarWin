using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Data;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class ChatDetailViewModel : ObservableObject
{
    private const int PageSize = 30;
    /// <summary>Design v1.0 Phase 5: hard UI window.</summary>
    private const int MaxUiMessages = 200;
    /// <summary>Design v1.0 Phase 5: drop this many from the newest side when over cap.</summary>
    private const int UiTrimBatch = 30;
    private static readonly TimeSpan SyncInterval = TimeSpan.FromSeconds(10);

    /// <summary>Session cache: <c>:prefix+slug:</c> or <c>prefix+slug</c> → DysonFS file id (capped LRU-ish).</summary>
    private static readonly ConcurrentDictionary<string, string> StickerFileIdCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Hard cap so the static cache cannot grow without bound over long sessions.</summary>
    private const int MaxStickerCacheEntries = 512;

    private readonly ISolarApiClient _api;
    private readonly IAuthService _authService;
    private readonly IToastService _toast;
    private readonly IVoiceRecorderService _voiceRecorder;
    private readonly IRealtimeCallService _realtimeCall;
    private readonly IIncomingCallService _incomingCalls;
    private readonly IChatWebSocketService _ws;
    private readonly IChatMessageNotifier _messageNotifier;
    private readonly IChatDataCache _cache;
    private readonly IChatLocalStore _localStore;
    private readonly DysonFileImageLoader _imageLoader;
    private readonly ChatViewModel _chatList;
    private readonly HashSet<Guid> _knownMessageIds = [];
    private readonly List<ChatBotCommand> _botCommands = [];

    private CancellationTokenSource? _syncCts;
    private CancellationTokenSource? _suggestCts;
    private CancellationTokenSource? _loadCts;
    private int _offset;
    private bool _hasMore = true;
    /// <summary>Phase 3: whether older history may still exist (local and/or server).</summary>
    private bool _hasMoreOlder = true;
    /// <summary>Phase 3: keyset cursor at the oldest message currently painted.</summary>
    private MessageKeysetCursor? _oldestLoaded;
    /// <summary>
    /// Phase 5: true when newer messages were trimmed from the UI tail (still in L2).
    /// </summary>
    private bool _hasDetachedNewer;
    /// <summary>
    /// Phase 5: last painted message after tail trim — L2 rows newer than this were detached.
    /// </summary>
    private MessageKeysetCursor? _detachedAfter;
    private bool _isRehydratingTail;
    private long _lastSyncTimestamp;
    private Guid? _lastSyncMessageId;
    private bool _markedRead;
    private bool _wsHooked;
    private JoinCallResponse? _activeCall;
    private bool _callEventsHooked;
    private bool _voiceHooked;
    private int _voiceLimitStopRequested;
    /// <summary>Generation stamp so cancelled / superseded loads never mutate the active room.</summary>
    private int _loadGeneration;

    // Stored delegates so transient VMs can detach from singleton services (see Unhook).
    private readonly EventHandler _callStateHandler;
    private readonly EventHandler _callParticipantsHandler;
    private readonly EventHandler _callDevicesHandler;
    private readonly EventHandler _callVideoHandler;

    public ChatDetailViewModel(
        ISolarApiClient api,
        IAuthService authService,
        IToastService toast,
        IVoiceRecorderService voiceRecorder,
        IRealtimeCallService realtimeCall,
        IIncomingCallService incomingCalls,
        IChatWebSocketService ws,
        IChatMessageNotifier messageNotifier,
        IChatDataCache cache,
        IChatLocalStore localStore,
        DysonFileImageLoader imageLoader,
        ChatViewModel chatList)
    {
        _api = api;
        _authService = authService;
        _toast = toast;
        _voiceRecorder = voiceRecorder;
        _realtimeCall = realtimeCall;
        _incomingCalls = incomingCalls;
        _ws = ws;
        _messageNotifier = messageNotifier;
        _cache = cache;
        _localStore = localStore;
        _imageLoader = imageLoader;
        _chatList = chatList;
        _callStateHandler = (_, _) => EnqueueUi(SyncCallUiFromService);
        _callParticipantsHandler = (_, _) => EnqueueUi(SyncCallParticipantsFromMedia);
        _callDevicesHandler = (_, _) => EnqueueUi(RefreshDeviceNameLists);
        _callVideoHandler = (_, _) => EnqueueUi(() =>
        {
            RemoteVideoBitmap = _realtimeCall.FocusedRemoteVideo;
            LocalVideoBitmap = _realtimeCall.LocalVideo;
        });
        HookVoiceRecorder();
        HookRealtimeCallEvents();
        _realtimeCall.RefreshDeviceLists();
    }

    private void HookVoiceRecorder()
    {
        if (_voiceHooked)
        {
            return;
        }

        _voiceHooked = true;
        _voiceRecorder.ElapsedChanged += OnVoiceElapsedChanged;
    }

    public RangeObservableCollection<MessageItemViewModel> Messages { get; } = [];

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
    [NotifyPropertyChangedFor(nameof(InCallControlsVisibility))]
    [NotifyPropertyChangedFor(nameof(JoinCallVisibility))]
    [NotifyPropertyChangedFor(nameof(MicToggleLabel))]
    [NotifyPropertyChangedFor(nameof(CameraToggleLabel))]
    public partial string CallStatusText { get; set; } = "未加入通话";

    [ObservableProperty]
    public partial string CallEndpointText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MicToggleLabel))]
    public partial bool IsMicOn { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CameraToggleLabel))]
    public partial bool IsCameraOn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScreenShareToggleLabel))]
    public partial bool IsScreenSharing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SidetoneToggleLabel))]
    public partial bool IsSidetoneOn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InCallControlsVisibility))]
    [NotifyPropertyChangedFor(nameof(JoinCallVisibility))]
    public partial bool IsInCall { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InCallControlsVisibility))]
    [NotifyPropertyChangedFor(nameof(JoinCallVisibility))]
    public partial bool IsCallConnecting { get; set; }

    [ObservableProperty]
    public partial bool IsCallReconnecting { get; set; }

    [ObservableProperty]
    public partial string ConnectionQualityText { get; set; } = "未知";

    [ObservableProperty]
    public partial Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap? RemoteVideoBitmap { get; set; }

    [ObservableProperty]
    public partial Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap? LocalVideoBitmap { get; set; }

    /// <summary>Multi-party video tiles from media service.</summary>
    public System.Collections.ObjectModel.ObservableCollection<CallVideoTile> VideoTiles =>
        _realtimeCall.VideoTiles;

    public System.Collections.ObjectModel.ObservableCollection<string> MicDeviceNames { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<string> SpeakerDeviceNames { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<string> CameraDeviceNames { get; } = [];

    [ObservableProperty]
    public partial int SelectedMicDeviceIndex { get; set; } = -1;

    [ObservableProperty]
    public partial int SelectedSpeakerDeviceIndex { get; set; } = -1;

    [ObservableProperty]
    public partial int SelectedCameraDeviceIndex { get; set; } = -1;

    public string MicToggleLabel => IsMicOn ? "静音" : "开麦";

    public string CameraToggleLabel => IsCameraOn ? "关摄像头" : "开摄像头";

    public string ScreenShareToggleLabel => IsScreenSharing ? "停共享" : "共享屏幕";

    public string SidetoneToggleLabel => IsSidetoneOn ? "关耳返" : "耳返";

    public Microsoft.UI.Xaml.Visibility InCallControlsVisibility =>
        IsInCall ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility JoinCallVisibility =>
        IsInCall || IsCallConnecting
            ? Microsoft.UI.Xaml.Visibility.Collapsed
            : Microsoft.UI.Xaml.Visibility.Visible;

    public Microsoft.UI.Xaml.Visibility ReconnectingBannerVisibility =>
        IsCallReconnecting ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

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

    /// <summary>Phase 5: UI dropped newer bubbles; scroll-to-bottom should rehydrate from L2.</summary>
    public bool HasDetachedNewer => _hasDetachedNewer;

    public void Initialize(Guid roomId, string? title)
    {
        if (IsRecording)
        {
            _ = _voiceRecorder.CancelAsync();
            IsRecording = false;
        }

        // Revisited via back-nav after Unhook → re-attach.
        HookVoiceRecorder();
        HookRealtimeCallEvents();

        // Abort any in-flight load for the previous room before swapping identity.
        CancelMessageLoad();
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
        _ = HangUpInternalAsync(showToast: false);
        CallStatusText = "未加入通话";
        CallEndpointText = string.Empty;
        IsInCall = false;
        IsCallConnecting = false;
        IsMicOn = true;
        IsCameraOn = false;
        RemoteVideoBitmap = null;
        LocalVideoBitmap = null;
        _botCommands.Clear();
        _offset = 0;
        _hasMore = true;
        _hasMoreOlder = true;
        _oldestLoaded = null;
        _hasDetachedNewer = false;
        _detachedAfter = null;
        _isRehydratingTail = false;
        _lastSyncTimestamp = 0;
        _lastSyncMessageId = null;
        Draft = string.Empty;
        ReplyTarget = null;
        ClearPendingImage();
        ErrorMessage = null;
        _markedRead = false;
        _voiceLimitStopRequested = 0;
        RecordingElapsedText = "0:00";
        UpdateCanSend();

        // Clear unread badge immediately when opening room
        _chatList.MarkRoomReadLocal(roomId);
    }

    private void CancelMessageLoad()
    {
        _loadGeneration++;
        if (_loadCts is null)
        {
            return;
        }

        try
        {
            _loadCts.Cancel();
        }
        catch
        {
            // ignore
        }

        _loadCts.Dispose();
        _loadCts = null;
    }

    private bool IsStaleLoad(int generation, Guid roomId, CancellationToken cancellationToken)
        => cancellationToken.IsCancellationRequested
           || generation != _loadGeneration
           || roomId != RoomId;

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
        var reachedLimit = elapsed >= _voiceRecorder.MaxDuration;
        var text = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";

        void Update()
        {
            RecordingElapsedText = text;
            if (reachedLimit
                && IsRecording
                && Interlocked.Exchange(ref _voiceLimitStopRequested, 1) == 0
                && ToggleVoiceRecordCommand.CanExecute(null))
            {
                _ = ToggleVoiceRecordCommand.ExecuteAsync(null);
            }
        }

        if (App.DispatcherQueue is { } dq && !dq.HasThreadAccess)
        {
            dq.TryEnqueue(Update);
            return;
        }

        Update();
    }

    [RelayCommand]
    private async Task LoadInitialAsync()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        CancelMessageLoad();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;
        var generation = _loadGeneration;
        var roomId = RoomId;
        var currentAccountId = _authService.CurrentAccount?.Id;

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            Messages.Clear();
            _knownMessageIds.Clear();
            _offset = 0;
            _hasMore = true;
            _hasMoreOlder = true;
            _oldestLoaded = null;
            _hasDetachedNewer = false;
            _detachedAfter = null;

            // Phase 4: L1 → SQLite newest page → instant paint; REST soft-refresh calibrates.
            var paintedLocal = TryPaintMessagesFromCache();
            if (!paintedLocal)
            {
                paintedLocal = await TryPaintMessagesFromSqliteAsync(roomId, generation, ct, currentAccountId)
                    .ConfigureAwait(false);
            }

            if (paintedLocal)
            {
                await RunOnUiAsync(() =>
                {
                    if (!IsStaleLoad(generation, roomId, ct))
                    {
                        // 秒开：本地有内容时先结束 busy，REST 在后台校准。
                        IsBusy = false;
                        ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
                    }
                }).ConfigureAwait(false);
            }

            try
            {
                // Soft-refresh: always try network when possible (offset=0 first page).
                var batch = await _api
                    .GetMessagesAsync(roomId.ToString(), offset: 0, take: PageSize, ct)
                    .ConfigureAwait(false);

                if (IsStaleLoad(generation, roomId, ct))
                {
                    return;
                }

                var ordered = await Task.Run(() => NormalizeOrder(batch), ct).ConfigureAwait(false);

                if (IsStaleLoad(generation, roomId, ct))
                {
                    return;
                }

                await RunOnUiAsync(() =>
                {
                    if (IsStaleLoad(generation, roomId, ct))
                    {
                        return;
                    }

                    if (paintedLocal)
                    {
                        // Merge: upsert server page + drop locals that server deleted (in-window).
                        CalibrateFirstPageFromServer(ordered, batch.Count, currentAccountId);
                    }
                    else
                    {
                        var items = BuildMessageItems(ordered, currentAccountId);
                        ApplyMessagePage(items, batch.Count, hasMore: batch.Count >= PageSize, replace: true);
                        // Phase 7: incremental dual-write (not full-window SetRoomMessages → SQLite).
                        foreach (var msg in ordered)
                        {
                            try
                            {
                                _cache.UpsertRoomMessage(roomId, msg);
                            }
                            catch
                            {
                                // ignore
                            }
                        }
                    }

                    // L1 snapshot only (no SQLite full flush).
                    PersistMessagesToCache();
                    ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SolarApiException ex)
            {
                if (!paintedLocal && !IsStaleLoad(generation, roomId, ct))
                {
                    await RunOnUiAsync(() =>
                    {
                        if (!IsStaleLoad(generation, roomId, ct))
                        {
                            ErrorMessage = ex.ApiMessage ?? ex.Message;
                        }
                    }).ConfigureAwait(false);
                }
                // else keep local messages on screen (弱网/断网秒开)
            }

            if (IsStaleLoad(generation, roomId, ct))
            {
                return;
            }

            // Media / meta / bots must not block window chrome; cancel with this room load.
            _ = LoadMediaAsync(ct, generation, roomId);
            _ = MarkRoomReadIfNeededAsync();
            _ = LoadRoomMetaAsync(ct, generation, roomId);
            _ = LoadBotCommandsAsync(ct, generation, roomId);
            // Full member list only when the panel is opened (large groups are expensive).
            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, ct))
                {
                    HookWebSocket();
                }
            }).ConfigureAwait(false);
            _ = EstablishRealtimeAsync(roomId, generation, ct);
        }
        catch (OperationCanceledException)
        {
            // superseded / left page
        }
        catch (SolarApiException ex)
        {
            if (!IsStaleLoad(generation, roomId, ct))
            {
                await RunOnUiAsync(() =>
                {
                    if (!IsStaleLoad(generation, roomId, ct))
                    {
                        ErrorMessage = ex.Message;
                    }
                }).ConfigureAwait(false);
            }
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                // Bound properties must be set on the UI thread (WinUI COM affinity).
                await RunOnUiAsync(() =>
                {
                    if (generation == _loadGeneration)
                    {
                        IsBusy = false;
                    }
                }).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Phase 4: L1 miss → paint newest <see cref="PageSize"/> from SQLite (async; UI apply on dispatcher).
    /// </summary>
    private async Task<bool> TryPaintMessagesFromSqliteAsync(
        Guid roomId,
        int generation,
        CancellationToken cancellationToken,
        Guid? currentAccountId)
    {
        IReadOnlyList<SnChatMessage> newest;
        try
        {
            newest = await _localStore
                .GetNewestMessagesAsync(roomId, PageSize, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatDetail] SQLite first paint: {ex.Message}");
            return false;
        }

        if (newest.Count == 0 || IsStaleLoad(generation, roomId, cancellationToken))
        {
            return false;
        }

        var ordered = await Task.Run(
                () => NormalizeOrder(newest as List<SnChatMessage> ?? newest.ToList()),
                cancellationToken)
            .ConfigureAwait(false);
        if (IsStaleLoad(generation, roomId, cancellationToken))
        {
            return false;
        }

        var painted = false;
        await RunOnUiAsync(() =>
        {
            if (IsStaleLoad(generation, roomId, cancellationToken))
            {
                return;
            }

            // If L1 was filled by a race, keep L1 paint.
            if (Messages.Count > 0)
            {
                painted = true;
                return;
            }

            var items = BuildMessageItems(ordered, currentAccountId);
            ApplyMessagePage(
                items,
                paintedCount: ordered.Count,
                hasMore: ordered.Count >= PageSize,
                replace: true);

            // Warm L1 so subsequent navigations hit memory without re-reading disk.
            try
            {
                _cache.SetRoomMessages(
                    roomId,
                    ordered,
                    _lastSyncTimestamp,
                    _lastSyncMessageId,
                    hasMore: ordered.Count >= PageSize,
                    offset: ordered.Count);
            }
            catch
            {
                // ignore
            }

            painted = true;
        }).ConfigureAwait(false);

        return painted;
    }

    /// <summary>
    /// Soft-refresh merge after local first paint (UI thread):
    /// upsert server first page; remove locals that fall inside the server window but are absent (deleted).
    /// Keeps optimistics (empty Id), messages older than the window, and messages newer than the window.
    /// </summary>
    private void CalibrateFirstPageFromServer(
        IReadOnlyList<SnChatMessage> ordered,
        int batchCount,
        Guid? currentAccountId)
    {
        // Cold path: nothing local — should not reach here, but safe.
        if (Messages.Count == 0)
        {
            var items = BuildMessageItems(ordered, currentAccountId);
            ApplyMessagePage(items, batchCount, hasMore: batchCount >= PageSize, replace: true);
            return;
        }

        var serverIds = new HashSet<Guid>();
        long minServerSeq = long.MaxValue;
        long maxServerSeq = long.MinValue;
        var hasPositiveSeq = false;
        foreach (var m in ordered)
        {
            if (m.Id != Guid.Empty)
            {
                serverIds.Add(m.Id);
            }

            if (m.RoomSequence > 0)
            {
                hasPositiveSeq = true;
                if (m.RoomSequence < minServerSeq)
                {
                    minServerSeq = m.RoomSequence;
                }

                if (m.RoomSequence > maxServerSeq)
                {
                    maxServerSeq = m.RoomSequence;
                }
            }
        }

        // 1) Remove locals that belong in the server first-page window but are missing → deleted.
        for (var i = Messages.Count - 1; i >= 0; i--)
        {
            var msg = Messages[i].Message;
            if (msg.Id == Guid.Empty)
            {
                continue; // optimistic keep
            }

            if (serverIds.Contains(msg.Id))
            {
                continue;
            }

            if (hasPositiveSeq && msg.RoomSequence > 0)
            {
                // Older than window → history keep; newer than window → WS/local race keep.
                if (msg.RoomSequence < minServerSeq || msg.RoomSequence > maxServerSeq)
                {
                    continue;
                }
            }
            else
            {
                // No reliable sequence on server page: only drop if UI is still first-page sized.
                if (Messages.Count > PageSize)
                {
                    continue;
                }
            }

            _knownMessageIds.Remove(msg.Id);
            Messages.RemoveAt(i);
            try
            {
                _cache.RemoveRoomMessage(RoomId, msg.Id, msg.ClientMessageId);
            }
            catch
            {
                // ignore
            }
        }

        // 2) Upsert each server message (update in place or insert by sequence).
        foreach (var msg in ordered)
        {
            var item = new MessageItemViewModel(msg, currentAccountId, _imageLoader);
            if (!TryAdoptPreparedMessage(item, append: true))
            {
                // Already present: force payload refresh (edits / reactions).
                if (msg.Id != Guid.Empty)
                {
                    for (var i = 0; i < Messages.Count; i++)
                    {
                        if (Messages[i].Message.Id == msg.Id)
                        {
                            Messages[i] = item;
                            _cache.UpsertRoomMessage(RoomId, msg);
                            break;
                        }
                    }
                }
            }
        }

        _offset = Math.Max(_offset, batchCount);
        // Always allow LoadMore to probe older local / API; short server page ≠ no history.
        _hasMoreOlder = true;
        _hasMore = true;
        UpdateSyncCursorFromMessages();
        RefreshOldestLoadedCursor();
    }

    private async Task EstablishRealtimeAsync(Guid roomId, int generation, CancellationToken cancellationToken)
    {
        try
        {
            await _api.MarkDeviceJoinedRoomAsync(roomId, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // optional presence
        }

        if (IsStaleLoad(generation, roomId, cancellationToken))
        {
            return;
        }

        try
        {
            await _ws.ConnectAsync(cancellationToken).ConfigureAwait(false);
            var mode = _ws.State == ChatWsConnectionState.Connected ? "实时: WebSocket" : "实时: 轮询";
            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, cancellationToken))
                {
                    RealtimeModeText = mode;
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch
        {
            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, cancellationToken))
                {
                    RealtimeModeText = "实时: 轮询";
                }
            }).ConfigureAwait(false);
        }
    }

    private Task LoadRoomMetaAsync()
        => LoadRoomMetaAsync(CancellationToken.None, _loadGeneration, RoomId);

    private async Task LoadRoomMetaAsync(CancellationToken cancellationToken, int generation, Guid roomId)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        try
        {
            var online = await _api.GetOnlineMembersAsync(roomId, cancellationToken).ConfigureAwait(false);
            if (IsStaleLoad(generation, roomId, cancellationToken))
            {
                return;
            }

            var text = online.OnlineCount > 0
                ? $"在线 {online.OnlineCount}"
                : "在线 —";
            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, cancellationToken))
                {
                    OnlineStatusText = text;
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, cancellationToken))
                {
                    OnlineStatusText = string.Empty;
                }
            }).ConfigureAwait(false);
        }

        try
        {
            var members = await _api.GetChatMembersAsync(roomId, cancellationToken).ConfigureAwait(false);
            if (IsStaleLoad(generation, roomId, cancellationToken))
            {
                return;
            }

            var summary = $"成员 {members.Count}";
            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, cancellationToken))
                {
                    MembersSummaryText = summary;
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, cancellationToken))
                {
                    MembersSummaryText = string.Empty;
                }
            }).ConfigureAwait(false);
        }

        try
        {
            var pins = await _api
                .GetPinnedMessagesAsync(roomId, includeExpired: false, cancellationToken)
                .ConfigureAwait(false);
            if (IsStaleLoad(generation, roomId, cancellationToken))
            {
                return;
            }

            var pinText = pins.Count > 0 ? $"置顶 {pins.Count}" : string.Empty;
            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, cancellationToken))
                {
                    PinnedSummaryText = pinText;
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, cancellationToken))
                {
                    PinnedSummaryText = string.Empty;
                }
            }).ConfigureAwait(false);
        }

        try
        {
            var me = await _api.GetMyChatMembershipAsync(roomId, cancellationToken).ConfigureAwait(false);
            if (IsStaleLoad(generation, roomId, cancellationToken))
            {
                return;
            }

            var notify = me.Notify switch
            {
                ChatMemberNotify.Mentions => "通知：仅提及",
                ChatMemberNotify.None => "通知：关闭",
                _ => "通知：全部",
            };
            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, cancellationToken))
                {
                    NotifyModeText = notify;
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignore
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
            // Dual-write delete to L1 + SQLite (Phase 2); UI already updated.
            _cache.RemoveRoomMessage(RoomId, item.Message.Id, item.Message.ClientMessageId);
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

        var roomId = RoomId;

        // Paint from cache while refreshing (batch replace when collection supports it).
        if (Members.Count == 0
            && _cache.TryGetRoomMembers(roomId, out var cachedMembers, out _)
            && cachedMembers.Count > 0)
        {
            ReplaceMembers(cachedMembers);
            MembersSummaryText = $"成员 {Members.Count}";
        }

        if (_cache.IsRoomMembersFresh(roomId) && Members.Count > 0)
        {
            return;
        }

        try
        {
            var list = await _api.GetChatMembersAsync(roomId).ConfigureAwait(false);
            if (roomId != RoomId)
            {
                return;
            }

            _cache.SetRoomMembers(roomId, list);
            await RunOnUiAsync(() =>
            {
                if (roomId != RoomId)
                {
                    return;
                }

                ReplaceMembers(list);
                MembersSummaryText = $"成员 {Members.Count}";
            }).ConfigureAwait(false);

            try
            {
                var me = await _api.GetMyChatMembershipAsync(roomId).ConfigureAwait(false);
                if (roomId != RoomId)
                {
                    return;
                }

                await RunOnUiAsync(() =>
                {
                    if (roomId == RoomId)
                    {
                        MyRoomNick = me.Nick ?? string.Empty;
                    }
                }).ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }
        catch (SolarApiException ex)
        {
            if (roomId == RoomId && Members.Count == 0)
            {
                _toast.Error($"成员列表失败：{ex.ApiMessage ?? ex.Message}");
            }
        }
    }

    private void ReplaceMembers(IReadOnlyList<SnChatMember> list)
    {
        // Members stays as ObservableCollection; rebuild with minimal churn via Clear + Add
        // after constructing VMs off the notification path.
        var items = list.Select(m => new ChatMemberItemViewModel(m)).ToList();
        Members.Clear();
        foreach (var item in items)
        {
            Members.Add(item);
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
        if (RoomId == Guid.Empty || IsInCall || IsCallConnecting)
        {
            return;
        }

        try
        {
            IsCallPanelOpen = true;
            IsCallConnecting = true;
            CallStatusText = "正在创建通话会话…";

            var call = await _api.JoinRealtimeCallAsync(RoomId).ConfigureAwait(true);
            _activeCall = call;
            CallEndpointText = string.IsNullOrWhiteSpace(call.Endpoint)
                ? "(无 endpoint)"
                : call.Endpoint!;
            CallStatusText = $"信令就绪 · {call.Provider ?? "livekit"} · 连接媒体…";

            CallParticipants.Clear();
            foreach (var p in call.Participants ?? [])
            {
                CallParticipants.Add(new CallParticipantItemViewModel(p));
            }

            // Real media: LiveKit via endpoint + token
            await _realtimeCall.ConnectAsync(call).ConfigureAwait(true);
            IsInCall = _realtimeCall.IsConnected;
            IsCallConnecting = false;
            IsMicOn = _realtimeCall.IsMicEnabled;
            IsCameraOn = _realtimeCall.IsCameraEnabled;
            IsScreenSharing = _realtimeCall.IsScreenShareEnabled;
            IsSidetoneOn = _realtimeCall.IsSidetoneEnabled;
            ConnectionQualityText = _realtimeCall.ConnectionQualityText;
            CallStatusText = _realtimeCall.StatusText ?? "通话中";
            SyncCallParticipantsFromMedia();
            RefreshDeviceNameLists();
            _toast.Success("已加入通话（麦克风已开；可开摄像头 / 共享屏幕）");
            _ = RefreshCallAsync();
        }
        catch (SolarApiException ex)
        {
            IsCallConnecting = false;
            IsInCall = false;
            CallStatusText = "加入失败";
            _toast.Error($"加入通话失败：{ex.ApiMessage ?? ex.Message}");
        }
        catch (Exception ex)
        {
            IsCallConnecting = false;
            IsInCall = false;
            CallStatusText = "媒体连接失败";
            _toast.Error("媒体连接失败：" + ex.Message);
            try
            {
                await _realtimeCall.DisconnectAsync().ConfigureAwait(true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [RelayCommand]
    private async Task HangUpAsync()
    {
        await HangUpInternalAsync(showToast: true).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ToggleMicAsync()
    {
        if (!IsInCall)
        {
            return;
        }

        try
        {
            var next = !IsMicOn;
            await _realtimeCall.SetMicEnabledAsync(next).ConfigureAwait(true);
            IsMicOn = _realtimeCall.IsMicEnabled;
            CallStatusText = _realtimeCall.StatusText ?? CallStatusText;
        }
        catch (Exception ex)
        {
            _toast.Error("麦克风切换失败：" + ex.Message);
        }
    }

    [RelayCommand]
    private async Task ToggleCameraAsync()
    {
        if (!IsInCall)
        {
            return;
        }

        try
        {
            var next = !IsCameraOn;
            await _realtimeCall.SetCameraEnabledAsync(next).ConfigureAwait(true);
            IsCameraOn = _realtimeCall.IsCameraEnabled;
            IsScreenSharing = _realtimeCall.IsScreenShareEnabled;
            LocalVideoBitmap = _realtimeCall.LocalVideo;
            CallStatusText = _realtimeCall.StatusText ?? CallStatusText;
            SyncCallParticipantsFromMedia();
        }
        catch (Exception ex)
        {
            _toast.Error("摄像头切换失败：" + ex.Message);
        }
    }

    [RelayCommand]
    private async Task ToggleScreenShareAsync()
    {
        if (!IsInCall)
        {
            return;
        }

        try
        {
            var next = !IsScreenSharing;
            await _realtimeCall.SetScreenShareEnabledAsync(next).ConfigureAwait(true);
            IsScreenSharing = _realtimeCall.IsScreenShareEnabled;
            IsCameraOn = _realtimeCall.IsCameraEnabled;
            CallStatusText = _realtimeCall.StatusText ?? CallStatusText;
            SyncCallParticipantsFromMedia();
        }
        catch (Exception ex)
        {
            _toast.Error("屏幕共享失败：" + ex.Message);
        }
    }

    [RelayCommand]
    private async Task ToggleSidetoneAsync()
    {
        if (!IsInCall)
        {
            return;
        }

        try
        {
            var next = !IsSidetoneOn;
            await _realtimeCall.SetSidetoneEnabledAsync(next).ConfigureAwait(true);
            IsSidetoneOn = _realtimeCall.IsSidetoneEnabled;
            CallStatusText = _realtimeCall.StatusText ?? CallStatusText;
        }
        catch (Exception ex)
        {
            _toast.Error("耳返切换失败：" + ex.Message);
        }
    }

    [RelayCommand]
    private async Task ApplyMicDeviceAsync()
    {
        if (SelectedMicDeviceIndex < 0)
        {
            return;
        }

        try
        {
            await _realtimeCall.SelectMicrophoneAsync(SelectedMicDeviceIndex).ConfigureAwait(true);
            CallStatusText = _realtimeCall.StatusText ?? CallStatusText;
        }
        catch (Exception ex)
        {
            _toast.Error("切换麦克风失败：" + ex.Message);
        }
    }

    [RelayCommand]
    private async Task ApplySpeakerDeviceAsync()
    {
        if (SelectedSpeakerDeviceIndex < 0)
        {
            return;
        }

        try
        {
            await _realtimeCall.SelectSpeakerAsync(SelectedSpeakerDeviceIndex).ConfigureAwait(true);
            CallStatusText = _realtimeCall.StatusText ?? CallStatusText;
        }
        catch (Exception ex)
        {
            _toast.Error("切换扬声器失败：" + ex.Message);
        }
    }

    [RelayCommand]
    private async Task ApplyCameraDeviceAsync()
    {
        if (SelectedCameraDeviceIndex < 0)
        {
            return;
        }

        try
        {
            await _realtimeCall.SelectCameraAsync(SelectedCameraDeviceIndex).ConfigureAwait(true);
            CallStatusText = _realtimeCall.StatusText ?? CallStatusText;
        }
        catch (Exception ex)
        {
            _toast.Error("切换摄像头失败：" + ex.Message);
        }
    }

    [RelayCommand]
    private void FocusVideoTile(CallVideoTile? tile)
    {
        if (tile is null)
        {
            return;
        }

        _realtimeCall.SetFocusedIdentity(tile.IsLocal ? null : tile.Identity);
        RemoteVideoBitmap = _realtimeCall.FocusedRemoteVideo;
        LocalVideoBitmap = _realtimeCall.LocalVideo;
    }

    /// <summary>Join after accepting a global incoming-call banner.</summary>
    public async Task AcceptIncomingAndJoinAsync(Guid roomId, string? title)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        if (RoomId != roomId)
        {
            Initialize(roomId, title);
            StartPolling();
            if (LoadInitialCommand.CanExecute(null))
            {
                await LoadInitialCommand.ExecuteAsync(null).ConfigureAwait(true);
            }
        }

        IsCallPanelOpen = true;
        if (JoinVoiceCommand.CanExecute(null))
        {
            await JoinVoiceCommand.ExecuteAsync(null).ConfigureAwait(true);
        }
    }

    private async Task HangUpInternalAsync(bool showToast)
    {
        try
        {
            await _realtimeCall.DisconnectAsync().ConfigureAwait(true);
        }
        catch
        {
            // ignore
        }

        _activeCall = null;
        IsInCall = false;
        IsCallConnecting = false;
        IsCallReconnecting = false;
        IsMicOn = true;
        IsCameraOn = false;
        IsScreenSharing = false;
        IsSidetoneOn = false;
        RemoteVideoBitmap = null;
        LocalVideoBitmap = null;
        ConnectionQualityText = "未知";
        CallStatusText = "未加入通话";
        CallEndpointText = string.Empty;
        if (showToast)
        {
            _toast.Show("已挂断");
        }
    }

    private void HookRealtimeCallEvents()
    {
        if (_callEventsHooked)
        {
            return;
        }

        _callEventsHooked = true;
        _realtimeCall.StateChanged += _callStateHandler;
        _realtimeCall.ParticipantsChanged += _callParticipantsHandler;
        _realtimeCall.DevicesChanged += _callDevicesHandler;
        _realtimeCall.VideoFrameUpdated += _callVideoHandler;
    }

    /// <summary>
    /// Detach from singleton services (voice recorder / realtime call / chat WebSocket)
    /// so this transient VM — and its whole Messages collection incl. bitmaps — can be
    /// garbage collected after leaving the page. Idempotent; Initialize re-hooks.
    /// </summary>
    public void Unhook()
    {
        if (_voiceHooked)
        {
            _voiceHooked = false;
            _voiceRecorder.ElapsedChanged -= OnVoiceElapsedChanged;
        }

        if (_callEventsHooked)
        {
            _callEventsHooked = false;
            _realtimeCall.StateChanged -= _callStateHandler;
            _realtimeCall.ParticipantsChanged -= _callParticipantsHandler;
            _realtimeCall.DevicesChanged -= _callDevicesHandler;
            _realtimeCall.VideoFrameUpdated -= _callVideoHandler;
        }

        if (_wsHooked)
        {
            _wsHooked = false;
            _ws.PacketReceived -= OnRoomWsPacket;
            _ws.StateChanged -= OnWsStateChanged;
        }

        try
        {
            _suggestCts?.Cancel();
        }
        catch
        {
            // ignore
        }
    }

    private void RefreshDeviceNameLists()
    {
        MicDeviceNames.Clear();
        foreach (var d in _realtimeCall.Microphones)
        {
            MicDeviceNames.Add(d.Name);
        }

        SpeakerDeviceNames.Clear();
        foreach (var d in _realtimeCall.Speakers)
        {
            SpeakerDeviceNames.Add(d.Name);
        }

        CameraDeviceNames.Clear();
        foreach (var d in _realtimeCall.Cameras)
        {
            CameraDeviceNames.Add(d.Name);
        }

        SelectedMicDeviceIndex = _realtimeCall.SelectedMicIndex;
        SelectedSpeakerDeviceIndex = _realtimeCall.SelectedSpeakerIndex;
        SelectedCameraDeviceIndex = _realtimeCall.SelectedCameraIndex;
    }

    private static void EnqueueUi(Action action)
    {
        var q = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()
                ?? App.Window?.DispatcherQueue;
        if (q is null)
        {
            try
            {
                action();
            }
            catch
            {
                // ignore
            }

            return;
        }

        q.TryEnqueue(() =>
        {
            try
            {
                action();
            }
            catch
            {
                // ignore
            }
        });
    }

    private void SyncCallUiFromService()
    {
        IsInCall = _realtimeCall.IsConnected;
        IsCallConnecting = _realtimeCall.IsConnecting;
        IsCallReconnecting = _realtimeCall.IsReconnecting;
        IsMicOn = _realtimeCall.IsMicEnabled;
        IsCameraOn = _realtimeCall.IsCameraEnabled;
        IsScreenSharing = _realtimeCall.IsScreenShareEnabled;
        IsSidetoneOn = _realtimeCall.IsSidetoneEnabled;
        ConnectionQualityText = _realtimeCall.ConnectionQualityText;
        if (!string.IsNullOrWhiteSpace(_realtimeCall.StatusText))
        {
            CallStatusText = _realtimeCall.StatusText!;
        }

        RemoteVideoBitmap = _realtimeCall.FocusedRemoteVideo;
        LocalVideoBitmap = _realtimeCall.LocalVideo;
    }

    private void SyncCallParticipantsFromMedia()
    {
        var media = _realtimeCall.Participants;
        if (media.Count == 0)
        {
            return;
        }

        CallParticipants.Clear();
        foreach (var p in media)
        {
            var label = p.IsLocal ? $"{p.DisplayName}（我）" : p.DisplayName;
            if (p.HasVideo)
            {
                label += p.IsScreenShare ? " · 屏幕" : " · 视频";
            }

            if (p.HasAudio)
            {
                label += " · 音频";
            }

            if (!string.IsNullOrWhiteSpace(p.QualityText))
            {
                label += " · " + p.QualityText;
            }

            CallParticipants.Add(new CallParticipantItemViewModel(new CallParticipant
            {
                Identity = p.Identity,
                Name = label,
            }));
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
        _ws.StateChanged += OnWsStateChanged;
    }

    private void OnWsStateChanged(object? sender, ChatWsConnectionState state)
    {
        void Apply() => RealtimeModeText = state == ChatWsConnectionState.Connected ? "实时: WebSocket" : "实时: 轮询";
        if (App.DispatcherQueue is { } dq && !dq.HasThreadAccess)
        {
            dq.TryEnqueue(Apply);
        }
        else
        {
            Apply();
        }
    }

    private void OnRoomWsPacket(object? sender, ChatWsPacket packet)
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        // Forward possible call invites when not already in a call
        var t = packet.Type ?? string.Empty;
        if (!IsInCall && !IsCallConnecting
            && (t.Contains("realtime", StringComparison.OrdinalIgnoreCase)
                || (t.Contains("call", StringComparison.OrdinalIgnoreCase)
                    && t.Contains("invite", StringComparison.OrdinalIgnoreCase))))
        {
            try
            {
                _incomingCalls.PresentInvite(new IncomingCallInfo
                {
                    RoomId = RoomId,
                    RoomTitle = RoomTitle,
                    CallerName = "通话邀请",
                });
            }
            catch
            {
                // ignore
            }
        }

        // Solian: messages.new / messages.update carry SnChatMessage in data
        if (!t.StartsWith("messages.", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(t, "system.e2ee.enabled", StringComparison.OrdinalIgnoreCase))
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

                var type = msg.Type ?? t;
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
                _voiceLimitStopRequested = 0;
                IsRecording = true;
                RecordingElapsedText = "0:00";
                UpdateCanSend();
                _toast.Show($"正在录音… 最长 {_voiceRecorder.MaxDuration.TotalSeconds:0} 秒");
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

            using var clip = await _voiceRecorder.StopAsync().ConfigureAwait(true);
            if (clip is null)
            {
                _toast.Show("录音太短或未采集到声音");
                return;
            }

            var sent = await _api.SendVoiceMessageAsync(
                    RoomId,
                    clip.WavStream,
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
            _voiceLimitStopRequested = 0;
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
        _voiceLimitStopRequested = 0;
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
            item.Image = await _imageLoader.LoadSafeAsync(fileId, DysonFileImageLoader.StickerThumbDecodeWidth).ConfigureAwait(true);
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

        TrimStickerFileIdCacheIfNeeded();
    }

    /// <summary>Drop ~half of entries when over cap (no true LRU; bounds process lifetime growth).</summary>
    private static void TrimStickerFileIdCacheIfNeeded()
    {
        var count = StickerFileIdCache.Count;
        if (count <= MaxStickerCacheEntries)
        {
            return;
        }

        var remove = count - (MaxStickerCacheEntries / 2);
        foreach (var k in StickerFileIdCache.Keys)
        {
            if (remove-- <= 0)
            {
                break;
            }

            StickerFileIdCache.TryRemove(k, out _);
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

    private Task LoadBotCommandsAsync()
        => LoadBotCommandsAsync(CancellationToken.None, _loadGeneration, RoomId);

    private async Task LoadBotCommandsAsync(CancellationToken cancellationToken, int generation, Guid roomId)
    {
        if (roomId == Guid.Empty)
        {
            return;
        }

        try
        {
            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, cancellationToken))
                {
                    BotPanelStatusText = "加载机器人命令…";
                }
            }).ConfigureAwait(false);

            var cmds = await _api.GetBotCommandsAsync(roomId, cancellationToken).ConfigureAwait(false);
            if (IsStaleLoad(generation, roomId, cancellationToken))
            {
                return;
            }

            var ordered = cmds
                .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(c => new ChatBotCommandItem(c))
                .ToList();
            var status = cmds.Count == 0
                ? "此房间暂无机器人命令"
                : $"共 {cmds.Count} 条命令 · 点选插入，或输入 / 筛选";

            await RunOnUiAsync(() =>
            {
                if (IsStaleLoad(generation, roomId, cancellationToken))
                {
                    return;
                }

                _botCommands.Clear();
                _botCommands.AddRange(cmds);
                BotCommands.Clear();
                foreach (var c in ordered)
                {
                    BotCommands.Add(c);
                }

                BotPanelStatusText = status;
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // leave room
        }
        catch (Exception ex)
        {
            if (IsStaleLoad(generation, roomId, cancellationToken))
            {
                return;
            }

            var msg = "加载命令失败：" + (ex is SolarApiException se
                ? (se.ApiMessage ?? se.Message)
                : ex.Message);
            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, cancellationToken))
                {
                    BotPanelStatusText = msg;
                }
            }).ConfigureAwait(false);
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
        // Phase 3: local keyset first, then REST offset backfill. First paint / UI trim unchanged.
        if (RoomId == Guid.Empty || IsLoadingMore || IsBusy || !_hasMoreOlder)
        {
            return;
        }

        var roomId = RoomId;
        var generation = _loadGeneration;
        var ct = _loadCts?.Token ?? CancellationToken.None;
        var currentAccountId = _authService.CurrentAccount?.Id;
        var offset = _offset;
        var olderThan = _oldestLoaded;

        try
        {
            IsLoadingMore = true;

            // 1) Local SQLite keyset (ascending page, older than current oldest painted).
            IReadOnlyList<SnChatMessage> localPage = Array.Empty<SnChatMessage>();
            try
            {
                localPage = await _localStore
                    .GetOlderMessagesAsync(roomId, olderThan, PageSize, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatDetail] local LoadMore: {ex.Message}");
            }

            if (IsStaleLoad(generation, roomId, ct))
            {
                return;
            }

            var merged = new List<SnChatMessage>(PageSize * 2);
            if (localPage.Count > 0)
            {
                merged.AddRange(localPage);
            }

            var localFullPage = localPage.Count >= PageSize;
            var apiBatchCount = 0;
            SolarApiException? apiError = null;

            // 2) If local short, backfill from REST (offset/take unchanged contract).
            if (!localFullPage)
            {
                try
                {
                    var batch = await _api
                        .GetMessagesAsync(roomId.ToString(), offset: offset, take: PageSize, ct)
                        .ConfigureAwait(false);

                    if (IsStaleLoad(generation, roomId, ct))
                    {
                        return;
                    }

                    apiBatchCount = batch.Count;
                    if (batch.Count > 0)
                    {
                        var orderedApi = await Task.Run(() => NormalizeOrder(batch), ct).ConfigureAwait(false);

                        // Dual-write into L1 + SQLite (Phase 2); memory remains read authority for hot path.
                        foreach (var msg in orderedApi)
                        {
                            try
                            {
                                _cache.UpsertRoomMessage(roomId, msg);
                            }
                            catch
                            {
                                // dual-write must never break LoadMore
                            }

                            merged.Add(msg);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (SolarApiException ex)
                {
                    apiError = ex;
                }
            }

            if (IsStaleLoad(generation, roomId, ct))
            {
                return;
            }

            // 3) Normalize + paint (dedupe by id; no duplicate bubbles).
            var ordered = await Task.Run(() => NormalizeOrder(merged), ct).ConfigureAwait(false);

            if (IsStaleLoad(generation, roomId, ct))
            {
                return;
            }

            var paintedAny = false;
            await RunOnUiAsync(() =>
            {
                if (IsStaleLoad(generation, roomId, ct))
                {
                    return;
                }

                var older = BuildMessageItems(ordered, currentAccountId);
                var fresh = new List<MessageItemViewModel>(older.Count);
                foreach (var item in older)
                {
                    var id = item.Message.Id;
                    if (id != Guid.Empty && !_knownMessageIds.Add(id))
                    {
                        continue;
                    }

                    // Optimistic / empty-id: also skip if same client_message_id already shown.
                    if (id == Guid.Empty && !string.IsNullOrEmpty(item.Message.ClientMessageId))
                    {
                        var clientId = item.Message.ClientMessageId;
                        var dup = false;
                        for (var i = 0; i < Messages.Count; i++)
                        {
                            if (string.Equals(
                                    Messages[i].Message.ClientMessageId,
                                    clientId,
                                    StringComparison.Ordinal))
                            {
                                dup = true;
                                break;
                            }
                        }

                        if (dup)
                        {
                            continue;
                        }
                    }

                    fresh.Add(item);
                }

                if (fresh.Count > 0)
                {
                    Messages.InsertRange(0, fresh);
                    paintedAny = true;
                    RefreshOldestLoadedCursor();
                    // Phase 5: cap UI — drop newest side so upward history can stay.
                    TrimUiTailIfNeeded();
                }

                if (apiBatchCount > 0)
                {
                    _offset += apiBatchCount;
                }

                // More older pages?
                // - local returned a full page → more local likely remains
                // - API returned a full page → more server history likely remains
                // - both short / empty and no network error with empty local → exhausted
                if (localFullPage || apiBatchCount >= PageSize)
                {
                    _hasMoreOlder = true;
                }
                else if (apiError is not null && merged.Count == 0)
                {
                    // Offline / error and nothing to show: keep flag so user can retry when online.
                    _hasMoreOlder = true;
                }
                else if (apiError is not null && paintedAny)
                {
                    // Painted local history offline; allow retry for server hole-fill later.
                    _hasMoreOlder = localFullPage || apiBatchCount >= PageSize || localPage.Count > 0;
                    // If local was short and we could not hit API, assume more may exist on server.
                    if (!localFullPage)
                    {
                        _hasMoreOlder = true;
                    }
                }
                else
                {
                    _hasMoreOlder = false;
                }

                _hasMore = _hasMoreOlder;
                PersistMessagesToCache();
                if (paintedAny)
                {
                    OlderMessagesLoaded?.Invoke(this, EventArgs.Empty);
                }

                // Only surface API error when we painted nothing (no offline history).
                if (apiError is not null && !paintedAny && merged.Count == 0)
                {
                    ErrorMessage = apiError.ApiMessage ?? apiError.Message;
                }
            }).ConfigureAwait(false);

            if (paintedAny)
            {
                _ = LoadMediaAsync(ct, generation, roomId);
            }
        }
        catch (OperationCanceledException)
        {
            // left room / cancelled
        }
        catch (SolarApiException ex)
        {
            if (!IsStaleLoad(generation, roomId, ct))
            {
                await RunOnUiAsync(() =>
                {
                    if (!IsStaleLoad(generation, roomId, ct))
                    {
                        ErrorMessage = ex.Message;
                    }
                }).ConfigureAwait(false);
            }
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                await RunOnUiAsync(() =>
                {
                    if (generation == _loadGeneration)
                    {
                        IsLoadingMore = false;
                    }
                }).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Sync <see cref="_oldestLoaded"/> from the oldest painted bubble (Messages[0]).</summary>
    private void RefreshOldestLoadedCursor()
    {
        if (Messages.Count == 0)
        {
            _oldestLoaded = null;
            return;
        }

        var first = Messages[0].Message;
        _oldestLoaded = new MessageKeysetCursor(first.RoomSequence, first.Id);
    }

    /// <summary>
    /// Phase 5: when UI exceeds <see cref="MaxUiMessages"/> after LoadMore, drop
    /// <see cref="UiTrimBatch"/> from the newest side and record a detached cursor.
    /// </summary>
    private void TrimUiTailIfNeeded()
    {
        while (Messages.Count > MaxUiMessages)
        {
            // Spec: 超 200 时从尾部裁 30（可略低于 200，减少频繁小裁切）.
            var drop = Math.Min(UiTrimBatch, Messages.Count);
            if (drop <= 0 || drop >= Messages.Count)
            {
                break;
            }

            // Cursor of the last message that remains = boundary for GetNewerMessages.
            var keepLastIndex = Messages.Count - drop - 1;
            if (keepLastIndex >= 0)
            {
                var boundary = Messages[keepLastIndex].Message;
                _detachedAfter = new MessageKeysetCursor(boundary.RoomSequence, boundary.Id);
                _hasDetachedNewer = true;
            }

            for (var i = Messages.Count - drop; i < Messages.Count; i++)
            {
                var id = Messages[i].Message.Id;
                if (id != Guid.Empty)
                {
                    _knownMessageIds.Remove(id);
                }
            }

            Messages.RemoveRange(Messages.Count - drop, drop);
        }
    }

    /// <summary>
    /// After rehydrating the tail, drop oldest bubbles so the window stays at MaxUiMessages
    /// without re-detaching the newest side.
    /// </summary>
    private void TrimUiHeadIfNeeded()
    {
        while (Messages.Count > MaxUiMessages)
        {
            var drop = Math.Min(UiTrimBatch, Messages.Count - MaxUiMessages);
            if (drop <= 0)
            {
                break;
            }

            for (var i = 0; i < drop; i++)
            {
                var id = Messages[i].Message.Id;
                if (id != Guid.Empty)
                {
                    _knownMessageIds.Remove(id);
                }
            }

            Messages.RemoveRange(0, drop);
            RefreshOldestLoadedCursor();
            _hasMoreOlder = true;
            _hasMore = true;
        }
    }

    /// <summary>
    /// Phase 5: restore messages newer than <see cref="_detachedAfter"/> from SQLite (scroll-to-bottom).
    /// </summary>
    [RelayCommand]
    private async Task RehydrateDetachedTailAsync()
    {
        if (RoomId == Guid.Empty || !_hasDetachedNewer || _isRehydratingTail)
        {
            return;
        }

        var roomId = RoomId;
        var generation = _loadGeneration;
        var ct = _loadCts?.Token ?? CancellationToken.None;
        var currentAccountId = _authService.CurrentAccount?.Id;
        var after = _detachedAfter;

        _isRehydratingTail = true;
        try
        {
            // Pull enough to catch up; loop while full pages keep coming.
            var safety = 0;
            while (_hasDetachedNewer && safety++ < 20)
            {
                if (IsStaleLoad(generation, roomId, ct))
                {
                    return;
                }

                IReadOnlyList<SnChatMessage> newer;
                try
                {
                    newer = await _localStore
                        .GetNewerMessagesAsync(roomId, after, PageSize, ct)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ChatDetail] tail rehydrate: {ex.Message}");
                    break;
                }

                if (IsStaleLoad(generation, roomId, ct))
                {
                    return;
                }

                if (newer.Count == 0)
                {
                    await RunOnUiAsync(() =>
                    {
                        if (!IsStaleLoad(generation, roomId, ct))
                        {
                            _hasDetachedNewer = false;
                            _detachedAfter = null;
                        }
                    }).ConfigureAwait(false);
                    break;
                }

                var ordered = await Task.Run(
                        () => NormalizeOrder(newer as List<SnChatMessage> ?? newer.ToList()),
                        ct)
                    .ConfigureAwait(false);

                await RunOnUiAsync(() =>
                {
                    if (IsStaleLoad(generation, roomId, ct))
                    {
                        return;
                    }

                    var items = BuildMessageItems(ordered, currentAccountId);
                    var fresh = new List<MessageItemViewModel>(items.Count);
                    foreach (var item in items)
                    {
                        var id = item.Message.Id;
                        if (id != Guid.Empty && !_knownMessageIds.Add(id))
                        {
                            continue;
                        }

                        fresh.Add(item);
                    }

                    if (fresh.Count > 0)
                    {
                        Messages.AddRange(fresh);
                        var last = Messages[^1].Message;
                        after = new MessageKeysetCursor(last.RoomSequence, last.Id);
                        _detachedAfter = after;
                        TrimUiHeadIfNeeded();
                    }

                    if (newer.Count < PageSize)
                    {
                        _hasDetachedNewer = false;
                        _detachedAfter = null;
                    }
                }).ConfigureAwait(false);

                if (newer.Count < PageSize)
                {
                    break;
                }
            }

            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, ct) && Messages.Count > 0)
                {
                    ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
                }
            }).ConfigureAwait(false);

            _ = LoadMediaAsync(ct, generation, roomId);
        }
        catch (OperationCanceledException)
        {
            // leave room
        }
        finally
        {
            _isRehydratingTail = false;
        }
    }

    /// <summary>
    /// Called when the user scrolls near the bottom or ScrollToBottom is requested while detached.
    /// </summary>
    public void NotifyNearBottom()
    {
        if (_hasDetachedNewer && RehydrateDetachedTailCommand.CanExecute(null))
        {
            RehydrateDetachedTailCommand.Execute(null);
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
        // Only stop the poll loop — do not cancel message load (StartPolling runs
        // right after LoadInitial on navigate-to).
        if (_syncCts is not null)
        {
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

        _syncCts = new CancellationTokenSource();
        _ = PollLoopAsync(_syncCts.Token);
    }

    /// <summary>Cancel in-flight message loads when leaving the detail page.</summary>
    public void CancelPendingLoads() => CancelMessageLoad();

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

        if (IsInCall || IsCallConnecting)
        {
            _ = HangUpInternalAsync(showToast: false);
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
                await Task.Delay(SyncInterval, cancellationToken).ConfigureAwait(false);
                await SyncOnceAsync(cancellationToken).ConfigureAwait(false);
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

        var roomId = RoomId;
        var request = new SyncRequest
        {
            LastSyncTimestamp = _lastSyncTimestamp,
            LastSyncMessageId = _lastSyncMessageId,
        };

        var response = await _api.SyncRoomMessagesAsync(roomId.ToString(), request, cancellationToken)
            .ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested || roomId != RoomId)
        {
            return;
        }

        var incoming = response.Messages ?? [];
        // Pure data sort only — BitmapImage/WinRT VMs must be created on the UI thread.
        var ordered = incoming.Count <= 1
            ? incoming
            : await Task.Run(() => NormalizeOrder(incoming), cancellationToken).ConfigureAwait(false);
        var currentAccountId = _authService.CurrentAccount?.Id;

        if (cancellationToken.IsCancellationRequested || roomId != RoomId)
        {
            return;
        }

        var added = false;
        await RunOnUiAsync(() =>
        {
            if (cancellationToken.IsCancellationRequested || roomId != RoomId)
            {
                return;
            }

            var prebuilt = BuildMessageItems(ordered, currentAccountId);
            foreach (var item in prebuilt)
            {
                if (TryAdoptPreparedMessage(item, append: true))
                {
                    added = true;
                }
            }

            if (response.CurrentTimestamp is { } serverTs)
            {
                _lastSyncTimestamp = serverTs.ToUnixTimeMilliseconds();
                _cache.UpdateRoomSyncCursor(roomId, _lastSyncTimestamp, _lastSyncMessageId);
            }
            else if (incoming.Count > 0)
            {
                UpdateSyncCursorFromMessages();
            }

            if (added)
            {
                ScrollToBottomRequested?.Invoke(this, EventArgs.Empty);
            }
        }).ConfigureAwait(false);

        if (added)
        {
            _ = LoadMediaAsync(cancellationToken, _loadGeneration, roomId);
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

        // Never dump the entire historical cache onto the UI — keep one page.
        var source = entry.Messages;
        IReadOnlyList<SnChatMessage> window = source.Count <= PageSize
            ? source
            : source.Skip(Math.Max(0, source.Count - PageSize)).ToList();

        var currentId = _authService.CurrentAccount?.Id;
        var items = BuildMessageItems(window, currentId);
        ApplyMessagePage(
            items,
            paintedCount: window.Count,
            hasMore: entry.HasMore || source.Count > PageSize,
            replace: true);

        // Preserve server-side pagination cursor from cache when available.
        _offset = entry.Offset > 0 ? entry.Offset : Math.Max(window.Count, source.Count);
        _lastSyncTimestamp = entry.LastSyncTimestamp;
        _lastSyncMessageId = entry.LastSyncMessageId;
        return true;
    }

    /// <summary>
    /// Must run on the UI thread: MessageItemViewModel may assign cached BitmapImage
    /// (WinRT apartment affinity) and bindable ObservableObject properties.
    /// </summary>
    private List<MessageItemViewModel> BuildMessageItems(
        IReadOnlyList<SnChatMessage> messages,
        Guid? currentAccountId)
    {
        var items = new List<MessageItemViewModel>(messages.Count);
        var loader = _imageLoader;
        foreach (var msg in messages)
        {
            items.Add(new MessageItemViewModel(msg, currentAccountId, loader));
        }

        return items;
    }

    /// <summary>Apply a prepared page of messages with a single collection Reset.</summary>
    private void ApplyMessagePage(
        IReadOnlyList<MessageItemViewModel> items,
        int paintedCount,
        bool hasMore,
        bool replace)
    {
        _knownMessageIds.Clear();
        foreach (var item in items)
        {
            if (item.Message.Id != Guid.Empty)
            {
                _knownMessageIds.Add(item.Message.Id);
            }
        }

        if (replace)
        {
            Messages.ReplaceAll(items);
        }
        else
        {
            Messages.AddRange(items);
        }

        _offset = paintedCount;
        _hasMore = hasMore;
        _hasMoreOlder = hasMore;
        UpdateSyncCursorFromMessages();
        RefreshOldestLoadedCursor();
    }

    /// <summary>
    /// Adopt a pre-built bubble into the live collection (dedupe / echo reconcile).
    /// Must run on the UI thread.
    /// </summary>
    private bool TryAdoptPreparedMessage(MessageItemViewModel item, bool append)
    {
        var message = item.Message;

        // Phase 5: UI tail was trimmed — keep L2 warm but do not append into a gap.
        // Scroll-to-bottom rehydrate will paint this message with the rest of the tail.
        if (append && _hasDetachedNewer)
        {
            _cache.UpsertRoomMessage(RoomId, message);
            return true;
        }

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

                    Messages[i] = item;
                    _cache.UpsertRoomMessage(RoomId, message);
                    return true;
                }
            }
        }

        if (message.Id != Guid.Empty && !_knownMessageIds.Add(message.Id))
        {
            return false;
        }

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

    private static Task RunOnUiAsync(
        Action action,
        Microsoft.UI.Dispatching.DispatcherQueuePriority priority =
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal)
    {
        var dq = App.DispatcherQueue;
        if (dq is null || dq.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dq.TryEnqueue(priority, () =>
            {
                try
                {
                    action();
                    tcs.TrySetResult();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
        {
            tcs.TrySetCanceled();
        }

        return tcs.Task;
    }

    /// <summary>
    /// Phase 7: refresh L1 window only (no full-list SQLite dual-write).
    /// Rows already dual-written via <see cref="IChatDataCache.UpsertRoomMessage"/>.
    /// </summary>
    private void PersistMessagesToCache()
    {
        if (RoomId == Guid.Empty)
        {
            return;
        }

        var list = Messages.Select(m => m.Message).ToList();
        // L1 snapshot for instant re-entry; SQLite is updated incrementally elsewhere.
        _cache.SetRoomMessages(
            RoomId,
            list,
            _lastSyncTimestamp,
            _lastSyncMessageId,
            _hasMore,
            _offset);
    }

    private bool _mediaLoading;

    /// <summary>Bound parallel image downloads/decodes per room load (peak memory guard).</summary>
    private const int MediaLoadConcurrency = 6;

    private readonly SemaphoreSlim _mediaGate = new(MediaLoadConcurrency, MediaLoadConcurrency);

    private async Task<BitmapImage?> LoadMediaThrottledAsync(
        Func<Task<BitmapImage?>> start,
        CancellationToken cancellationToken)
    {
        await _mediaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await start().ConfigureAwait(false);
        }
        finally
        {
            _mediaGate.Release();
        }
    }

    private Task LoadMediaAsync()
        => LoadMediaAsync(CancellationToken.None, _loadGeneration, RoomId);

    private async Task LoadMediaAsync(CancellationToken cancellationToken, int generation, Guid roomId)
    {
        // Single-flight: send/poll/load-more can all trigger this concurrently.
        if (_mediaLoading)
        {
            return;
        }

        _mediaLoading = true;
        try
        {
            if (IsStaleLoad(generation, roomId, cancellationToken))
            {
                return;
            }

            // Resolve :prefix+slug: stickers → file ids before downloading bitmaps.
            await ResolveStickerFileIdsAsync(cancellationToken, generation, roomId).ConfigureAwait(false);

            if (IsStaleLoad(generation, roomId, cancellationToken))
            {
                return;
            }

            List<MessageItemViewModel> snapshot = [];
            await RunOnUiAsync(() =>
            {
                if (!IsStaleLoad(generation, roomId, cancellationToken))
                {
                    snapshot = Messages.ToList();
                }
            }).ConfigureAwait(false);

            if (snapshot.Count == 0 || IsStaleLoad(generation, roomId, cancellationToken))
            {
                return;
            }

            var avatarTasks = new List<(MessageItemViewModel Msg, Task<BitmapImage?> Task)>();
            var attachmentTasks = new List<(MessageAttachmentViewModel Att, Task<BitmapImage?> Task)>();
            var stickerTasks = new List<(MessageStickerViewModel Sticker, Task<BitmapImage?> Task)>();

            await RunOnUiAsync(() =>
            {
                if (IsStaleLoad(generation, roomId, cancellationToken))
                {
                    return;
                }

                foreach (var msg in snapshot)
                {
                    if (!msg.AvatarAuthenticated && !string.IsNullOrWhiteSpace(msg.AvatarUrl))
                    {
                        avatarTasks.Add((msg, LoadMediaThrottledAsync(
                            () => _imageLoader.LoadSafeAsync(msg.AvatarUrl, DysonFileImageLoader.AvatarDecodeWidth, cancellationToken),
                            cancellationToken)));
                    }

                    foreach (var att in msg.Attachments.Where(a => a.IsImage && a.Image is null))
                    {
                        var key = att.FileId ?? att.Url;
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            continue;
                        }

                        att.IsLoading = true;
                        attachmentTasks.Add((att, LoadMediaThrottledAsync(
                            () => _imageLoader.LoadSafeAsync(key, DysonFileImageLoader.ChatImageDecodeWidth, cancellationToken),
                            cancellationToken)));
                    }

                    foreach (var sticker in msg.Stickers.Where(s => s.Image is null && !string.IsNullOrWhiteSpace(s.FileId)))
                    {
                        sticker.IsLoading = true;
                        stickerTasks.Add((sticker, LoadMediaThrottledAsync(
                            () => _imageLoader.LoadSafeAsync(sticker.FileId, DysonFileImageLoader.StickerDecodeWidth, cancellationToken),
                            cancellationToken)));
                    }
                }
            }).ConfigureAwait(false);

            if (avatarTasks.Count == 0 && attachmentTasks.Count == 0 && stickerTasks.Count == 0)
            {
                return;
            }

            // Downloads run on thread pool; BitmapImage creation is low-priority UI work inside the loader.
            await Task.WhenAll(
                avatarTasks.Select(t => t.Task)
                    .Concat(attachmentTasks.Select(t => t.Task))
                    .Concat(stickerTasks.Select(t => t.Task))).ConfigureAwait(false);

            if (IsStaleLoad(generation, roomId, cancellationToken))
            {
                return;
            }

            // Apply results in small UI batches so title-bar / drag keep processing.
            const int batchSize = 6;
            for (var i = 0; i < avatarTasks.Count; i += batchSize)
            {
                if (IsStaleLoad(generation, roomId, cancellationToken))
                {
                    return;
                }

                var slice = avatarTasks.Skip(i).Take(batchSize).ToList();
                await RunOnUiAsync(() =>
                {
                    if (IsStaleLoad(generation, roomId, cancellationToken))
                    {
                        return;
                    }

                    foreach (var (msg, task) in slice)
                    {
                        if (task.IsCompletedSuccessfully && task.Result is { } bmp)
                        {
                            msg.SetAuthenticatedAvatar(bmp);
                        }
                    }
                }, Microsoft.UI.Dispatching.DispatcherQueuePriority.Low).ConfigureAwait(false);
            }

            for (var i = 0; i < attachmentTasks.Count; i += batchSize)
            {
                if (IsStaleLoad(generation, roomId, cancellationToken))
                {
                    return;
                }

                var slice = attachmentTasks.Skip(i).Take(batchSize).ToList();
                await RunOnUiAsync(() =>
                {
                    if (IsStaleLoad(generation, roomId, cancellationToken))
                    {
                        return;
                    }

                    foreach (var (att, task) in slice)
                    {
                        if (task.IsCompletedSuccessfully && task.Result is { } bmp)
                        {
                            att.Image = bmp;
                            att.ImageOpacity = 1.0;
                        }

                        att.IsLoading = false;
                    }
                }, Microsoft.UI.Dispatching.DispatcherQueuePriority.Low).ConfigureAwait(false);
            }

            for (var i = 0; i < stickerTasks.Count; i += batchSize)
            {
                if (IsStaleLoad(generation, roomId, cancellationToken))
                {
                    return;
                }

                var slice = stickerTasks.Skip(i).Take(batchSize).ToList();
                await RunOnUiAsync(() =>
                {
                    if (IsStaleLoad(generation, roomId, cancellationToken))
                    {
                        return;
                    }

                    foreach (var (sticker, task) in slice)
                    {
                        if (task.IsCompletedSuccessfully && task.Result is { } bmp)
                        {
                            sticker.Image = bmp;
                            sticker.ImageOpacity = 1.0;
                        }

                        sticker.IsLoading = false;
                    }
                }, Microsoft.UI.Dispatching.DispatcherQueuePriority.Low).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // leave room
        }
        finally
        {
            _mediaLoading = false;
        }
    }

    /// <summary>
    /// Fill sticker slots with DysonFS file ids from cache / owned packs / API batch lookup.
    /// </summary>
    private Task ResolveStickerFileIdsAsync()
        => ResolveStickerFileIdsAsync(CancellationToken.None, _loadGeneration, RoomId);

    private async Task ResolveStickerFileIdsAsync(
        CancellationToken cancellationToken,
        int generation,
        Guid roomId)
    {
        var pending = new List<(MessageItemViewModel Msg, string Placeholder)>();
        await RunOnUiAsync(() =>
        {
            if (IsStaleLoad(generation, roomId, cancellationToken))
            {
                return;
            }

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
        }).ConfigureAwait(false);

        if (pending.Count == 0 || IsStaleLoad(generation, roomId, cancellationToken))
        {
            return;
        }

        try
        {
            var placeholders = pending.Select(p => p.Placeholder).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var hits = await _api.LookupStickersBatchAsync(placeholders, cancellationToken).ConfigureAwait(false);
            if (IsStaleLoad(generation, roomId, cancellationToken))
            {
                return;
            }

            await RunOnUiAsync(() =>
            {
                if (IsStaleLoad(generation, roomId, cancellationToken))
                {
                    return;
                }

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
            }).ConfigureAwait(false);

            // Any remaining: single lookup (batch may omit unmatched)
            foreach (var (msg, ph) in pending)
            {
                if (IsStaleLoad(generation, roomId, cancellationToken))
                {
                    return;
                }

                var needsLookup = false;
                await RunOnUiAsync(() =>
                {
                    if (IsStaleLoad(generation, roomId, cancellationToken))
                    {
                        return;
                    }

                    var slot = msg.Stickers.FirstOrDefault(s =>
                        string.Equals(s.Placeholder, ph, StringComparison.OrdinalIgnoreCase));
                    if (slot is { FileId: { Length: > 0 } })
                    {
                        return;
                    }

                    if (TryGetCachedStickerFileId(ph, out var cached2) && !string.IsNullOrWhiteSpace(cached2))
                    {
                        msg.EnsureStickerSlot(ph, cached2, msg.IsStickerOnly);
                        return;
                    }

                    needsLookup = true;
                }).ConfigureAwait(false);

                if (!needsLookup)
                {
                    continue;
                }

                var sticker = await _api.LookupStickerAsync(ph, cancellationToken).ConfigureAwait(false);
                if (sticker is null || IsStaleLoad(generation, roomId, cancellationToken))
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
                await RunOnUiAsync(() =>
                {
                    if (!IsStaleLoad(generation, roomId, cancellationToken))
                    {
                        msg.EnsureStickerSlot(ph, fileId, msg.IsStickerOnly || msg.StickerPlaceholders.Count <= 1);
                    }
                }).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // leave room
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
