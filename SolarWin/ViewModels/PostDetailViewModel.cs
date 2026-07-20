using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;

namespace SolarWin.ViewModels;

/// <summary>Full single-post view: untruncated content, all images.</summary>
public partial class PostDetailViewModel : ObservableObject
{
    private readonly DysonFileImageLoader _imageLoader;

    public PostDetailViewModel(DysonFileImageLoader imageLoader)
    {
        _imageLoader = imageLoader;
    }

    public ObservableCollection<BitmapImage> Images { get; } = [];

    [ObservableProperty]
    public partial BitmapImage? AvatarImage { get; set; }

    [ObservableProperty]
    public partial string AuthorName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AuthorHandle { get; set; } = string.Empty;

    public string Initials => AuthorName.Length > 0 ? AuthorName[..1].ToUpperInvariant() : "?";

    [ObservableProperty]
    public partial string TimeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility TitleVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial string ContentText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ForwardedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility ForwardedVisibility { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial string StatsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Visibility ImagesVisibility { get; set; } = Visibility.Collapsed;

    public void Initialize(PostItemViewModel item)
    {
        var post = item.Post;

        AuthorName = item.AuthorName;
        OnPropertyChanged(nameof(Initials));
        AuthorHandle = item.AuthorHandle;
        AvatarImage = item.AvatarImage;
        Title = item.Title;
        TitleVisibility = item.TitleVisibility;
        ContentText = post.Content ?? post.Description ?? string.Empty;
        StatsText = item.StatsText;

        var time = post.PublishedAt ?? post.CreatedAt;
        TimeText = time?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? string.Empty;

        if (post.ForwardedPost is { } forwarded)
        {
            var fwdAuthor = forwarded.Publisher?.Nick ?? forwarded.Publisher?.Name ?? "未知";
            var fwdContent = forwarded.Content ?? forwarded.Description ?? string.Empty;
            ForwardedText = $"转发自 {fwdAuthor}:{fwdContent}";
            ForwardedVisibility = Visibility.Visible;
        }

        ImagesVisibility = item.HasImages ? Visibility.Visible : Visibility.Collapsed;

        _ = LoadImagesAsync(item);
    }

    private async Task LoadImagesAsync(PostItemViewModel item)
    {
        if (AvatarImage is null && !string.IsNullOrWhiteSpace(item.AvatarUrl))
        {
            var bmp = await _imageLoader.LoadAsync(item.AvatarUrl).ConfigureAwait(true);
            if (bmp is not null)
            {
                AvatarImage = bmp;
                item.AvatarImage ??= bmp;
            }
        }

        foreach (var url in item.ImageUrls)
        {
            var bmp = await _imageLoader.LoadAsync(url).ConfigureAwait(true);
            if (bmp is not null)
            {
                Images.Add(bmp);
            }
        }
    }
}
