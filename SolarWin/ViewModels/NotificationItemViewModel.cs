using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.ViewModels;

public partial class NotificationItemViewModel : ObservableObject
{
    public NotificationItemViewModel(SnNotification notification, DysonFileImageLoader imageLoader)
    {
        Notification = notification;
        _ = imageLoader;
        Id = notification.Id;
        Title = string.IsNullOrWhiteSpace(notification.Title) ? "通知" : notification.Title!;
        Subtitle = notification.Subtitle ?? string.Empty;
        Content = notification.Content ?? string.Empty;
        Topic = notification.Topic ?? string.Empty;
        TimeText = FormatTime(notification.CreatedAt);
        IsUnread = notification.ViewedAt is null;
        UnreadOpacity = IsUnread ? 1.0 : 0.0;
        TitleWeightName = IsUnread ? "SemiBold" : "Normal";
        CardOpacity = IsUnread ? 1.0 : 0.85;
        AvatarUrl = FindAvatarUrl(notification.Meta);
        Initials = GetInitials(notification);
        // Avatar paints via FastWin2DImage + AvatarUrl (GPU); no BitmapImage prefetch.
        AvatarOpacity = 0;
        InitialsOpacity = 1;
    }

    public SnNotification Notification { get; }
    public Guid Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string Content { get; }
    public string Topic { get; }
    public string TimeText { get; }
    public string Initials { get; }
    public string? AvatarUrl { get; }

    /// <summary>Legacy BitmapImage slot (unused on GPU path).</summary>
    [ObservableProperty]
    public partial BitmapImage? AvatarImage { get; set; }

    [ObservableProperty]
    public partial double AvatarOpacity { get; set; }

    [ObservableProperty]
    public partial double InitialsOpacity { get; set; } = 1.0;

    public bool IsUnread { get; private set; }
    public double UnreadOpacity { get; private set; }
    public double CardOpacity { get; private set; }

    /// <summary>Font weight name for XAML FontWeight conversion via resource or code-behind free string.</summary>
    public string TitleWeightName { get; private set; }

    public string UnreadLabel => IsUnread ? "未读" : "已读";

    public void MarkReadLocal()
    {
        IsUnread = false;
        UnreadOpacity = 0.0;
        CardOpacity = 0.85;
        TitleWeightName = "Normal";
        OnPropertyChanged(nameof(IsUnread));
        OnPropertyChanged(nameof(UnreadOpacity));
        OnPropertyChanged(nameof(CardOpacity));
        OnPropertyChanged(nameof(TitleWeightName));
        OnPropertyChanged(nameof(UnreadLabel));
    }

    private static string GetInitials(SnNotification notification)
    {
        var text = notification.Meta is not null
            ? FindText(
                notification.Meta,
                "nick",
                "nickname",
                "name",
                "username",
                "account_name",
                "sender_name",
                "operator_name",
                "user_name",
                "display_name")
            : null;
        text ??= notification.Subtitle;
        text ??= notification.Title;
        return string.IsNullOrWhiteSpace(text) ? "?" : text.Trim()[..1].ToUpperInvariant();
    }

    private static string? FindAvatarUrl(Dictionary<string, JsonElement>? meta)
    {
        if (meta is null)
        {
            return null;
        }

        // Prefer well-known top-level keys first (faster + avoids wrong nested matches).
        foreach (var key in PreferredAvatarKeys)
        {
            if (!meta.TryGetValue(key, out var value))
            {
                continue;
            }

            var url = ResolveAvatarValue(value);
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        // Nested account / user / publisher blobs often carry profile.picture.
        foreach (var key in ActorObjectKeys)
        {
            if (!meta.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var fromActor = ResolveFromActorObject(value);
            if (!string.IsNullOrWhiteSpace(fromActor))
            {
                return fromActor;
            }
        }

        foreach (var value in meta.Values)
        {
            var url = FindAvatarUrl(value, 0);
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        return null;
    }

    private static readonly string[] PreferredAvatarKeys =
    [
        "avatar",
        "avatar_url",
        "avatarUrl",
        "avatar_id",
        "avatarId",
        "picture",
        "picture_url",
        "pictureUrl",
        "picture_id",
        "pictureId",
        "profile_picture",
        "profilePicture",
        "icon",
        "icon_url",
        "iconUrl",
        "icon_id",
        "iconId",
        "image",
        "image_url",
        "imageUrl",
        "image_id",
        "imageId",
        "thumbnail",
        "thumbnail_url",
        "thumb_url",
        "photo",
    ];

    private static readonly string[] ActorObjectKeys =
    [
        "user",
        "account",
        "sender",
        "operator",
        "actor",
        "from",
        "author",
        "publisher",
        "related_user",
        "related_account",
        "profile",
    ];

    private static string? FindAvatarUrl(JsonElement element, int depth)
    {
        if (depth > 6)
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            // Direct actor-shaped object (SnAccount / SnPublisher / profile).
            var fromActor = ResolveFromActorObject(element);
            if (!string.IsNullOrWhiteSpace(fromActor))
            {
                return fromActor;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (IsAvatarProperty(property.Name))
                {
                    var url = ResolveAvatarValue(property.Value);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        return url;
                    }
                }

                var nested = FindAvatarUrl(property.Value, depth + 1);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindAvatarUrl(item, depth + 1);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    private static string? ResolveFromActorObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // SnPublisher / SnCloudFile-ish picture field
        if (TryGetPropertyIgnoreCase(element, "picture", out var picture))
        {
            var url = ResolveAvatarValue(picture);
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        // SnAccount.profile.picture
        if (TryGetPropertyIgnoreCase(element, "profile", out var profile)
            && profile.ValueKind == JsonValueKind.Object
            && TryGetPropertyIgnoreCase(profile, "picture", out var profilePicture))
        {
            var url = ResolveAvatarValue(profilePicture);
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        // SnAccount nested under actor
        if (TryGetPropertyIgnoreCase(element, "account", out var account)
            && account.ValueKind == JsonValueKind.Object)
        {
            var nested = ResolveFromActorObject(account);
            if (!string.IsNullOrWhiteSpace(nested))
            {
                return nested;
            }
        }

        foreach (var key in PreferredAvatarKeys)
        {
            if (!TryGetPropertyIgnoreCase(element, key, out var value))
            {
                continue;
            }

            var url = ResolveAvatarValue(value);
            if (!string.IsNullOrWhiteSpace(url))
            {
                return url;
            }
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string? ResolveAvatarValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // Skip obvious non-file tokens (pure display names without path/id shape).
            text = text.Trim();
            if (text.Contains('/') || text.Contains(':') || LooksLikeFileId(text))
            {
                return text.Contains('/') || text.Contains(':')
                    ? CloudFileUrlHelper.Normalize(text)
                    : CloudFileUrlHelper.DriveFileUrl(text);
            }

            return null;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            // Prefer id / url without full deserialize when possible.
            if (TryGetPropertyIgnoreCase(value, "id", out var idEl)
                && idEl.ValueKind == JsonValueKind.String)
            {
                var id = idEl.GetString();
                if (!string.IsNullOrWhiteSpace(id) && LooksLikeFileId(id))
                {
                    return CloudFileUrlHelper.DriveFileUrl(id);
                }
            }

            if (TryGetPropertyIgnoreCase(value, "url", out var urlEl)
                && urlEl.ValueKind == JsonValueKind.String)
            {
                var normalized = CloudFileUrlHelper.Normalize(urlEl.GetString());
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return normalized;
                }
            }

            try
            {
                var file = value.Deserialize<SnCloudFile>(JsonDefaults.Options);
                var resolved = CloudFileUrlHelper.Resolve(file);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    return resolved;
                }
            }
            catch (JsonException)
            {
                // fall through
            }

            // Nested profile.picture inside this object
            return ResolveFromActorObject(value);
        }

        return null;
    }

    /// <summary>Drive file ids are typically UUID / hex-ish tokens, not CJK display names.</summary>
    private static bool LooksLikeFileId(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var s = text.Trim();
        if (s.Length is < 8 or > 128)
        {
            return false;
        }

        // UUID
        if (Guid.TryParse(s, out _))
        {
            return true;
        }

        // Hex / base62-ish file tokens without spaces
        var hasLetterOrDigit = false;
        foreach (var ch in s)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.')
            {
                hasLetterOrDigit = true;
                continue;
            }

            return false;
        }

        return hasLetterOrDigit;
    }

    private static bool IsAvatarProperty(string name)
        => PreferredAvatarKeys.Any(k => k.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static string? FindText(Dictionary<string, JsonElement> meta, params string[] names)
    {
        foreach (var name in names)
        {
            if (meta.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        // Nested nick/name on actor objects
        foreach (var key in ActorObjectKeys)
        {
            if (!meta.TryGetValue(key, out var actor) || actor.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var nestedName in new[] { "nick", "nickname", "name", "username", "display_name" })
            {
                if (TryGetPropertyIgnoreCase(actor, nestedName, out var el)
                    && el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        return s;
                    }
                }
            }
        }

        return null;
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

        return local.ToString("yyyy-MM-dd HH:mm");
    }
}
