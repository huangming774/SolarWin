using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.ViewModels;

/// <summary>One row in the post feed (also reused for replies).</summary>
public partial class PostItemViewModel : ObservableObject
{
    /// <summary>
    /// Server default reaction symbols (not emoji): thumb_up, heart, clap, …
    /// Custom symbols require a subscription.
    /// </summary>
    public const string DefaultLikeSymbol = "thumb_up";

    public PostItemViewModel(SnPost post, DysonFileImageLoader imageLoader)
    {
        Post = post;
        Id = post.Id;
        ApplyPost(post, imageLoader);
    }

    public SnPost Post { get; private set; }

    public Guid Id { get; private set; }

    public string AuthorName { get; private set; } = string.Empty;

    public string AuthorHandle { get; private set; } = string.Empty;

    public string? AvatarUrl { get; private set; }

    public bool HasAvatar { get; private set; }

    [ObservableProperty]
    public partial BitmapImage? AvatarImage { get; set; }

    public string Initials => AuthorName.Length > 0 ? AuthorName[..1].ToUpperInvariant() : "?";

    public string Title { get; private set; } = string.Empty;

    public bool HasTitle { get; private set; }

    public Visibility TitleVisibility => HasTitle ? Visibility.Visible : Visibility.Collapsed;

    public string ContentText { get; private set; } = string.Empty;

    public string? ForwardedText { get; private set; }

    public bool HasForwarded => !string.IsNullOrWhiteSpace(ForwardedText);

    public Visibility ForwardedVisibility => HasForwarded ? Visibility.Visible : Visibility.Collapsed;

    public string TimeText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StatsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int RepliesCount { get; set; }

    [ObservableProperty]
    public partial int BoostCount { get; set; }

    [ObservableProperty]
    public partial int Upvotes { get; set; }

    [ObservableProperty]
    public partial bool IsLiked { get; set; }

    [ObservableProperty]
    public partial bool IsBookmarked { get; set; }

    [ObservableProperty]
    public partial bool IsBoosted { get; set; }

    public string LikeLabel => IsLiked ? $"赞 {Upvotes}" : (Upvotes > 0 ? $"赞 {Upvotes}" : "赞");

    public string BoostLabel => BoostCount > 0 ? $"转发 {BoostCount}" : "转发";

    public string ReplyLabel => RepliesCount > 0 ? $"回复 {RepliesCount}" : "回复";

    public string BookmarkLabel => IsBookmarked ? "已收藏" : "收藏";

    public List<string> ImageUrls { get; private set; } = [];

    public bool HasImages { get; private set; }

    public Visibility ImagesVisibility => HasImages ? Visibility.Visible : Visibility.Collapsed;

    public bool HasExtraImages { get; private set; }

    public Visibility ExtraImagesVisibility => HasExtraImages ? Visibility.Visible : Visibility.Collapsed;

    public string ExtraImagesText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial BitmapImage? FirstImage { get; set; }

    /// <summary>Indent for threaded replies (0 = top-level).</summary>
    public int Depth { get; set; }

    public Thickness ReplyIndent => new(Math.Min(Depth, 6) * 16, 0, 0, 0);

    partial void OnIsLikedChanged(bool value)
    {
        OnPropertyChanged(nameof(LikeLabel));
    }

    partial void OnUpvotesChanged(int value)
    {
        OnPropertyChanged(nameof(LikeLabel));
        RefreshStatsText();
    }

    partial void OnBoostCountChanged(int value)
    {
        OnPropertyChanged(nameof(BoostLabel));
        RefreshStatsText();
    }

    partial void OnRepliesCountChanged(int value)
    {
        OnPropertyChanged(nameof(ReplyLabel));
        RefreshStatsText();
    }

    partial void OnIsBookmarkedChanged(bool value)
    {
        OnPropertyChanged(nameof(BookmarkLabel));
    }

    partial void OnIsBoostedChanged(bool value)
    {
        OnPropertyChanged(nameof(BoostLabel));
    }

    /// <summary>Refresh display fields from a newer server post payload.</summary>
    public void UpdateFrom(SnPost post, DysonFileImageLoader? imageLoader = null)
    {
        Post = post;
        Id = post.Id;
        ApplyPost(post, imageLoader);
        OnPropertyChanged(nameof(AuthorName));
        OnPropertyChanged(nameof(AuthorHandle));
        OnPropertyChanged(nameof(Initials));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(HasTitle));
        OnPropertyChanged(nameof(TitleVisibility));
        OnPropertyChanged(nameof(ContentText));
        OnPropertyChanged(nameof(ForwardedText));
        OnPropertyChanged(nameof(HasForwarded));
        OnPropertyChanged(nameof(ForwardedVisibility));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(HasImages));
        OnPropertyChanged(nameof(ImagesVisibility));
        OnPropertyChanged(nameof(ExtraImagesText));
        OnPropertyChanged(nameof(HasExtraImages));
        OnPropertyChanged(nameof(ExtraImagesVisibility));
        OnPropertyChanged(nameof(LikeLabel));
        OnPropertyChanged(nameof(BoostLabel));
        OnPropertyChanged(nameof(ReplyLabel));
        OnPropertyChanged(nameof(BookmarkLabel));
    }

    public void ApplyInteractionCounts(SnPost post)
    {
        RepliesCount = post.RepliesCount;
        BoostCount = post.BoostCount;
        Upvotes = post.Upvotes;
        IsBookmarked = post.IsBookmarked;
        IsLiked = HasReaction(post, DefaultLikeSymbol) || post.Upvotes > 0 && HasAnyPositiveReaction(post);
        RefreshStatsText();
    }

    private void ApplyPost(SnPost post, DysonFileImageLoader? imageLoader)
    {
        var publisher = post.Publisher;
        AuthorName = publisher?.Nick ?? publisher?.Name ?? "未知发布者";
        AuthorHandle = string.IsNullOrWhiteSpace(publisher?.Name) ? string.Empty : $"@{publisher.Name}";
        AvatarUrl = CloudFileUrlHelper.Resolve(publisher?.Picture);
        HasAvatar = !string.IsNullOrWhiteSpace(AvatarUrl);

        if (imageLoader is not null && HasAvatar && AvatarImage is null &&
            imageLoader.TryGetCached(AvatarUrl, out var cachedAvatar))
        {
            AvatarImage = cachedAvatar;
        }

        Title = post.Title ?? string.Empty;
        HasTitle = !string.IsNullOrWhiteSpace(post.Title);
        ContentText = post.Content ?? post.Description ?? string.Empty;

        ForwardedText = null;
        if (post.ForwardedPost is { } forwarded)
        {
            var fwdAuthor = forwarded.Publisher?.Nick ?? forwarded.Publisher?.Name ?? "未知";
            var fwdContent = forwarded.Content ?? forwarded.Description ?? string.Empty;
            if (fwdContent.Length > 200)
            {
                fwdContent = fwdContent[..200] + "…";
            }

            ForwardedText = $"转发自 {fwdAuthor}:{fwdContent}";
        }

        TimeText = FormatTime(post.PublishedAt ?? post.CreatedAt);
        RepliesCount = post.RepliesCount;
        BoostCount = post.BoostCount;
        Upvotes = post.Upvotes;
        IsBookmarked = post.IsBookmarked;
        IsLiked = HasReaction(post, DefaultLikeSymbol) || HasAnyPositiveReaction(post);
        // Boosted state is not returned on SnPost; UI may set IsBoosted after local action.
        RefreshStatsText();

        ImageUrls = (post.Attachments ?? [])
            .Where(CloudFileUrlHelper.IsLikelyImage)
            .Select(CloudFileUrlHelper.Resolve)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Cast<string>()
            .ToList();
        HasImages = ImageUrls.Count > 0;
        if (imageLoader is not null && HasImages && FirstImage is null &&
            imageLoader.TryGetCached(ImageUrls[0], out var cachedImage))
        {
            FirstImage = cachedImage;
        }

        ExtraImagesText = ImageUrls.Count > 1 ? $"+{ImageUrls.Count - 1}" : string.Empty;
        HasExtraImages = ImageUrls.Count > 1;
    }

    private void RefreshStatsText()
    {
        StatsText = $"回复 {RepliesCount} · 转发 {BoostCount} · 赞 {Upvotes}";
    }

    private static bool HasReaction(SnPost post, string symbol)
    {
        if (post.ReactionsMade is null)
        {
            return false;
        }

        return post.ReactionsMade.TryGetValue(symbol, out var made) && made;
    }

    private static bool HasAnyPositiveReaction(SnPost post)
    {
        if (post.ReactionsMade is null)
        {
            return false;
        }

        return post.ReactionsMade.Any(kv => kv.Value);
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
