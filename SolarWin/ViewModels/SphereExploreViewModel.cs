using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>
/// Sphere explore hub: publishers, subscriptions, bookmarks, drafts/featured,
/// tags/categories, stickers, awards/sponsor.
/// </summary>
public partial class SphereExploreViewModel : ObservableObject
{
    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;
    private readonly IAuthService _auth;

    public SphereExploreViewModel(
        ISolarApiClient api,
        IToastService toast,
        DysonFileImageLoader imageLoader,
        IAuthService auth)
    {
        _api = api;
        _toast = toast;
        _imageLoader = imageLoader;
        _auth = auth;
    }

    public ObservableCollection<SocialListItemViewModel> MyPublishers { get; } = [];
    public ObservableCollection<SocialListItemViewModel> SearchPublishers { get; } = [];
    public ObservableCollection<SocialListItemViewModel> PublisherInvites { get; } = [];
    public ObservableCollection<SocialListItemViewModel> PublisherSubscriptions { get; } = [];
    public ObservableCollection<SocialListItemViewModel> PostSubscriptions { get; } = [];
    public ObservableCollection<PostItemViewModel> Bookmarks { get; } = [];
    public ObservableCollection<PostItemViewModel> Drafts { get; } = [];
    public ObservableCollection<PostItemViewModel> Featured { get; } = [];
    public ObservableCollection<SocialListItemViewModel> Tags { get; } = [];
    public ObservableCollection<SocialListItemViewModel> Categories { get; } = [];
    public ObservableCollection<SocialListItemViewModel> Collections { get; } = [];
    public ObservableCollection<StickerPackItemViewModel> StickerPacks { get; } = [];
    public ObservableCollection<StickerPackItemViewModel> StickerSearch { get; } = [];
    public ObservableCollection<StickerItemViewModel> StickerSearchHits { get; } = [];
    public ObservableCollection<StickerItemViewModel> OpenPackStickers { get; } = [];
    public ObservableCollection<SocialListItemViewModel> Awards { get; } = [];

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    [ObservableProperty]
    public partial string PublisherSearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPublisherName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPublisherNick { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CollectionPublisherName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StickerSearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OpenPackTitle { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpenPackVisibility))]
    public partial bool HasOpenPack { get; set; }

    public Microsoft.UI.Xaml.Visibility OpenPackVisibility =>
        HasOpenPack ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    [ObservableProperty]
    public partial string AwardPostIdText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AwardAmountText { get; set; } = "1";

    [ObservableProperty]
    public partial string AwardMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SponsorAmountText { get; set; } = "1";

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    public event EventHandler<PublisherNavArgs>? NavigateToPublisher;
    public event EventHandler<PostItemViewModel>? NavigateToPost;
    public event EventHandler<PostFeedNavArgs>? NavigateToFeed;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            InfoMessage = null;
            await Safe(LoadPublishersAsync).ConfigureAwait(true);
            await Safe(LoadSubscriptionsAsync).ConfigureAwait(true);
            await Safe(LoadBookmarksAsync).ConfigureAwait(true);
            await Safe(LoadDraftsAndFeaturedAsync).ConfigureAwait(true);
            await Safe(LoadTagsCategoriesAsync).ConfigureAwait(true);
            await Safe(LoadStickersAsync).ConfigureAwait(true);
            StatusText = "已刷新";
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadPublishersAsync()
    {
        MyPublishers.Clear();
        PublisherInvites.Clear();

        var mine = await _api.GetMyPublishersAsync().ConfigureAwait(true);
        foreach (var p in mine)
        {
            MyPublishers.Add(ToPublisherItem(p));
        }

        try
        {
            var invites = await _api.GetPublisherInvitesAsync().ConfigureAwait(true);
            foreach (var m in invites)
            {
                var name = m.Publisher?.Name ?? m.PublisherId.ToString("D")[..8];
                PublisherInvites.Add(new SocialListItemViewModel(
                    name,
                    m.Publisher?.Nick ?? name,
                    $"角色 {m.Role}",
                    "待处理邀请",
                    m)
                {
                    Slug = m.Publisher?.Name,
                });
            }
        }
        catch (SolarApiException)
        {
        }
    }

    private async Task LoadSubscriptionsAsync()
    {
        PublisherSubscriptions.Clear();
        PostSubscriptions.Clear();

        try
        {
            var pubs = await _api.GetMyPublisherSubscriptionsAsync().ConfigureAwait(true);
            foreach (var s in pubs)
            {
                var p = s.Publisher;
                PublisherSubscriptions.Add(new SocialListItemViewModel(
                    s.Id.ToString("D"),
                    p?.Nick ?? p?.Name ?? s.PublisherId.ToString("D")[..8],
                    p?.Name is { } n ? $"@{n}" : "",
                    s.IsActive ? "订阅中" : "已结束",
                    s)
                {
                    Slug = p?.Name,
                });
            }
        }
        catch (SolarApiException ex)
        {
            InfoMessage = Append(InfoMessage, "发布者订阅: " + ex.Message);
        }

        try
        {
            var posts = await _api.GetMyPostSubscriptionsAsync().ConfigureAwait(true);
            foreach (var s in posts)
            {
                PostSubscriptions.Add(new SocialListItemViewModel(
                    s.PostId.ToString("D"),
                    s.PostId.ToString("D")[..8] + "…",
                    "帖子订阅",
                    s.CreatedAt?.ToLocalTime().ToString("g") ?? "",
                    s));
            }
        }
        catch (SolarApiException)
        {
        }
    }

    private async Task LoadBookmarksAsync()
    {
        Bookmarks.Clear();
        var list = await _api.GetBookmarkedPostsAsync(take: 40).ConfigureAwait(true);
        foreach (var post in list)
        {
            Bookmarks.Add(new PostItemViewModel(post, _imageLoader));
        }
    }

    private async Task LoadDraftsAndFeaturedAsync()
    {
        Drafts.Clear();
        Featured.Clear();

        try
        {
            var drafts = await _api.GetDraftPostsAsync(take: 30).ConfigureAwait(true);
            foreach (var post in drafts)
            {
                Drafts.Add(new PostItemViewModel(post, _imageLoader));
            }
        }
        catch (SolarApiException ex)
        {
            InfoMessage = Append(InfoMessage, "草稿: " + ex.Message);
        }

        try
        {
            var featured = await _api.GetFeaturedPostsAsync().ConfigureAwait(true);
            foreach (var post in featured)
            {
                Featured.Add(new PostItemViewModel(post, _imageLoader));
            }
        }
        catch (SolarApiException)
        {
        }
    }

    private async Task LoadTagsCategoriesAsync()
    {
        Tags.Clear();
        Categories.Clear();

        try
        {
            var tags = await _api.GetPostTagsAsync().ConfigureAwait(true);
            foreach (var t in tags.Take(80))
            {
                Tags.Add(new SocialListItemViewModel(
                    t.Slug ?? t.Id.ToString("D"),
                    t.Name ?? t.Slug ?? "标签",
                    t.Description ?? "",
                    $"用法 {t.Usage}" + (t.IsProtected ? " · 保护" : ""),
                    t)
                {
                    Slug = t.Slug,
                });
            }
        }
        catch (SolarApiException)
        {
        }

        try
        {
            var cats = await _api.GetPostCategoriesAsync().ConfigureAwait(true);
            foreach (var c in cats.Take(80))
            {
                Categories.Add(new SocialListItemViewModel(
                    c.Slug ?? c.Id.ToString("D"),
                    c.Name ?? c.Slug ?? "分类",
                    c.Slug ?? "",
                    $"用法 {c.Usage}",
                    c)
                {
                    Slug = c.Slug,
                });
            }
        }
        catch (SolarApiException)
        {
        }
    }

    private async Task LoadStickersAsync()
    {
        StickerPacks.Clear();
        try
        {
            var packs = await _api.GetMyStickerPacksAsync().ConfigureAwait(true);
            foreach (var o in packs)
            {
                var item = new StickerPackItemViewModel(o, _imageLoader);
                StickerPacks.Add(item);
                _ = item.LoadIconAsync();
            }

            if (packs.Count == 0)
            {
                InfoMessage = Append(InfoMessage, "贴纸: 暂无已拥有的贴纸包，可搜索后添加");
            }
        }
        catch (SolarApiException ex)
        {
            InfoMessage = Append(InfoMessage, "贴纸: " + ex.Message);
        }
    }

    [RelayCommand]
    private async Task SearchPublishersAsync()
    {
        SearchPublishers.Clear();
        if (string.IsNullOrWhiteSpace(PublisherSearchQuery))
        {
            return;
        }

        try
        {
            IsBusy = true;
            var list = await _api.SearchPublishersAsync(PublisherSearchQuery.Trim()).ConfigureAwait(true);
            foreach (var p in list)
            {
                SearchPublishers.Add(ToPublisherItem(p));
            }
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenPublisher(SocialListItemViewModel? item)
    {
        var name = item?.Slug ?? (item?.Payload as SnPublisher)?.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            _toast.Warning("缺少发布者名称");
            return;
        }

        NavigateToPublisher?.Invoke(this, new PublisherNavArgs(name, item?.Title));
    }

    [RelayCommand]
    private async Task CreatePublisherAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPublisherName))
        {
            _toast.Warning("请填写发布者 name");
            return;
        }

        try
        {
            await _api.CreateIndividualPublisherAsync(new PublisherRequest
            {
                Name = NewPublisherName.Trim(),
                Nick = string.IsNullOrWhiteSpace(NewPublisherNick) ? NewPublisherName.Trim() : NewPublisherNick.Trim(),
            }).ConfigureAwait(true);
            _toast.Success("已创建发布者");
            NewPublisherName = string.Empty;
            NewPublisherNick = string.Empty;
            await LoadPublishersAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task AcceptPublisherInviteAsync(SocialListItemViewModel? item)
    {
        var name = item?.Slug;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            await _api.AcceptPublisherInviteAsync(name).ConfigureAwait(true);
            _toast.Success("已接受邀请");
            await LoadPublishersAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeclinePublisherInviteAsync(SocialListItemViewModel? item)
    {
        var name = item?.Slug;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            await _api.DeclinePublisherInviteAsync(name).ConfigureAwait(true);
            _toast.Success("已拒绝邀请");
            await LoadPublishersAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task UnsubscribePublisherAsync(SocialListItemViewModel? item)
    {
        var name = item?.Slug;
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            await _api.UnsubscribePublisherAsync(name).ConfigureAwait(true);
            _toast.Success("已取消订阅");
            await LoadSubscriptionsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private void OpenPost(PostItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        NavigateToPost?.Invoke(this, item);
    }

    [RelayCommand]
    private async Task UnbookmarkPostAsync(PostItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await _api.UnbookmarkPostAsync(item.Post.Id).ConfigureAwait(true);
            Bookmarks.Remove(item);
            _toast.Success("已取消书签");
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private void OpenTagFeed(SocialListItemViewModel? item)
    {
        var slug = item?.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        NavigateToFeed?.Invoke(
            this,
            new PostFeedNavArgs(PostFeedKind.Tag, slug, item?.Title ?? slug));
    }

    [RelayCommand]
    private void OpenCategoryFeed(SocialListItemViewModel? item)
    {
        var slug = item?.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        NavigateToFeed?.Invoke(
            this,
            new PostFeedNavArgs(PostFeedKind.Category, slug, item?.Title ?? slug));
    }

    [RelayCommand]
    private void OpenCollectionFeed(SocialListItemViewModel? item)
    {
        var slug = item?.Slug;
        var pub = CollectionPublisherName?.Trim();
        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(pub))
        {
            _toast.Warning("需要发布者 name 与合集 slug");
            return;
        }

        NavigateToFeed?.Invoke(
            this,
            new PostFeedNavArgs(PostFeedKind.Collection, slug, item?.Title, pub));
    }

    [RelayCommand]
    private async Task SubscribeTagAsync(SocialListItemViewModel? item)
    {
        var slug = item?.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        try
        {
            await _api.SubscribeTagAsync(slug).ConfigureAwait(true);
            _toast.Success($"已订阅标签 {slug}");
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task UnsubscribeTagAsync(SocialListItemViewModel? item)
    {
        var slug = item?.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        try
        {
            await _api.UnsubscribeTagAsync(slug).ConfigureAwait(true);
            _toast.Success($"已取消标签订阅 {slug}");
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SubscribeCategoryAsync(SocialListItemViewModel? item)
    {
        var slug = item?.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        try
        {
            await _api.SubscribeCategoryAsync(slug).ConfigureAwait(true);
            _toast.Success($"已订阅分类 {slug}");
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task UnsubscribeCategoryAsync(SocialListItemViewModel? item)
    {
        var slug = item?.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        try
        {
            await _api.UnsubscribeCategoryAsync(slug).ConfigureAwait(true);
            _toast.Success($"已取消分类订阅 {slug}");
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task LoadCollectionsAsync()
    {
        Collections.Clear();
        if (string.IsNullOrWhiteSpace(CollectionPublisherName))
        {
            _toast.Warning("请填写发布者 name");
            return;
        }

        try
        {
            IsBusy = true;
            var list = await _api.GetPublisherCollectionsAsync(CollectionPublisherName.Trim()).ConfigureAwait(true);
            foreach (var c in list)
            {
                Collections.Add(new SocialListItemViewModel(
                    c.Slug ?? c.Id.ToString("D"),
                    c.Name ?? c.Slug ?? "合集",
                    c.Description ?? "",
                    $"条目 {c.ItemCount}",
                    c)
                {
                    Slug = c.Slug,
                });
            }
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubscribeCollectionAsync(SocialListItemViewModel? item)
    {
        var pub = CollectionPublisherName.Trim();
        var slug = item?.Slug;
        if (string.IsNullOrWhiteSpace(pub) || string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        try
        {
            await _api.SubscribeCollectionAsync(pub, slug).ConfigureAwait(true);
            _toast.Success("已订阅合集");
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SearchStickersAsync()
    {
        StickerSearch.Clear();
        StickerSearchHits.Clear();
        try
        {
            IsBusy = true;
            var query = StickerSearchQuery ?? string.Empty;

            // Packs: GET /sphere/stickers (icon on pack)
            var packs = await _api.SearchStickerPacksAsync(query).ConfigureAwait(true);
            foreach (var pack in packs)
            {
                var item = new StickerPackItemViewModel(pack, _imageLoader);
                StickerSearch.Add(item);
                _ = item.LoadIconAsync();
            }

            // Stickers: GET /sphere/stickers/search (image on each sticker)
            try
            {
                var stickers = await _api.SearchStickersAsync(query).ConfigureAwait(true);
                foreach (var s in stickers.OrderBy(x => x.Order).ThenBy(x => x.Name))
                {
                    var vm = new StickerItemViewModel(s, _imageLoader);
                    StickerSearchHits.Add(vm);
                    _ = vm.LoadImageAsync();
                }
            }
            catch (SolarApiException)
            {
                // Pack search still useful even if sticker search fails.
            }

            if (packs.Count == 0 && StickerSearchHits.Count == 0)
            {
                _toast.Show("没有找到贴纸包或贴纸");
            }
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenStickerPackAsync(StickerPackItemViewModel? item)
    {
        if (item is null || item.PackId == Guid.Empty)
        {
            return;
        }

        await LoadPackContentAsync(item.PackId, item.Title).ConfigureAwait(true);
    }

    public void OpenPackFromStickerHit(StickerItemViewModel? sticker)
    {
        if (sticker is null || sticker.PackId == Guid.Empty)
        {
            return;
        }

        _ = LoadPackContentAsync(sticker.PackId, sticker.Title);
    }

    private async Task LoadPackContentAsync(Guid packId, string? title)
    {
        if (packId == Guid.Empty)
        {
            return;
        }

        try
        {
            IsBusy = true;
            OpenPackStickers.Clear();
            OpenPackTitle = (string.IsNullOrWhiteSpace(title) ? "贴纸包" : title) + " · 贴纸内容";
            HasOpenPack = true;
            var stickers = await _api.GetStickerPackContentAsync(packId).ConfigureAwait(true);
            foreach (var s in stickers.OrderBy(x => x.Order).ThenBy(x => x.Name))
            {
                var vm = new StickerItemViewModel(s, _imageLoader);
                OpenPackStickers.Add(vm);
                _ = vm.LoadImageAsync();
            }

            if (stickers.Count == 0)
            {
                _toast.Show("该贴纸包没有内容或无权查看");
            }
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.ApiMessage ?? ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CloseOpenPack()
    {
        HasOpenPack = false;
        OpenPackStickers.Clear();
        OpenPackTitle = string.Empty;
    }

    [RelayCommand]
    private async Task OwnStickerPackAsync(StickerPackItemViewModel? item)
    {
        var packId = item?.PackId ?? Guid.Empty;
        if (packId == Guid.Empty)
        {
            return;
        }

        try
        {
            await _api.OwnStickerPackAsync(packId).ConfigureAwait(true);
            _toast.Success("已添加贴纸包");
            await LoadStickersAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task LoadAwardsAsync()
    {
        Awards.Clear();
        if (!Guid.TryParse(AwardPostIdText.Trim(), out var postId))
        {
            _toast.Warning("请填写帖子 GUID");
            return;
        }

        try
        {
            IsBusy = true;
            var list = await _api.GetPostAwardsAsync(postId).ConfigureAwait(true);
            foreach (var a in list)
            {
                Awards.Add(new SocialListItemViewModel(
                    a.Id.ToString("D"),
                    $"金额 {a.Amount:0.##}",
                    a.Message ?? "",
                    a.CreatedAt?.ToLocalTime().ToString("g") ?? "",
                    a));
            }
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AwardPostAsync()
    {
        if (!Guid.TryParse(AwardPostIdText.Trim(), out var postId))
        {
            _toast.Warning("请填写帖子 GUID");
            return;
        }

        if (!double.TryParse(AwardAmountText.Trim(), out var amount) || amount <= 0)
        {
            _toast.Warning("请填写有效金额");
            return;
        }

        try
        {
            await _api.AwardPostAsync(postId, new PostAwardRequest
            {
                Amount = amount,
                Attitude = 0,
                Message = string.IsNullOrWhiteSpace(AwardMessage) ? null : AwardMessage.Trim(),
            }).ConfigureAwait(true);
            _toast.Success("打赏成功");
            await LoadAwardsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SponsorPostAsync()
    {
        if (!Guid.TryParse(AwardPostIdText.Trim(), out var postId))
        {
            _toast.Warning("请填写帖子 GUID");
            return;
        }

        if (!double.TryParse(SponsorAmountText.Trim(), out var amount) || amount <= 0)
        {
            _toast.Warning("请填写有效赞助金额");
            return;
        }

        try
        {
            await _api.SponsorPostAsync(postId, new PostSponsorRequest { Amount = amount }).ConfigureAwait(true);
            _toast.Success("赞助成功");
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    private static SocialListItemViewModel ToPublisherItem(SnPublisher p)
        => new(
            p.Id.ToString("D"),
            p.Nick ?? p.Name ?? "发布者",
            p.Name is { } n ? $"@{n}" : "",
            $"{p.Type} · 评分 {p.Rating:0.#}",
            p)
        {
            Slug = p.Name,
        };

    private static async Task Safe(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (SolarApiException)
        {
        }
        catch (Exception)
        {
        }
    }

    private static string Append(string? existing, string next)
        => string.IsNullOrWhiteSpace(existing) ? next : existing + "\n" + next;
}
