using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>
/// Chat room list with in-memory cache (singleton lifetime).
/// Returning to the page reuses list; background refresh when stale.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);

    private readonly ISolarApiClient _api;
    private readonly IAuthService _authService;
    private readonly DysonFileImageLoader _imageLoader;

    private DateTimeOffset _lastLoadedAt = DateTimeOffset.MinValue;
    private bool _avatarsLoaded;
    private int _loadGeneration;

    public ChatViewModel(
        ISolarApiClient api,
        IAuthService authService,
        DysonFileImageLoader imageLoader)
    {
        _api = api;
        _authService = authService;
        _imageLoader = imageLoader;
    }

    public ObservableCollection<ChatRoomListItem> Rooms { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    public bool HasCache => Rooms.Count > 0;

    public bool IsCacheFresh => HasCache && DateTimeOffset.UtcNow - _lastLoadedAt < CacheTtl;

    public event EventHandler<ChatRoomListItem>? RoomSelected;

    /// <summary>
    /// Load list. Uses cache when available; only clears UI on full reload.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        // Soft entry: show cache immediately, refresh in background if stale.
        if (IsCacheFresh)
        {
            StatusText = $"会话 {Rooms.Count} · 未读 {Rooms.Sum(r => r.UnreadCount)}（缓存）";
            return;
        }

        if (HasCache)
        {
            await RefreshAsync(silent: true).ConfigureAwait(true);
            return;
        }

        await RefreshAsync(silent: false).ConfigureAwait(true);
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

            var meId = _authService.CurrentAccount?.Id;

            List<SnChatRoom> rooms;
            try
            {
                rooms = await _api.GetChatRoomsAsync().ConfigureAwait(true);
            }
            catch (SolarApiException ex)
            {
                rooms = [];
                if (!HasCache)
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
            }
            catch (SolarApiException)
            {
                // optional
            }

            if (gen != _loadGeneration)
            {
                return;
            }

            var next = BuildItems(rooms, summary, meId);

            // Diff-update to preserve avatar bitmaps when possible
            MergeRooms(next);

            _lastLoadedAt = DateTimeOffset.UtcNow;
            var unread = Rooms.Sum(r => r.UnreadCount);
            StatusText = $"会话 {Rooms.Count} · 未读 {unread}";
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

        var unread = Rooms.Sum(r => r.UnreadCount);
        StatusText = $"会话 {Rooms.Count} · 未读 {unread}";
    }

    public void MarkRoomReadLocal(Guid roomId)
    {
        var item = Rooms.FirstOrDefault(r => r.RoomId == roomId);
        item?.MarkReadLocal();
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

    private static List<ChatRoomListItem> BuildItems(
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
                list.Add(new ChatRoomListItem(synthetic, value, meId));
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

            list.Add(new ChatRoomListItem(room, s, meId));
        }

        return list;
    }

    private async Task LoadAvatarsAuthenticatedAsync()
    {
        var snapshot = Rooms.Where(r => !string.IsNullOrWhiteSpace(r.AvatarUrl)).ToList();
        foreach (var room in snapshot)
        {
            try
            {
                var bmp = await _imageLoader.LoadAsync(room.AvatarUrl).ConfigureAwait(true);
                if (bmp is not null)
                {
                    room.SetAuthenticatedAvatar(bmp);
                }
            }
            catch
            {
                // keep initials
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
