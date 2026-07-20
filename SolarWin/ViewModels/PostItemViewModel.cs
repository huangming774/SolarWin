using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.ViewModels;

/// <summary>One row in the post feed.</summary>
public partial class PostItemViewModel : ObservableObject
{
    public PostItemViewModel(SnPost post, DysonFileImageLoader imageLoader)
    {
        Post = post;
        Id = post.Id;

        var publisher = post.Publisher;
        AuthorName = publisher?.Nick ?? publisher?.Name ?? "未知发布者";
        AuthorHandle = string.IsNullOrWhiteSpace(publisher?.Name) ? string.Empty : $"@{publisher.Name}";
        AvatarUrl = CloudFileUrlHelper.Resolve(publisher?.Picture);
        HasAvatar = !string.IsNullOrWhiteSpace(AvatarUrl);
        // Never bind UriSource directly: drive/files requires Bearer auth, and a failed
        // unauthenticated BitmapImage would block the authenticated fallback in LoadImagesAsync.
        if (HasAvatar && imageLoader.TryGetCached(AvatarUrl, out var cachedAvatar))
        {
            AvatarImage = cachedAvatar;
        }

        Title = post.Title ?? string.Empty;
        HasTitle = !string.IsNullOrWhiteSpace(post.Title);
        ContentText = post.Content ?? post.Description ?? string.Empty;

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
        StatsText = $"回复 {post.RepliesCount} · 转发 {post.BoostCount} · 赞 {post.Upvotes}";

        ImageUrls = (post.Attachments ?? [])
            .Where(CloudFileUrlHelper.IsLikelyImage)
            .Select(CloudFileUrlHelper.Resolve)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Cast<string>()
            .ToList();
        HasImages = ImageUrls.Count > 0;
        if (HasImages && imageLoader.TryGetCached(ImageUrls[0], out var cachedImage))
        {
            FirstImage = cachedImage;
        }

        ExtraImagesText = ImageUrls.Count > 1 ? $"+{ImageUrls.Count - 1}" : string.Empty;
        HasExtraImages = ImageUrls.Count > 1;
    }

    public SnPost Post { get; }

    public Guid Id { get; }

    public string AuthorName { get; }

    public string AuthorHandle { get; }

    public string? AvatarUrl { get; }

    public bool HasAvatar { get; }

    [ObservableProperty]
    public partial BitmapImage? AvatarImage { get; set; }

    public string Initials => AuthorName.Length > 0 ? AuthorName[..1].ToUpperInvariant() : "?";

    public string Title { get; }

    public bool HasTitle { get; }

    public Visibility TitleVisibility => HasTitle ? Visibility.Visible : Visibility.Collapsed;

    public string ContentText { get; }

    public string? ForwardedText { get; }

    public bool HasForwarded => !string.IsNullOrWhiteSpace(ForwardedText);

    public Visibility ForwardedVisibility => HasForwarded ? Visibility.Visible : Visibility.Collapsed;

    public string TimeText { get; }

    public string StatsText { get; }

    public List<string> ImageUrls { get; }

    public bool HasImages { get; }

    public Visibility ImagesVisibility => HasImages ? Visibility.Visible : Visibility.Collapsed;

    public bool HasExtraImages { get; }

    public Visibility ExtraImagesVisibility => HasExtraImages ? Visibility.Visible : Visibility.Collapsed;

    public string ExtraImagesText { get; }

    [ObservableProperty]
    public partial BitmapImage? FirstImage { get; set; }

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
