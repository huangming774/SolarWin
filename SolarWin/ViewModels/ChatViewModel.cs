using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>
/// Chat room list UI (singleton lifetime).
/// API payloads are held by <see cref="IChatDataCache"/>; this VM projects them to the list.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private readonly ISolarApiClient _api;
    private readonly IAuthService _authService;
    private readonly IToastService _toast;
    private readonly IChatWebSocketService _ws;
    private readonly IChatMessageNotifier _messageNotifier;
    private readonly IChatDataCache _cache;
    private readonly DysonFileImageLoader _imageLoader;

    private long _roomsSyncTimestamp;
    private bool _avatarsLoaded;
    private int _loadGeneration;
    private bool _wsHooked;
    private CancellationTokenSource? _userSearchCts;

    public ChatViewModel(
        ISolarApiClient api,
        IAuthService authService,
        IToastService toast,
        IChatWebSocketService ws,
        IChatMessageNotifier messageNotifier,
        IChatDataCache cache,
        DysonFileImageLoader imageLoader)
    {
        _api = api;
        _authService = authService;
        _toast = toast;
        _ws = ws;
        _messageNotifier = messageNotifier;
        _cache = cache;
        _imageLoader = imageLoader;
        _authService.AuthenticationStateChanged += OnAuthStateChanged;
        BindCacheToAccount();
    }

    private void OnAuthStateChanged(object? sender, EventArgs e)
    {
        BindCacheToAccount();
        if (!_authService.IsAuthenticated)
        {
            Rooms.Clear();
            PendingInvites.Clear();
            Groups.Clear();
            TotalUnreadCount = 0;
            PendingInviteCount = 0;
            _avatarsLoaded = false;
        }
    }

    private void BindCacheToAccount()
    {
        _cache.BindAccount(_authService.CurrentAccount?.Id);
    }

    public ObservableCollection<ChatRoomListItem> Rooms { get; } = [];

    public ObservableCollection<ChatRoomListItem> PendingInvites { get; } = [];

    public ObservableCollection<ChatGroupItemViewModel> Groups { get; } = [];

    public ObservableCollection<UserSearchResultItem> UserSearchResults { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UserSearchPanelVisibility))]
    public partial bool IsSearchingUsers { get; set; }

    [ObservableProperty]
    public partial string UserSearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UserSearchPanelVisibility))]
    public partial bool HasUserSearchResults { get; set; }

    [ObservableProperty]
    public partial string UserSearchHintText { get; set; } = "输入用户名实时搜索，点选一键私聊";

    public Microsoft.UI.Xaml.Visibility UserSearchPanelVisibility =>
        HasUserSearchResults || IsSearchingUsers
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    partial void OnUserSearchQueryChanged(string value)
    {
        _ = DebouncedSearchUsersAsync(value);
    }

    private async Task DebouncedSearchUsersAsync(string? query)
    {
        _userSearchCts?.Cancel();
        _userSearchCts = new CancellationTokenSource();
        var token = _userSearchCts.Token;
        var q = query?.Trim() ?? string.Empty;

        if (q.Length == 0)
        {
            UserSearchResults.Clear();
            HasUserSearchResults = false;
            UserSearchHintText = "输入用户名实时搜索，点选一键私聊";
            return;
        }

        if (q.Length < 2 && !Guid.TryParse(q, out _))
        {
            UserSearchHintText = "至少输入 2 个字符…";
            return;
        }

        try
        {
            await Task.Delay(280, token).ConfigureAwait(true);
            if (token.IsCancellationRequested)
            {
                return;
            }

            // Silent search (no toast spam while typing)
            await SearchUsersCoreAsync(q, showEmptyToast: false).ConfigureAwait(true);
            UserSearchHintText = HasUserSearchResults
                ? $"找到 {UserSearchResults.Count} 人 · 点选私聊"
                : "没有匹配用户";
        }
        catch (OperationCanceledException)
        {
            // debounce
        }
    }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalUnreadCount { get; set; }

    [ObservableProperty]
    public partial string NewGroupName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DirectUserIdText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RealmSlugText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedGroupIdText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MoveRoomIdText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WsStatusText { get; set; } = "WS: 未连接";

    [ObservableProperty]
    public partial string SyncStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingInvites))]
    public partial int PendingInviteCount { get; set; }

    public bool HasPendingInvites => PendingInviteCount > 0;

    public bool HasCache => Rooms.Count > 0 || _cache.TryGetRooms(out _);

    public bool IsCacheFresh => _cache.IsRoomsFresh() && Rooms.Count > 0;

    public event EventHandler<ChatRoomListItem>? RoomSelected;

    /// <summary>
    /// Load list. Uses cache when available; only clears UI on full reload.
    /// </summary>
    public void EnsureRealtimeStarted()
    {
        if (!_wsHooked)
        {
            _wsHooked = true;
            _ws.StateChanged += OnWsStateChanged;
            _ws.PacketReceived += OnWsPacket;
        }

        _messageNotifier.Start();
        _ = ConnectWsAsync();
    }

    private async Task ConnectWsAsync()
    {
        try
        {
            await _ws.ConnectAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            WsStatusText = $"WS: 失败 {ex.Message}";
        }
    }

    private void OnWsStateChanged(object? sender, ChatWsConnectionState state)
    {
        void Apply()
        {
            WsStatusText = state switch
            {
                ChatWsConnectionState.Connected => "WS: 已连接",
                ChatWsConnectionState.Connecting => "WS: 连接中…",
                ChatWsConnectionState.Error => "WS: 错误",
                _ => "WS: 未连接",
            };
        }

        if (App.DispatcherQueue is { } dq && !dq.HasThreadAccess)
        {
            dq.TryEnqueue(Apply);
            return;
        }

        Apply();
    }

    private void OnWsPacket(object? sender, ChatWsPacket packet)
    {
        // List-level: any message traffic → soft refresh room list
        if (packet.Type.StartsWith("messages.", StringComparison.OrdinalIgnoreCase) ||
            packet.Type.StartsWith("chat.", StringComparison.OrdinalIgnoreCase))
        {
            if (App.DispatcherQueue is { } dq && !dq.HasThreadAccess)
            {
                dq.TryEnqueue(() => _ = RefreshAsync(silent: true));
                return;
            }

            _ = RefreshAsync(silent: true);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        EnsureRealtimeStarted();
        BindCacheToAccount();

        // Project singleton cache → UI before network.
        ApplyRoomsFromCacheIfEmpty();

        // Soft entry: show cache immediately, refresh in background if stale.
        if (IsCacheFresh)
        {
            RefreshStatusText();
            StatusText += "（缓存）";
            return;
        }

        if (HasCache)
        {
            await RefreshAsync(silent: true).ConfigureAwait(true);
            return;
        }

        await RefreshAsync(silent: false).ConfigureAwait(true);
    }

    /// <summary>Fill list from IChatDataCache / disk without hitting the network.</summary>
    private void ApplyRoomsFromCacheIfEmpty()
    {
        if (Rooms.Count > 0)
        {
            return;
        }

        if (!_cache.TryGetRooms(out var rooms) || rooms.Count == 0)
        {
            if (!_cache.TryHydrateRoomsFromDisk() || !_cache.TryGetRooms(out rooms) || rooms.Count == 0)
            {
                return;
            }
        }

        _cache.TryGetSummary(out var summary);
        var meId = _authService.CurrentAccount?.Id;
        var dict = summary as Dictionary<string, ChatSummaryResponse>
                   ?? summary.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        MergeRooms(BuildItems(rooms.ToList(), dict.Count > 0 ? dict : null, meId));

        if (_cache.TotalUnread is { } unread)
        {
            TotalUnreadCount = unread;
        }

        if (_cache.TryGetInvites(out var invites) && invites.Count > 0)
        {
            PendingInvites.Clear();
            foreach (var room in invites)
            {
                PendingInvites.Add(new ChatRoomListItem(room, null, _imageLoader, meId));
            }

            PendingInviteCount = PendingInvites.Count;
        }

        RefreshStatusText();
    }

    [RelayCommand]
    private async Task ForceRefreshAsync()
        => await RefreshAsync(silent: false).ConfigureAwait(true);

    private async Task RefreshAsync(bool silent)
    {
        var gen = ++_loadGeneration;
        try
        {
            if (silent)
            {
                IsRefreshing = true;
            }
            else
            {
                IsBusy = true;
                ErrorMessage = null;
            }

            BindCacheToAccount();
            var meId = _authService.CurrentAccount?.Id;

            List<SnChatRoom> rooms;
            try
            {
                rooms = await _api.GetChatRoomsAsync().ConfigureAwait(true);
                _cache.SetRooms(rooms, persistDisk: true);
            }
            catch (SolarApiException ex)
            {
                rooms = [];
                if (_cache.TryGetRooms(out var mem) && mem.Count > 0)
                {
                    rooms = mem.ToList();
                    if (!silent)
                    {
                        StatusText = "内存缓存会话列表";
                    }
                }
                else if (_cache.TryHydrateRoomsFromDisk() && _cache.TryGetRooms(out var disk) && disk.Count > 0)
                {
                    rooms = disk.ToList();
                    if (!silent)
                    {
                        StatusText = "离线缓存会话列表";
                    }
                }
                else if (!HasCache)
                {
                    ErrorMessage = $"会话列表：{ex.ApiMessage ?? ex.Message}";
                }
            }

            if (gen != _loadGeneration)
            {
                return;
            }

            Dictionary<string, ChatSummaryResponse>? summary = null;
            try
            {
                summary = await _api.GetChatSummaryAsync().ConfigureAwait(true);
                if (summary is not null)
                {
                    _cache.SetSummary(summary);
                }
            }
            catch (SolarApiException)
            {
                if (_cache.TryGetSummary(out var cachedSummary) && cachedSummary.Count > 0)
                {
                    summary = cachedSummary as Dictionary<string, ChatSummaryResponse>
                              ?? cachedSummary.ToDictionary(
                                  kv => kv.Key,
                                  kv => kv.Value,
                                  StringComparer.OrdinalIgnoreCase);
                }
            }

            if (gen != _loadGeneration)
            {
                return;
            }

            var next = BuildItems(rooms, summary, meId);

            // Diff-update to preserve avatar bitmaps when possible
            MergeRooms(next);

            await RefreshUnreadAndInvitesAsync(gen).ConfigureAwait(true);
            await RefreshGroupsAsync(gen).ConfigureAwait(true);
            await ApplyRoomsSyncAsync(gen).ConfigureAwait(true);

            RefreshStatusText();
            if (Rooms.Count > 0
                && ErrorMessage is not null
                && ErrorMessage.StartsWith("会话列表：", StringComparison.Ordinal))
            {
                ErrorMessage = null;
            }
            else if (Rooms.Count == 0 && ErrorMessage is null)
            {
                StatusText = "暂无会话";
            }

            if (!_avatarsLoaded || next.Any(n => n.HasAvatar && n.AvatarImage is null))
            {
                _ = LoadAvatarsAuthenticatedAsync();
            }
        }
        catch (Exception ex)
        {
            if (!HasCache)
            {
                ErrorMessage = ex.Message;
                StatusText = "加载失败";
            }
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    /// <summary>Mark room read on server + local badge clear.</summary>
    public async Task MarkRoomReadAsync(Guid roomId)
    {
        // Local first for snappy UI
        MarkRoomReadLocal(roomId);

        try
        {
            await _api.MarkChatRoomReadAsync(roomId.ToString()).ConfigureAwait(true);
        }
        catch
        {
            // ignore network; local already cleared
        }

        RefreshStatusText();
    }

    public void MarkRoomReadLocal(Guid roomId)
    {
        var item = Rooms.FirstOrDefault(r => r.RoomId == roomId);
        item?.MarkReadLocal();
        TotalUnreadCount = Rooms.Sum(r => r.UnreadCount);
        RefreshStatusText();
    }

    [RelayCommand]
    private void OpenRoom(ChatRoomListItem? item)
    {
        if (item is null)
        {
            return;
        }

        // Clear unread immediately when entering
        item.MarkReadLocal();
        RoomSelected?.Invoke(this, item);
        _ = MarkRoomReadAsync(item.RoomId);
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        try
        {
            await _api.MarkAllChatRoomsReadAsync().ConfigureAwait(true);
            foreach (var room in Rooms)
            {
                room.MarkReadLocal();
            }

            TotalUnreadCount = 0;
            RefreshStatusText();
            _toast.Success("已全部标为已读");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"全部已读失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CreateGroupRoomAsync()
    {
        var name = NewGroupName.Trim();
        if (name.Length == 0)
        {
            _toast.Show("请输入群名称");
            return;
        }

        try
        {
            IsBusy = true;
            var room = await _api.CreateChatRoomAsync(new ChatRoomRequest
            {
                Name = name,
                IsPublic = false,
            }).ConfigureAwait(true);

            NewGroupName = string.Empty;
            Rooms.Insert(0, new ChatRoomListItem(room, null, _imageLoader, _authService.CurrentAccount?.Id));
            // Keep singleton API cache in sync with UI
            if (_cache.TryGetRooms(out var cachedRooms))
            {
                var merged = new List<SnChatRoom> { room };
                merged.AddRange(cachedRooms.Where(r => r.Id != room.Id));
                _cache.SetRooms(merged);
            }
            else
            {
                _cache.SetRooms([room]);
            }

            RefreshStatusText();
            _toast.Success("群聊已创建");
            OpenRoom(Rooms[0]);
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"创建失败：{ex.ApiMessage ?? ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchUsersAsync()
    {
        var q = UserSearchQuery.Trim();
        if (q.Length == 0)
        {
            UserSearchResults.Clear();
            HasUserSearchResults = false;
            return;
        }

        // UUID pasted → no need to search; fill direct id box.
        if (Guid.TryParse(q, out var pastedId))
        {
            DirectUserIdText = pastedId.ToString("D");
            await OpenDirectChatWithUserIdAsync(pastedId).ConfigureAwait(true);
            return;
        }

        await SearchUsersCoreAsync(q, showEmptyToast: true).ConfigureAwait(true);
    }

    private async Task SearchUsersCoreAsync(string q, bool showEmptyToast)
    {
        try
        {
            IsSearchingUsers = true;
            var list = await _api.SearchAccountsAsync(q, take: 20).ConfigureAwait(true);
            UserSearchResults.Clear();
            foreach (var acc in list)
            {
                if (acc.Id == Guid.Empty)
                {
                    continue;
                }

                // Skip self
                if (_authService.CurrentAccount?.Id is { } me && acc.Id == me)
                {
                    continue;
                }

                UserSearchResults.Add(new UserSearchResultItem(acc, _imageLoader));
            }

            HasUserSearchResults = UserSearchResults.Count > 0;
            if (!HasUserSearchResults)
            {
                if (showEmptyToast)
                {
                    _toast.Show("没有找到用户");
                }
            }
            else
            {
                _ = LoadUserSearchAvatarsAsync();
            }
        }
        catch (SolarApiException ex)
        {
            UserSearchResults.Clear();
            HasUserSearchResults = false;
            if (showEmptyToast)
            {
                _toast.Error($"搜索失败：{ex.ApiMessage ?? ex.Message}");
            }
        }
        finally
        {
            IsSearchingUsers = false;
        }
    }

    [RelayCommand]
    private async Task StartDirectChatAsync()
    {
        var text = DirectUserIdText.Trim();
        if (!Guid.TryParse(text, out var userId))
        {
            // Fallback: treat as search query
            if (!string.IsNullOrWhiteSpace(UserSearchQuery) || !string.IsNullOrWhiteSpace(text))
            {
                if (string.IsNullOrWhiteSpace(UserSearchQuery))
                {
                    UserSearchQuery = text;
                }

                await SearchUsersAsync().ConfigureAwait(true);
                return;
            }

            _toast.Show("请搜索用户名，或输入对方账号 UUID");
            return;
        }

        await OpenDirectChatWithUserIdAsync(userId).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task StartDirectChatWithUserAsync(UserSearchResultItem? user)
    {
        if (user is null || user.AccountId == Guid.Empty)
        {
            return;
        }

        await OpenDirectChatWithUserIdAsync(user.AccountId, displayName: user.Nick).ConfigureAwait(true);
    }

    private async Task OpenDirectChatWithUserIdAsync(Guid userId, string? displayName = null)
    {
        try
        {
            IsBusy = true;
            var item = await EnsureDirectChatAsync(userId, displayName).ConfigureAwait(true);
            if (item is null)
            {
                return;
            }

            DirectUserIdText = string.Empty;
            UserSearchQuery = string.Empty;
            UserSearchResults.Clear();
            HasUserSearchResults = false;

            _toast.Success(string.IsNullOrWhiteSpace(displayName) ? "已打开私聊" : $"已打开与 {displayName} 的私聊");
            OpenRoom(item);
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"私聊失败：{ex.ApiMessage ?? ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Create or reopen a direct chat and ensure it appears in the room list.
    /// Used by home / user profile "私聊" entry points.
    /// </summary>
    public async Task<ChatRoomListItem?> EnsureDirectChatAsync(Guid userId, string? displayName = null)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        var existing = await _api.GetDirectChatAsync(userId).ConfigureAwait(true);
        var room = existing ?? await _api.CreateDirectChatAsync(userId).ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(room.Name) && !string.IsNullOrWhiteSpace(displayName))
        {
            room.Name = displayName;
        }

        var item = Rooms.FirstOrDefault(r => r.RoomId == room.Id);
        if (item is null)
        {
            item = new ChatRoomListItem(room, null, _imageLoader, _authService.CurrentAccount?.Id);
            Rooms.Insert(0, item);
            if (_cache.TryGetRooms(out var cachedRooms))
            {
                var merged = new List<SnChatRoom> { room };
                merged.AddRange(cachedRooms.Where(r => r.Id != room.Id));
                _cache.SetRooms(merged);
            }
            else
            {
                _cache.SetRooms([room]);
            }
        }

        RefreshStatusText();
        return item;
    }

    /// <summary>Open a room (fires RoomSelected for ChatPage navigation).</summary>
    public void OpenRoomExternal(ChatRoomListItem item) => OpenRoom(item);

    private async Task LoadUserSearchAvatarsAsync()
    {
        var pending = UserSearchResults
            .Where(u => u.HasAvatar && u.AvatarImage is null && !string.IsNullOrWhiteSpace(u.AvatarUrl))
            .Select(u => (Item: u, Task: _imageLoader.LoadSafeAsync(u.AvatarUrl!)))
            .ToList();

        if (pending.Count == 0)
        {
            return;
        }

        await Task.WhenAll(pending.Select(p => p.Task)).ConfigureAwait(true);
        foreach (var (item, task) in pending)
        {
            if (task.Result is { } bmp)
            {
                item.AvatarImage = bmp;
            }
        }
    }

    [RelayCommand]
    private async Task AcceptInviteAsync(ChatRoomListItem? invite)
    {
        if (invite is null)
        {
            return;
        }

        try
        {
            await _api.AcceptChatInviteAsync(invite.RoomId).ConfigureAwait(true);
            PendingInvites.Remove(invite);
            PendingInviteCount = PendingInvites.Count;
            await ForceRefreshAsync().ConfigureAwait(true);
            _toast.Success("已接受邀请");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"接受失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeclineInviteAsync(ChatRoomListItem? invite)
    {
        if (invite is null)
        {
            return;
        }

        try
        {
            await _api.DeclineChatInviteAsync(invite.RoomId).ConfigureAwait(true);
            PendingInvites.Remove(invite);
            PendingInviteCount = PendingInvites.Count;
            _toast.Success("已拒绝邀请");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"拒绝失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    private async Task RefreshUnreadAndInvitesAsync(int gen)
    {
        try
        {
            var total = await _api.GetChatUnreadCountAsync().ConfigureAwait(true);
            if (gen == _loadGeneration)
            {
                TotalUnreadCount = total;
                _cache.TotalUnread = total;
            }
        }
        catch
        {
            if (gen == _loadGeneration)
            {
                TotalUnreadCount = _cache.TotalUnread ?? Rooms.Sum(r => r.UnreadCount);
            }
        }

        try
        {
            var invites = await _api.GetChatInvitesAsync().ConfigureAwait(true);
            if (gen != _loadGeneration)
            {
                return;
            }

            _cache.SetInvites(invites);
            PendingInvites.Clear();
            var meId = _authService.CurrentAccount?.Id;
            foreach (var room in invites)
            {
                PendingInvites.Add(new ChatRoomListItem(room, null, _imageLoader, meId));
            }

            PendingInviteCount = PendingInvites.Count;
        }
        catch
        {
            if (gen == _loadGeneration
                && _cache.TryGetInvites(out var cached)
                && PendingInvites.Count == 0
                && cached.Count > 0)
            {
                var meId = _authService.CurrentAccount?.Id;
                foreach (var room in cached)
                {
                    PendingInvites.Add(new ChatRoomListItem(room, null, _imageLoader, meId));
                }

                PendingInviteCount = PendingInvites.Count;
            }
        }
    }

    private void RefreshStatusText()
    {
        if (TotalUnreadCount <= 0)
        {
            TotalUnreadCount = Rooms.Sum(r => r.UnreadCount);
        }

        StatusText = PendingInviteCount > 0
            ? $"会话 {Rooms.Count} · 未读 {TotalUnreadCount} · 邀请 {PendingInviteCount}"
            : $"会话 {Rooms.Count} · 未读 {TotalUnreadCount}";
    }

    private async Task RefreshGroupsAsync(int gen)
    {
        try
        {
            var groups = await _api.GetChatGroupsAsync().ConfigureAwait(true);
            if (gen != _loadGeneration)
            {
                return;
            }

            _cache.SetGroups(groups);
            Groups.Clear();
            foreach (var g in groups.OrderBy(x => x.Order).ThenBy(x => x.Name))
            {
                Groups.Add(new ChatGroupItemViewModel(g));
            }
        }
        catch
        {
            if (gen == _loadGeneration
                && Groups.Count == 0
                && _cache.TryGetGroups(out var cached)
                && cached.Count > 0)
            {
                foreach (var g in cached.OrderBy(x => x.Order).ThenBy(x => x.Name))
                {
                    Groups.Add(new ChatGroupItemViewModel(g));
                }
            }
        }
    }

    private async Task ApplyRoomsSyncAsync(int gen)
    {
        try
        {
            var sync = await _api.SyncChatRoomsAsync(_roomsSyncTimestamp).ConfigureAwait(true);
            if (gen != _loadGeneration)
            {
                return;
            }

            if (sync.CurrentTimestamp is { } ts)
            {
                _roomsSyncTimestamp = ts.ToUnixTimeMilliseconds();
            }
            else
            {
                _roomsSyncTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }

            var changeCount = sync.Changes?.Count ?? 0;
            var summaryCount = sync.Summaries?.Count ?? 0;
            SyncStatusText = changeCount + summaryCount > 0
                ? $"房间同步 +{changeCount} 变更 / {summaryCount} 摘要"
                : $"房间同步 ok · ts {_roomsSyncTimestamp}";

            if (sync.Summaries is { Count: > 0 })
            {
                foreach (var s in sync.Summaries)
                {
                    var row = Rooms.FirstOrDefault(r => r.RoomId == s.RoomId);
                    if (row is null)
                    {
                        continue;
                    }

                    row.UnreadCount = s.UnreadCount;
                    if (s.LastMessage is { } last)
                    {
                        row.LastMessagePreview = string.IsNullOrWhiteSpace(last.Content)
                            ? $"[{last.Type ?? "消息"}]"
                            : last.Content!;
                        row.LastMessageTime = last.CreatedAt?.ToLocalTime().ToString("HH:mm") ?? row.LastMessageTime;
                    }
                }

                TotalUnreadCount = Rooms.Sum(r => r.UnreadCount);
            }
        }
        catch (Exception ex)
        {
            SyncStatusText = $"房间同步失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ReconnectWsAsync()
    {
        try
        {
            await _ws.DisconnectAsync().ConfigureAwait(true);
            await _ws.ConnectAsync().ConfigureAwait(true);
            _toast.Success("WebSocket 已重连");
        }
        catch (Exception ex)
        {
            _toast.Error($"WS 重连失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RunGlobalMessageSyncAsync()
    {
        try
        {
            IsRefreshing = true;
            var resp = await _api.SyncAllChatMessagesAsync(new SyncRequest
            {
                LastSyncTimestamp = 0,
            }).ConfigureAwait(true);

            var n = resp.Messages?.Count ?? resp.TotalCount;
            SyncStatusText = $"全局消息同步：{n} 条";
            _toast.Success($"全局 sync 完成（{n}）");
            await RefreshAsync(silent: true).ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"全局 sync 失败：{ex.ApiMessage ?? ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task LoadRealmChatsAsync()
    {
        var slug = RealmSlugText.Trim();
        if (slug.Length == 0)
        {
            _toast.Show("请输入 Realm slug");
            return;
        }

        try
        {
            IsBusy = true;
            var rooms = await _api.GetRealmChatRoomsAsync(slug).ConfigureAwait(true);
            var meId = _authService.CurrentAccount?.Id;
            Rooms.Clear();
            foreach (var room in rooms)
            {
                Rooms.Add(new ChatRoomListItem(room, null, _imageLoader, meId));
            }

            StatusText = $"Realm «{slug}» · {Rooms.Count} 个聊天";
            _toast.Success($"已加载 Realm 聊天 {Rooms.Count}");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"Realm 聊天失败：{ex.ApiMessage ?? ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RenameSelectedGroupAsync()
    {
        if (!Guid.TryParse(SelectedGroupIdText.Trim(), out var groupId))
        {
            _toast.Show("请填写分组 UUID（从下方分组列表复制）");
            return;
        }

        var name = NewGroupName.Trim();
        if (name.Length == 0)
        {
            _toast.Show("请输入新分组名称（上面「新建群聊名称」输入框复用）");
            return;
        }

        try
        {
            await _api.UpdateChatGroupAsync(groupId, new UpdateGroupRequest { Name = name }).ConfigureAwait(true);
            await RefreshGroupsAsync(_loadGeneration).ConfigureAwait(true);
            _toast.Success("分组已更新");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"分组更新失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedGroupAsync()
    {
        if (!Guid.TryParse(SelectedGroupIdText.Trim(), out var groupId))
        {
            _toast.Show("请填写分组 UUID");
            return;
        }

        try
        {
            await _api.DeleteChatGroupAsync(groupId).ConfigureAwait(true);
            await RefreshGroupsAsync(_loadGeneration).ConfigureAwait(true);
            _toast.Success("分组已删除");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"删除分组失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private async Task MoveRoomToSelectedGroupAsync()
    {
        if (!Guid.TryParse(MoveRoomIdText.Trim(), out var roomId))
        {
            // default: first selected-looking = none; use text
            _toast.Show("请填写要移动的会话 Room UUID");
            return;
        }

        Guid? groupId = null;
        if (Guid.TryParse(SelectedGroupIdText.Trim(), out var g))
        {
            groupId = g;
        }

        try
        {
            await _api.MoveRoomToGroupAsync(roomId, groupId).ConfigureAwait(true);
            _toast.Success(groupId is null ? "已移出分组" : "已移入分组");
            await RefreshGroupsAsync(_loadGeneration).ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"移动失败：{ex.ApiMessage ?? ex.Message}");
        }
    }

    [RelayCommand]
    private void UseGroup(ChatGroupItemViewModel? group)
    {
        if (group is null)
        {
            return;
        }

        SelectedGroupIdText = group.Id.ToString("D");
        NewGroupName = group.Name;
    }

    private void MergeRooms(List<ChatRoomListItem> next)
    {
        // Build map of existing by room id
        var existing = Rooms.ToDictionary(r => r.RoomId);
        Rooms.Clear();

        foreach (var n in next)
        {
            if (existing.TryGetValue(n.RoomId, out var old))
            {
                // Reuse avatar image if same url
                if (old.AvatarImage is not null
                    && string.Equals(old.AvatarUrl, n.AvatarUrl, StringComparison.OrdinalIgnoreCase))
                {
                    n.SetAuthenticatedAvatar(old.AvatarImage);
                }
            }

            Rooms.Add(n);
        }
    }

    private List<ChatRoomListItem> BuildItems(
        List<SnChatRoom> rooms,
        Dictionary<string, ChatSummaryResponse>? summary,
        Guid? meId)
    {
        var list = new List<ChatRoomListItem>();

        if (rooms.Count == 0 && summary is { Count: > 0 })
        {
            foreach (var (key, value) in summary)
            {
                if (!Guid.TryParse(key, out var id))
                {
                    continue;
                }

                var synthetic = new SnChatRoom
                {
                    Id = id,
                    Name = value.LastMessage?.ChatRoom?.Name
                        ?? value.LastMessage?.Sender?.Nick
                        ?? value.LastMessage?.Sender?.Account?.Nick
                        ?? "会话",
                    Picture = value.LastMessage?.ChatRoom?.Picture
                        ?? value.LastMessage?.Sender?.Account?.Profile?.Picture,
                    Members = value.LastMessage?.Sender is { } sender
                        ? [new ChatMemberTransmissionObject
                        {
                            Id = sender.Id,
                            AccountId = sender.AccountId,
                            Account = sender.Account,
                            Nick = sender.Nick,
                            Username = sender.Username,
                        }]
                        : null,
                };
                list.Add(new ChatRoomListItem(synthetic, value, _imageLoader, meId));
            }

            return list;
        }

        foreach (var room in rooms.OrderByDescending(r => GetSortKey(r, summary)))
        {
            ChatSummaryResponse? s = null;
            if (summary is not null)
            {
                summary.TryGetValue(room.Id.ToString(), out s);
                s ??= summary.FirstOrDefault(kv =>
                    string.Equals(kv.Key, room.Id.ToString("D"), StringComparison.OrdinalIgnoreCase)).Value;
            }

            list.Add(new ChatRoomListItem(room, s, _imageLoader, meId));
        }

        return list;
    }

    private async Task LoadAvatarsAuthenticatedAsync()
    {
        var pending = Rooms
            .Where(r => !string.IsNullOrWhiteSpace(r.AvatarUrl) && r.AvatarImage is null)
            .Select(r => (Room: r, Task: _imageLoader.LoadSafeAsync(r.AvatarUrl)))
            .ToList();

        if (pending.Count > 0)
        {
            // Parallel download; apply in one UI turn so rows don't pop individually.
            await Task.WhenAll(pending.Select(t => t.Task)).ConfigureAwait(true);
            foreach (var (room, task) in pending)
            {
                if (task.Result is { } bmp)
                {
                    room.SetAuthenticatedAvatar(bmp);
                }
            }
        }

        _avatarsLoaded = true;
    }

    private static DateTimeOffset GetSortKey(SnChatRoom room, Dictionary<string, ChatSummaryResponse>? summary)
    {
        if (summary is not null && summary.TryGetValue(room.Id.ToString(), out var s) && s.LastMessage?.CreatedAt is { } t)
        {
            return t;
        }

        return room.UpdatedAt ?? room.CreatedAt ?? DateTimeOffset.MinValue;
    }
}
