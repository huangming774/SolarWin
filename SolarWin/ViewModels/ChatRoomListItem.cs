using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.ViewModels;

/// <summary>One row in the chat room list (summary + room).</summary>
public partial class ChatRoomListItem : ObservableObject
{
    public ChatRoomListItem(SnChatRoom room, ChatSummaryResponse? summary, Guid? currentAccountId = null)
    {
        Room = room;
        RoomId = room.Id;
        Name = ResolveName(room);
        AvatarUrl = CloudFileUrlHelper.ResolveRoomAvatar(room, currentAccountId);
        HasAvatar = !string.IsNullOrWhiteSpace(AvatarUrl);
        InitialsOpacity = HasAvatar ? 0.0 : 1.0;
        AvatarOpacity = HasAvatar ? 1.0 : 0.0;
        Description = room.Description;

        // Public URL first; may be replaced by authenticated load later.
        if (HasAvatar)
        {
            AvatarImage = AvatarImageHelper.TryCreateBitmap(AvatarUrl);
        }

        if (summary?.LastMessage is { } last)
        {
            LastMessagePreview = string.IsNullOrWhiteSpace(last.Content)
                ? (last.Type is null or "text" ? "（消息）" : $"[{last.Type}]")
                : last.Content;
            LastMessageTime = FormatTime(last.CreatedAt);
        }
        else
        {
            LastMessagePreview = "暂无消息";
            LastMessageTime = string.Empty;
        }

        ApplyUnread(summary?.UnreadCount ?? 0);
    }

    public SnChatRoom Room { get; }

    public Guid RoomId { get; }

    public string Name { get; }

    public string? AvatarUrl { get; }

    public bool HasAvatar { get; }

    public double InitialsOpacity { get; private set; }

    public double AvatarOpacity { get; private set; }

    [ObservableProperty]
    public partial BitmapImage? AvatarImage { get; set; }

    public string? Description { get; }

    [ObservableProperty]
    public partial string LastMessagePreview { get; set; } = "暂无消息";

    [ObservableProperty]
    public partial string LastMessageTime { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnread))]
    [NotifyPropertyChangedFor(nameof(UnreadOpacity))]
    [NotifyPropertyChangedFor(nameof(UnreadBadge))]
    public partial int UnreadCount { get; set; }

    public bool HasUnread => UnreadCount > 0;

    public double UnreadOpacity => HasUnread ? 1.0 : 0.0;

    public string UnreadBadge => UnreadCount > 99 ? "99+" : (UnreadCount > 0 ? UnreadCount.ToString() : string.Empty);

    public void ApplyUnread(int count)
    {
        UnreadCount = Math.Max(0, count);
    }

    public void MarkReadLocal() => ApplyUnread(0);

    public void UpdatePreview(string? preview, DateTimeOffset? time)
    {
        if (!string.IsNullOrWhiteSpace(preview))
        {
            LastMessagePreview = preview!;
        }

        if (time is not null)
        {
            LastMessageTime = FormatTime(time);
        }
    }

    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return "?";
            }

            return Name.Length >= 1 ? Name[..1].ToUpperInvariant() : "?";
        }
    }

    public void SetAuthenticatedAvatar(BitmapImage? image)
    {
        if (image is null)
        {
            return;
        }

        AvatarImage = image;
        InitialsOpacity = 0.0;
        AvatarOpacity = 1.0;
        OnPropertyChanged(nameof(InitialsOpacity));
        OnPropertyChanged(nameof(AvatarOpacity));
    }

    private static string ResolveName(SnChatRoom room)
    {
        if (!string.IsNullOrWhiteSpace(room.Name))
        {
            return room.Name!;
        }

        if (room.Members is { Count: > 0 })
        {
            var names = room.Members
                .Select(m => m.Nick ?? m.Username ?? m.Account?.Nick ?? m.Account?.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Take(3)
                .ToArray();
            if (names.Length > 0)
            {
                return string.Join("、", names!);
            }
        }

        return "未命名会话";
    }

    private static string FormatTime(DateTimeOffset? time)
    {
        if (time is null)
        {
            return string.Empty;
        }

        var local = time.Value.ToLocalTime();
        var now = DateTimeOffset.Now;
        if (local.Date == now.Date)
        {
            return local.ToString("HH:mm");
        }

        if (local.Year == now.Year)
        {
            return local.ToString("MM-dd HH:mm");
        }

        return local.ToString("yyyy-MM-dd");
    }
}
