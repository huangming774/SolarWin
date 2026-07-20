using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.ViewModels;

/// <summary>Single chat bubble bound in the detail ListView.</summary>
public partial class MessageItemViewModel : ObservableObject
{
    public MessageItemViewModel(SnChatMessage message, Guid? currentAccountId, DysonFileImageLoader imageLoader)
    {
        Message = message;
        IsMine = IsSentByCurrentUser(message, currentAccountId);
        Alignment = IsMine ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        var isImageType = string.Equals(message.Type, "image", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message.Type, "media", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message.Type, "sticker", StringComparison.OrdinalIgnoreCase);

        Content = string.IsNullOrWhiteSpace(message.Content)
            ? (message.IsEncrypted
                ? "（加密消息）"
                : isImageType
                    ? string.Empty
                    : (message.Attachments is { Count: > 0 } ? string.Empty : $"[{message.Type ?? "消息"}]"))
            : message.Content!;

        HasText = !string.IsNullOrWhiteSpace(Content);
        TextOpacity = HasText ? 1.0 : 0.0;

        SenderName = message.Sender?.Nick
            ?? message.Sender?.Username
            ?? message.Sender?.Account?.Nick
            ?? message.Sender?.Account?.Name
            ?? (IsMine ? "我" : "对方");
        TimeText = FormatTime(message.CreatedAt);
        ShowSenderName = !IsMine;
        SenderNameOpacity = IsMine ? 0.0 : 0.65;
        BubbleOpacity = IsMine ? 1.0 : 0.95;

        AvatarUrl = CloudFileUrlHelper.ResolveAccountAvatar(message.Sender?.Account)
            ?? CloudFileUrlHelper.Resolve(message.Sender?.Account?.Profile?.Picture);
        HasAvatar = !string.IsNullOrWhiteSpace(AvatarUrl);
        if (HasAvatar && imageLoader.TryGetCached(AvatarUrl, out var cachedAvatar))
        {
            SetAuthenticatedAvatar(cachedAvatar!);
        }
        else
        {
            // Initials until the authenticated download lands; a bare UriSource would 401 on private drive files.
            AvatarOpacity = 0.0;
            InitialsOpacity = 1.0;
        }
        Initials = string.IsNullOrWhiteSpace(SenderName) ? "?" : SenderName[..1].ToUpperInvariant();

        // Attachments
        if (message.Attachments is { Count: > 0 })
        {
            foreach (var att in message.Attachments)
            {
                var vm = new MessageAttachmentViewModel(att);
                // Force image when message type says so
                if (isImageType && !vm.IsImage && !string.IsNullOrWhiteSpace(vm.FileId ?? vm.Url))
                {
                    Attachments.Add(new MessageAttachmentViewModel(ForceImage(att)));
                }
                else
                {
                    Attachments.Add(vm);
                }
            }
        }

        // meta may contain file ids: meta.file_id / meta.attachment_ids
        if (Attachments.Count == 0 && message.Meta is not null)
        {
            TryAddFromMeta(message.Meta, isImageType);
        }

        HasAttachments = Attachments.Count > 0;
        AttachmentsOpacity = HasAttachments ? 1.0 : 0.0;
        HasImages = Attachments.Any(a => a.IsImage);
    }

    public SnChatMessage Message { get; }

    public Guid MessageId => Message.Id;

    public bool IsMine { get; }

    public bool ShowSenderName { get; }

    public double SenderNameOpacity { get; }

    public double BubbleOpacity { get; }

    public HorizontalAlignment Alignment { get; }

    public string Content { get; }

    public bool HasText { get; }

    public double TextOpacity { get; }

    public string SenderName { get; }

    public string TimeText { get; }

    public long RoomSequence => Message.RoomSequence;

    public string? AvatarUrl { get; }

    public bool HasAvatar { get; }

    [ObservableProperty]
    public partial BitmapImage? AvatarImage { get; set; }

    [ObservableProperty]
    public partial double AvatarOpacity { get; set; }

    [ObservableProperty]
    public partial double InitialsOpacity { get; set; }

    public string Initials { get; }

    public ObservableCollection<MessageAttachmentViewModel> Attachments { get; } = [];

    public bool HasAttachments { get; }

    public double AttachmentsOpacity { get; }

    public bool HasImages { get; }

    /// <summary>True once the avatar came from the authenticated loader; skips redundant re-sets.</summary>
    public bool AvatarAuthenticated { get; private set; }

    public void SetAuthenticatedAvatar(BitmapImage image)
    {
        AvatarImage = image;
        AvatarOpacity = 1.0;
        InitialsOpacity = 0.0;
        AvatarAuthenticated = true;
    }

    private void TryAddFromMeta(Dictionary<string, System.Text.Json.JsonElement> meta, bool forceImage)
    {
        void AddId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            if (Attachments.Any(a => string.Equals(a.FileId, id, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var file = new SnCloudFile { Id = id, MimeType = forceImage ? "image/jpeg" : null };
            Attachments.Add(new MessageAttachmentViewModel(file));
        }

        foreach (var key in new[] { "file_id", "picture_id", "image_id", "attachment_id" })
        {
            if (meta.TryGetValue(key, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                AddId(el.GetString()!);
            }
        }

        foreach (var key in new[] { "attachments_id", "file_ids", "attachment_ids", "images" })
        {
            if (!meta.TryGetValue(key, out var el) || el.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    AddId(item.GetString()!);
                }
                else if (item.ValueKind == System.Text.Json.JsonValueKind.Object
                         && item.TryGetProperty("id", out var idEl)
                         && idEl.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    AddId(idEl.GetString()!);
                }
            }
        }
    }

    private static SnCloudFile ForceImage(SnCloudFile file)
        => new()
        {
            Id = file.Id,
            Name = file.Name,
            Url = file.Url,
            MimeType = string.IsNullOrWhiteSpace(file.MimeType) ? "image/jpeg" : file.MimeType,
            Size = file.Size,
            Width = file.Width,
            Height = file.Height,
            FileMeta = file.FileMeta,
        };

    private static bool IsSentByCurrentUser(SnChatMessage message, Guid? currentAccountId)
    {
        if (currentAccountId is null || currentAccountId == Guid.Empty)
        {
            return false;
        }

        if (message.Sender?.AccountId is Guid accountId && accountId == currentAccountId)
        {
            return true;
        }

        if (message.Sender?.Account?.Id is Guid nestedId && nestedId == currentAccountId)
        {
            return true;
        }

        return false;
    }

    private static string FormatTime(DateTimeOffset? time)
    {
        if (time is null)
        {
            return string.Empty;
        }

        return time.Value.ToLocalTime().ToString("HH:mm");
    }
}
