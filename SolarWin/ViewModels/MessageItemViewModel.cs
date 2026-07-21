using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.ViewModels;

/// <summary>Single chat bubble bound in the detail ListView.</summary>
public partial class MessageItemViewModel : ObservableObject
{
    /// <summary>Solian sticker markdown: <c>:prefix+slug:</c>.</summary>
    private static readonly Regex StickerPlaceholderRegex = new(
        @":([-\w]*\+[-\w]*):",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public MessageItemViewModel(SnChatMessage message, Guid? currentAccountId, DysonFileImageLoader imageLoader)
    {
        Message = message;
        IsMine = IsSentByCurrentUser(message, currentAccountId);
        Alignment = IsMine ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        var isImageType = string.Equals(message.Type, "image", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message.Type, "media", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message.Type, "sticker", StringComparison.OrdinalIgnoreCase);

        var rawContent = string.IsNullOrWhiteSpace(message.Content)
            ? (message.IsEncrypted
                ? "（加密消息）"
                : isImageType
                    ? string.Empty
                    : (message.Attachments is { Count: > 0 } ? string.Empty : $"[{message.Type ?? "消息"}]"))
            : message.Content!;

        // Parse :prefix+slug: stickers out of the text body.
        StickerPlaceholders = ExtractStickerPlaceholders(rawContent);
        if (StickerPlaceholders.Count > 0)
        {
            var stripped = StickerPlaceholderRegex.Replace(rawContent, string.Empty).Trim();
            Content = stripped;
            IsStickerOnly = string.IsNullOrWhiteSpace(stripped)
                            && (message.Attachments is null || message.Attachments.Count == 0);
        }
        else
        {
            Content = rawContent;
            IsStickerOnly = isImageType
                && string.Equals(message.Type, "sticker", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(rawContent);
        }

        HasText = !string.IsNullOrWhiteSpace(Content);
        TextOpacity = HasText ? 1.0 : 0.0;

        SenderName = message.Sender?.Nick
            ?? message.Sender?.Username
            ?? message.Sender?.Account?.Nick
            ?? message.Sender?.Account?.Name
            ?? (IsMine ? "我" : "对方");
        SenderAccountName = message.Sender?.Account?.Name
            ?? message.Sender?.Username;
        SenderAccountId = message.Sender?.Account?.Id
            ?? (message.Sender?.AccountId is { } aid && aid != Guid.Empty ? aid : null);
        TimeText = FormatTime(message.CreatedAt);
        ShowSenderName = !IsMine;
        SenderNameOpacity = IsMine ? 0.0 : 0.65;
        BubbleOpacity = IsMine ? 1.0 : 0.95;
        RefreshReactionText();

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

        // Pre-seed stickers from meta.sticker_id when present (rare; usually markdown only).
        if (Stickers.Count == 0 && message.Meta is not null)
        {
            TrySeedStickerFromMeta(message.Meta);
        }

        RefreshStickerFlags();

        // Quoted reply preview (replied_message nested or id-only)
        if (message.RepliedMessage is { } replied)
        {
            var who = replied.Sender?.Nick
                ?? replied.Sender?.Username
                ?? replied.Sender?.Account?.Nick
                ?? replied.Sender?.Account?.Name
                ?? "消息";
            var body = string.IsNullOrWhiteSpace(replied.Content)
                ? (replied.Attachments is { Count: > 0 } ? "[附件]" : "[消息]")
                : replied.Content!.Trim().Replace('\n', ' ');
            if (body.Length > 80)
            {
                body = body[..80] + "…";
            }

            ReplyPreviewText = $"{who}: {body}";
            HasReplyPreview = true;
            ReplyPreviewOpacity = 1.0;
        }
        else if (message.RepliedMessageId is { } rid && rid != Guid.Empty)
        {
            ReplyPreviewText = "回复一条消息";
            HasReplyPreview = true;
            ReplyPreviewOpacity = 1.0;
        }
        else
        {
            ReplyPreviewText = string.Empty;
            HasReplyPreview = false;
            ReplyPreviewOpacity = 0.0;
        }
    }

    public SnChatMessage Message { get; }

    public Guid MessageId => Message.Id;

    public bool IsMine { get; }

    /// <summary>True when this bubble quotes another message.</summary>
    public bool HasReplyPreview { get; }

    public double ReplyPreviewOpacity { get; }

    public string ReplyPreviewText { get; }

    public bool ShowSenderName { get; }

    /// <summary>Passport handle for opening profile (null if unknown).</summary>
    public string? SenderAccountName { get; }

    public Guid? SenderAccountId { get; }

    public bool CanOpenSenderProfile =>
        !IsMine
        && !string.IsNullOrWhiteSpace(SenderAccountName)
        && PostItemViewModel.LooksLikeAccountHandle(SenderAccountName);

    public UserProfileNavArgs? TryCreateSenderProfileArgs()
    {
        if (IsMine || string.IsNullOrWhiteSpace(SenderAccountName))
        {
            return null;
        }

        // Never open /passport/accounts/{nick} with Chinese display names.
        if (!PostItemViewModel.LooksLikeAccountHandle(SenderAccountName))
        {
            return null;
        }

        return new UserProfileNavArgs(SenderAccountName, SenderAccountId, SenderName);
    }

    public double SenderNameOpacity { get; }

    public double BubbleOpacity { get; }

    public HorizontalAlignment Alignment { get; }

    public string Content { get; set; }

    public bool HasText { get; set; }

    public double TextOpacity { get; set; }

    /// <summary>Full placeholders like <c>:prefix+slug:</c> still needing image resolve.</summary>
    public IReadOnlyList<string> StickerPlaceholders { get; }

    /// <summary>True when the bubble is only stickers (no leftover text / file attachments).</summary>
    public bool IsStickerOnly { get; private set; }

    public ObservableCollection<MessageStickerViewModel> Stickers { get; } = [];

    public bool HasStickers { get; private set; }

    public double StickersOpacity { get; private set; }

    public bool NeedsStickerLookup =>
        StickerPlaceholders.Count > 0
        && Stickers.Count < StickerPlaceholders.Count;

    public string SenderName { get; }

    public string TimeText { get; }

    [ObservableProperty]
    public partial string ReactionText { get; set; } = string.Empty;

    public bool HasReactions => !string.IsNullOrWhiteSpace(ReactionText);

    public Visibility ReactionVisibility => HasReactions ? Visibility.Visible : Visibility.Collapsed;

    public void RefreshReactionText()
    {
        if (Message.ReactionsCount is not { Count: > 0 })
        {
            ReactionText = string.Empty;
            OnPropertyChanged(nameof(HasReactions));
            OnPropertyChanged(nameof(ReactionVisibility));
            return;
        }

        ReactionText = string.Join(" ", Message.ReactionsCount
            .Where(kv => kv.Value > 0)
            .Select(kv => $"{kv.Key}×{kv.Value}"));
        OnPropertyChanged(nameof(HasReactions));
        OnPropertyChanged(nameof(ReactionVisibility));
    }

    public void ApplyEditedContent(string text)
    {
        Message.Content = text;
        var placeholders = ExtractStickerPlaceholders(text ?? string.Empty);
        // StickerPlaceholders is init-only; rebuild display text only.
        var stripped = placeholders.Count > 0
            ? StickerPlaceholderRegex.Replace(text ?? string.Empty, string.Empty).Trim()
            : (text ?? string.Empty);
        Content = stripped;
        HasText = !string.IsNullOrWhiteSpace(Content);
        TextOpacity = HasText ? 1.0 : 0.0;
        OnPropertyChanged(nameof(Content));
        OnPropertyChanged(nameof(HasText));
        OnPropertyChanged(nameof(TextOpacity));
    }

    /// <summary>Attach a resolved sticker bitmap slot (file id known; image loaded later).</summary>
    public MessageStickerViewModel EnsureStickerSlot(string placeholder, string? fileId, bool large)
    {
        var existing = Stickers.FirstOrDefault(s =>
            string.Equals(s.Placeholder, placeholder, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.FileId) && !string.IsNullOrWhiteSpace(fileId))
            {
                existing.FileId = fileId;
            }

            return existing;
        }

        var item = new MessageStickerViewModel(placeholder, fileId, large);
        Stickers.Add(item);
        RefreshStickerFlags();
        return item;
    }

    /// <summary>Seed local echo with a known DysonFS file id (skip network lookup).</summary>
    public void SeedStickerFromFileId(string placeholder, string fileId)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            return;
        }

        EnsureStickerSlot(
            string.IsNullOrWhiteSpace(placeholder) ? $":sticker:" : placeholder,
            fileId,
            large: IsStickerOnly || StickerPlaceholders.Count <= 1);
    }

    private void RefreshStickerFlags()
    {
        HasStickers = Stickers.Count > 0;
        StickersOpacity = HasStickers ? 1.0 : 0.0;
        OnPropertyChanged(nameof(HasStickers));
        OnPropertyChanged(nameof(StickersOpacity));
        OnPropertyChanged(nameof(NeedsStickerLookup));
    }

    private void TrySeedStickerFromMeta(Dictionary<string, System.Text.Json.JsonElement> meta)
    {
        foreach (var key in new[] { "sticker_id", "sticker_image_id", "image_id" })
        {
            if (!meta.TryGetValue(key, out var el) || el.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                continue;
            }

            var id = el.GetString();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var ph = StickerPlaceholders.Count > 0 ? StickerPlaceholders[0] : $":meta:{id}:";
            EnsureStickerSlot(ph, id, large: true);
            return;
        }
    }

    private static List<string> ExtractStickerPlaceholders(string content)
    {
        if (string.IsNullOrEmpty(content) || content.IndexOf(':') < 0)
        {
            return [];
        }

        var list = new List<string>();
        foreach (Match m in StickerPlaceholderRegex.Matches(content))
        {
            if (m.Success && !list.Contains(m.Value, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(m.Value);
            }
        }

        return list;
    }

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

        foreach (var key in new[]
                 {
                     "file_id", "picture_id", "image_id", "attachment_id",
                     "sticker_id", "sticker_image_id", "image",
                 })
        {
            if (!meta.TryGetValue(key, out var el))
            {
                continue;
            }

            if (el.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                AddId(el.GetString()!);
            }
            else if (el.ValueKind == System.Text.Json.JsonValueKind.Object
                     && el.TryGetProperty("id", out var nestedId)
                     && nestedId.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                AddId(nestedId.GetString()!);
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

/// <summary>Resolved or pending sticker image inside a chat bubble.</summary>
public partial class MessageStickerViewModel : ObservableObject
{
    public MessageStickerViewModel(string placeholder, string? fileId, bool large)
    {
        Placeholder = placeholder;
        FileId = fileId;
        MaxSide = large ? 160.0 : 72.0;
    }

    public string Placeholder { get; }

    public string? FileId { get; set; }

    /// <summary>Max pixel side — large for sticker-only bubbles, smaller for inline emotes.</summary>
    public double MaxSide { get; }

    [ObservableProperty]
    public partial BitmapImage? Image { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial double ImageOpacity { get; set; }
}
