using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.ViewModels;

/// <summary>Single chat bubble bound in the detail ListView.</summary>
public partial class MessageItemViewModel : ObservableObject
{
    /// <summary>Cap nested ItemsControl children so a single bubble cannot explode layout (#15).</summary>
    private const int MaxVisibleAttachments = 4;
    private const int MaxVisibleStickers = 6;

    /// <summary>Solian sticker markdown: <c>:prefix+slug:</c>.</summary>
    private static readonly Regex StickerPlaceholderRegex = new(
        @":([-\w]*\+[-\w]*):",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FencedCodeRegex = new(
        @"```(?<language>[^`\r\n]*)\r?\n(?<code>[\s\S]*?)```",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WebUrlRegex = new(
        @"https?://[^\s<>""']+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Brush MineBubbleBrush = new LinearGradientBrush
    {
        StartPoint = new Windows.Foundation.Point(0, 0),
        EndPoint = new Windows.Foundation.Point(1, 1),
        GradientStops =
        {
            new GradientStop { Offset = 0, Color = Microsoft.UI.ColorHelper.FromArgb(74, 0, 120, 212) },
            new GradientStop { Offset = 1, Color = Microsoft.UI.ColorHelper.FromArgb(58, 109, 74, 219) },
        },
    };
    private static readonly Brush MineBubbleStroke = new SolidColorBrush(
        Microsoft.UI.ColorHelper.FromArgb(112, 0, 120, 212));
    private static readonly Brush OtherBubbleBrush = new SolidColorBrush(
        Microsoft.UI.ColorHelper.FromArgb(24, 128, 128, 128));
    private static readonly Brush OtherBubbleStroke = new SolidColorBrush(
        Microsoft.UI.ColorHelper.FromArgb(40, 128, 128, 128));

    private bool _playSendAnimation;

    public MessageItemViewModel(
        SnChatMessage message,
        Guid? currentAccountId,
        DysonFileImageLoader imageLoader,
        bool playSendAnimation = false)
    {
        Message = message;
        IsMine = IsSentByCurrentUser(message, currentAccountId);
        _playSendAnimation = playSendAnimation && IsMine;
        Alignment = IsMine ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        var isImageType = string.Equals(message.Type, "image", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message.Type, "media", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message.Type, "sticker", StringComparison.OrdinalIgnoreCase);
        var isVideoType = string.Equals(message.Type, "video", StringComparison.OrdinalIgnoreCase);

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
            var stripped = StripStickerPlaceholders(rawContent).Trim();
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
        RefreshContentBlocks();

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
        IsPending = IsMine && message.Id == Guid.Empty;
        DeliveryStatusText = IsPending ? "同步中" : "已发送";
        ShowSenderName = !IsMine;
        SenderNameOpacity = IsMine ? 0.0 : 0.65;
        BubbleOpacity = IsMine ? 1.0 : 0.95;
        RefreshReactionText();

        AvatarUrl = CloudFileUrlHelper.ResolveAccountAvatar(message.Sender?.Account)
            ?? CloudFileUrlHelper.Resolve(message.Sender?.Account?.Profile?.Picture);
        HasAvatar = !string.IsNullOrWhiteSpace(AvatarUrl);
        // Avatars paint via FastWin2DImage + AvatarUrl (GPU). Initials show underneath until bitmap arrives.
        _ = imageLoader;
        AvatarOpacity = 0.0;
        InitialsOpacity = 1.0;
        Initials = string.IsNullOrWhiteSpace(SenderName) ? "?" : SenderName[..1].ToUpperInvariant();

        // Attachments (hard cap for ItemsControl under virtualized ListView)
        if (message.Attachments is { Count: > 0 })
        {
            var n = 0;
            foreach (var att in message.Attachments)
            {
                if (n >= MaxVisibleAttachments)
                {
                    break;
                }

                var vm = new MessageAttachmentViewModel(att, isVideoType);
                // Force image when message type says so
                if (isImageType && !vm.IsImage && !string.IsNullOrWhiteSpace(vm.FileId ?? vm.Url))
                {
                    Attachments.Add(new MessageAttachmentViewModel(ForceImage(att)));
                }
                else
                {
                    Attachments.Add(vm);
                }

                n++;
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

        // Forwarded message preview. The API already supplies forwarded_message (or an id-only
        // placeholder), but without explicit UI properties the forwarded body becomes invisible.
        if (message.ForwardedMessage is { } forwarded)
        {
            ForwardedSenderName = ResolveSenderName(forwarded, "未知用户");
            ForwardedContent = BuildForwardedContent(forwarded);
            ForwardedTimeText = FormatTime(forwarded.CreatedAt);
            HasForwardedPreview = true;
        }
        else if (message.ForwardedMessageId is { } forwardedId && forwardedId != Guid.Empty)
        {
            ForwardedSenderName = "转发的消息";
            ForwardedContent = "原消息内容暂不可用";
            ForwardedTimeText = string.Empty;
            HasForwardedPreview = true;
        }
        else
        {
            ForwardedSenderName = string.Empty;
            ForwardedContent = string.Empty;
            ForwardedTimeText = string.Empty;
            HasForwardedPreview = false;
        }
    }

    public SnChatMessage Message { get; }

    /// <summary>
    /// Returns true once for a newly sent local echo. ListView recycling and server
    /// reconciliation must not replay the entrance animation.
    /// </summary>
    public bool ConsumeSendAnimation()
    {
        if (!_playSendAnimation)
        {
            return false;
        }

        _playSendAnimation = false;
        return true;
    }

    public Guid MessageId => Message.Id;

    public bool IsMine { get; }

    public bool IsPending { get; }

    public string DeliveryStatusText { get; }

    public Visibility MineChromeVisibility => IsMine ? Visibility.Visible : Visibility.Collapsed;

    public Visibility OtherChromeVisibility => IsMine ? Visibility.Collapsed : Visibility.Visible;

    public Visibility PendingVisibility => IsPending ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SentVisibility => IsMine && !IsPending ? Visibility.Visible : Visibility.Collapsed;

    public CornerRadius BubbleCornerRadius => IsMine
        ? new CornerRadius(18, 18, 5, 18)
        : new CornerRadius(18, 18, 18, 5);

    public double EntranceOffset => IsMine ? 34 : -22;

    public HorizontalAlignment FooterAlignment => IsMine
        ? HorizontalAlignment.Right
        : HorizontalAlignment.Left;

    public Visibility SenderChromeVisibility =>
        IsMine ? Visibility.Collapsed : Visibility.Visible;

    public Brush BubbleBackground => IsMine ? MineBubbleBrush : OtherBubbleBrush;

    public Brush BubbleStroke => IsMine ? MineBubbleStroke : OtherBubbleStroke;

    /// <summary>True when this bubble quotes another message.</summary>
    public bool HasReplyPreview { get; }

    public double ReplyPreviewOpacity { get; }

    public string ReplyPreviewText { get; }

    /// <summary>True when this bubble forwards another chat message.</summary>
    public bool HasForwardedPreview { get; }

    public Visibility ForwardedPreviewVisibility =>
        HasForwardedPreview ? Visibility.Visible : Visibility.Collapsed;

    public string ForwardedSenderName { get; }

    public string ForwardedContent { get; }

    public string ForwardedTimeText { get; }

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

        // Never open /stargate/accounts/{nick} with Chinese display names.
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

    public ObservableCollection<MessageContentBlockViewModel> ContentBlocks { get; } = [];

    public string? LinkPreviewUrl { get; private set; }

    public Visibility LinkPreviewVisibility => string.IsNullOrWhiteSpace(LinkPreviewUrl)
        ? Visibility.Collapsed
        : Visibility.Visible;

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
            ? StripStickerPlaceholders(text ?? string.Empty).Trim()
            : (text ?? string.Empty);
        Content = stripped;
        HasText = !string.IsNullOrWhiteSpace(Content);
        TextOpacity = HasText ? 1.0 : 0.0;
        RefreshContentBlocks();
        OnPropertyChanged(nameof(Content));
        OnPropertyChanged(nameof(HasText));
        OnPropertyChanged(nameof(TextOpacity));
    }

    private void RefreshContentBlocks()
    {
        const int maxCodeBlocksPerMessage = 8;
        ContentBlocks.Clear();
        LinkPreviewUrl = ExtractFirstWebUrl(Content);
        OnPropertyChanged(nameof(LinkPreviewUrl));
        OnPropertyChanged(nameof(LinkPreviewVisibility));
        if (string.IsNullOrEmpty(Content))
        {
            return;
        }

        var cursor = 0;
        var codeBlockCount = 0;
        foreach (Match match in FencedCodeRegex.Matches(Content))
        {
            if (codeBlockCount >= maxCodeBlocksPerMessage)
            {
                // Preserve the remaining markdown as selectable text without creating more cards.
                AddTextBlock(Content[cursor..]);
                return;
            }

            AddTextBlock(Content[cursor..match.Index]);

            var language = match.Groups["language"].Value.Trim();
            var code = match.Groups["code"].Value.TrimEnd('\r', '\n');
            ContentBlocks.Add(MessageContentBlockViewModel.Code(code, language));
            codeBlockCount++;
            cursor = match.Index + match.Length;
        }

        AddTextBlock(Content[cursor..]);
    }

    private void AddTextBlock(string text)
    {
        var normalized = text.Trim('\r', '\n');
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            ContentBlocks.Add(MessageContentBlockViewModel.Text(normalized));
        }
    }

    private static string? ExtractFirstWebUrl(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var codeBlocks = FencedCodeRegex.Matches(content);
        foreach (Match match in WebUrlRegex.Matches(content))
        {
            if (IsInsideCodeBlock(match.Index, codeBlocks))
            {
                continue;
            }

            var candidate = match.Value.TrimEnd('.', ',', '!', '?', ';', ':', ')', ']', '}');
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https")
            {
                return uri.AbsoluteUri;
            }
        }

        return null;
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

        if (Stickers.Count >= MaxVisibleStickers)
        {
            // Cap nested ItemsControl children; still return a slot so callers do not NRE.
            return Stickers[^1];
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

        var codeBlocks = FencedCodeRegex.Matches(content);
        var list = new List<string>();
        foreach (Match m in StickerPlaceholderRegex.Matches(content))
        {
            if (m.Success
                && !IsInsideCodeBlock(m.Index, codeBlocks)
                && !list.Contains(m.Value, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(m.Value);
            }
        }

        return list;
    }

    private static string StripStickerPlaceholders(string content)
    {
        var codeBlocks = FencedCodeRegex.Matches(content);
        return StickerPlaceholderRegex.Replace(
            content,
            match => IsInsideCodeBlock(match.Index, codeBlocks) ? match.Value : string.Empty);
    }

    private static bool IsInsideCodeBlock(int index, MatchCollection codeBlocks)
    {
        foreach (Match codeBlock in codeBlocks)
        {
            if (index >= codeBlock.Index && index < codeBlock.Index + codeBlock.Length)
            {
                return true;
            }
        }

        return false;
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

    private static string ResolveSenderName(SnChatMessage message, string fallback)
        => message.Sender?.Nick
           ?? message.Sender?.Username
           ?? message.Sender?.Account?.Nick
           ?? message.Sender?.Account?.Name
           ?? fallback;

    private static string BuildForwardedContent(SnChatMessage message)
    {
        if (message.DeletedAt is not null)
        {
            return "原消息已删除";
        }

        var body = message.Content?.Trim();
        var attachmentCount = message.Attachments?.Count ?? 0;
        if (string.IsNullOrWhiteSpace(body))
        {
            if (message.IsEncrypted)
            {
                body = "[加密消息]";
            }
            else if (attachmentCount > 0)
            {
                var hasImage = message.Attachments!.Any(CloudFileUrlHelper.IsLikelyImage);
                body = hasImage ? $"[图片 ×{attachmentCount}]" : $"[附件 ×{attachmentCount}]";
            }
            else
            {
                body = message.Type?.ToLowerInvariant() switch
                {
                    "sticker" => "[贴纸]",
                    "audio" or "voice" => "[语音消息]",
                    "video" => "[视频]",
                    _ => "[消息]",
                };
            }
        }
        else if (attachmentCount > 0)
        {
            body += $"\n[附件 ×{attachmentCount}]";
        }

        const int maxLength = 500;
        return body.Length > maxLength ? body[..maxLength] + "…" : body;
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

/// <summary>A plain-text or fenced-code section within one message body.</summary>
public sealed class MessageContentBlockViewModel
{
    private MessageContentBlockViewModel(string content, string language, bool isCode)
    {
        Content = content;
        Language = language;
        TextVisibility = isCode ? Visibility.Collapsed : Visibility.Visible;
        CodeVisibility = isCode ? Visibility.Visible : Visibility.Collapsed;
    }

    public string Content { get; }

    public string Language { get; }

    public Visibility TextVisibility { get; }

    public Visibility CodeVisibility { get; }

    public static MessageContentBlockViewModel Text(string content) => new(content, string.Empty, false);

    public static MessageContentBlockViewModel Code(string content, string language) => new(content, language, true);
}

/// <summary>Resolved or pending sticker image inside a chat bubble (GPU via FileId + FastWin2DImage).</summary>
public partial class MessageStickerViewModel : ObservableObject
{
    public MessageStickerViewModel(string placeholder, string? fileId, bool large)
    {
        Placeholder = placeholder;
        FileId = fileId;
        MaxSide = large ? 160.0 : 72.0;
    }

    public string Placeholder { get; }

    /// <summary>DysonFS file id / URL for <c>FastWin2DImage.Source</c>.</summary>
    [ObservableProperty]
    public partial string? FileId { get; set; }

    /// <summary>Max layout side — large for sticker-only bubbles, smaller for inline emotes.</summary>
    public double MaxSide { get; }

    /// <summary>Legacy BitmapImage slot (unused on GPU path; kept for compile compatibility).</summary>
    [ObservableProperty]
    public partial BitmapImage? Image { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial double ImageOpacity { get; set; }
}
