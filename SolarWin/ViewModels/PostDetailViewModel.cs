using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>Full single-post view: detail fetch, interactions, replies.</summary>
public partial class PostDetailViewModel : ObservableObject
{
    private const int ReplyPageSize = 20;

    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;
    private readonly IAuthService _auth;

    private Guid _postId;
    private int _replyOffset;
    private string? _publisherName;
    private bool _publisherResolved;
    private bool _busyAction;

    public PostDetailViewModel(
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

    public ObservableCollection<BitmapImage> Images { get; } = [];

    public ObservableCollection<PostItemViewModel> Replies { get; } = [];

    [ObservableProperty]
    public partial BitmapImage? AvatarImage { get; set; }

    [ObservableProperty]
    public partial string AuthorName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AuthorHandle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? AuthorAccountName { get; set; }

    [ObservableProperty]
    public partial Guid? AuthorAccountId { get; set; }

    [ObservableProperty]
    public partial string? PublisherName { get; set; }

    public string Initials => AuthorName.Length > 0 ? AuthorName[..1].ToUpperInvariant() : "?";

    public bool CanOpenAuthorProfile =>
        !string.IsNullOrWhiteSpace(AuthorAccountName) || !string.IsNullOrWhiteSpace(PublisherName);

    /// <summary>Page navigates to UserProfilePage.</summary>
    public event EventHandler<UserProfileNavArgs>? NavigateToUserProfile;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyReplies))]
    [NotifyPropertyChangedFor(nameof(EmptyRepliesVisibility))]
    [NotifyPropertyChangedFor(nameof(ShowReplies))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(ErrorVisibility))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingReplies { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoadMoreRepliesVisibility))]
    public partial bool HasMoreReplies { get; set; }

    public Visibility LoadMoreRepliesVisibility => HasMoreReplies ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReply))]
    public partial string ReplyContent { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReply))]
    public partial bool IsReplying { get; set; }

    [ObservableProperty]
    public partial bool IsLiked { get; set; }

    [ObservableProperty]
    public partial bool IsBoosted { get; set; }

    [ObservableProperty]
    public partial bool IsBookmarked { get; set; }

    [ObservableProperty]
    public partial int Upvotes { get; set; }

    [ObservableProperty]
    public partial int BoostCount { get; set; }

    [ObservableProperty]
    public partial int RepliesCount { get; set; }

    [ObservableProperty]
    public partial string LikeButtonText { get; set; } = "赞";

    [ObservableProperty]
    public partial string BoostButtonText { get; set; } = "转发";

    [ObservableProperty]
    public partial string BookmarkButtonText { get; set; } = "收藏";

    [ObservableProperty]
    public partial bool IsPostSubscribed { get; set; }

    [ObservableProperty]
    public partial string SubscribeButtonText { get; set; } = "订阅帖";

    [ObservableProperty]
    public partial string AwardAmountText { get; set; } = "1";

    [ObservableProperty]
    public partial string AwardMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SponsorAmountText { get; set; } = "1";

    [ObservableProperty]
    public partial string AwardsSummary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MonetizeStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PostIdText { get; set; } = string.Empty;

    public ObservableCollection<SocialListItemViewModel> AwardItems { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyReplies))]
    [NotifyPropertyChangedFor(nameof(EmptyRepliesVisibility))]
    [NotifyPropertyChangedFor(nameof(ShowReplies))]
    public partial string RepliesHeader { get; set; } = "回复";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public Visibility ErrorVisibility => HasError ? Visibility.Visible : Visibility.Collapsed;

    public bool IsEmptyReplies => !IsBusy && !IsLoadingReplies && Replies.Count == 0;

    public Visibility EmptyRepliesVisibility => IsEmptyReplies ? Visibility.Visible : Visibility.Collapsed;

    public bool ShowReplies => Replies.Count > 0;

    public bool CanReply => !IsReplying && !string.IsNullOrWhiteSpace(ReplyContent) && _postId != Guid.Empty;

    /// <summary>
    /// Seed from feed row then refresh full detail + replies from API.
    /// </summary>
    public void Initialize(PostItemViewModel item)
    {
        _postId = item.Id;
        ApplyHeader(item);
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_postId == Guid.Empty)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            Images.Clear();
            Replies.Clear();
            _replyOffset = 0;

            SnPost post;
            try
            {
                post = await _api.GetPostAsync(_postId).ConfigureAwait(true);
            }
            catch (SolarApiException)
            {
                // Fall back to seed data already on screen.
                await LoadRepliesInternalAsync(reset: true).ConfigureAwait(true);
                return;
            }

            ApplyPost(post);
            await LoadImagesFromPostAsync(post).ConfigureAwait(true);
            await LoadSubscriptionStateAsync().ConfigureAwait(true);
            await LoadAwardsPreviewAsync().ConfigureAwait(true);
            await LoadRepliesInternalAsync(reset: true).ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("加载帖子失败");
        }
        finally
        {
            IsBusy = false;
            NotifyReplyUi();
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ErrorVisibility));
        }
    }

    [RelayCommand]
    private void OpenAuthorProfile()
    {
        var name = AuthorAccountName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = PublisherName;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            _toast.Warning("无法打开资料：缺少用户名");
            return;
        }

        NavigateToUserProfile?.Invoke(
            this,
            new UserProfileNavArgs(name.TrimStart('@'), AuthorAccountId, AuthorName));
    }

    [RelayCommand]
    private async Task TogglePostSubscribeAsync()
    {
        if (_busyAction || _postId == Guid.Empty)
        {
            return;
        }

        try
        {
            _busyAction = true;
            if (IsPostSubscribed)
            {
                await _api.UnsubscribePostAsync(_postId).ConfigureAwait(true);
                IsPostSubscribed = false;
                SubscribeButtonText = "订阅帖";
                _toast.Success("已取消帖子订阅");
            }
            else
            {
                await _api.SubscribePostAsync(_postId).ConfigureAwait(true);
                IsPostSubscribed = true;
                SubscribeButtonText = "已订阅帖";
                _toast.Success("已订阅本帖更新");
            }
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"订阅失败:{FriendlySphereError(ex)}");
        }
        finally
        {
            _busyAction = false;
        }
    }

    [RelayCommand]
    private async Task AwardPostAsync()
    {
        if (_busyAction || _postId == Guid.Empty)
        {
            return;
        }

        if (!double.TryParse(AwardAmountText.Trim(), out var amount) || amount <= 0)
        {
            _toast.Warning("请填写有效打赏金额");
            return;
        }

        try
        {
            _busyAction = true;
            MonetizeStatus = "打赏中…";
            await _api.AwardPostAsync(_postId, new PostAwardRequest
            {
                Amount = amount,
                Attitude = 0,
                Message = string.IsNullOrWhiteSpace(AwardMessage) ? null : AwardMessage.Trim(),
            }).ConfigureAwait(true);
            MonetizeStatus = $"已打赏 {amount:0.##}";
            _toast.Success("打赏成功");
            AwardMessage = string.Empty;
            await LoadAwardsPreviewAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            MonetizeStatus = FriendlySphereError(ex);
            _toast.Error($"打赏失败:{FriendlySphereError(ex)}");
        }
        finally
        {
            _busyAction = false;
        }
    }

    [RelayCommand]
    private async Task SponsorPostAsync()
    {
        if (_busyAction || _postId == Guid.Empty)
        {
            return;
        }

        if (!double.TryParse(SponsorAmountText.Trim(), out var amount) || amount <= 0)
        {
            _toast.Warning("请填写有效赞助金额");
            return;
        }

        try
        {
            _busyAction = true;
            MonetizeStatus = "赞助中…";
            await _api.SponsorPostAsync(_postId, new PostSponsorRequest { Amount = amount }).ConfigureAwait(true);
            MonetizeStatus = $"已赞助 {amount:0.##}";
            _toast.Success("赞助成功");
        }
        catch (SolarApiException ex)
        {
            MonetizeStatus = FriendlySphereError(ex);
            _toast.Error($"赞助失败:{FriendlySphereError(ex)}");
        }
        finally
        {
            _busyAction = false;
        }
    }

    private async Task LoadSubscriptionStateAsync()
    {
        try
        {
            var sub = await _api.GetPostSubscriptionAsync(_postId).ConfigureAwait(true);
            IsPostSubscribed = sub is not null;
            SubscribeButtonText = IsPostSubscribed ? "已订阅帖" : "订阅帖";
        }
        catch (SolarApiException)
        {
            IsPostSubscribed = false;
            SubscribeButtonText = "订阅帖";
        }
    }

    private async Task LoadAwardsPreviewAsync()
    {
        AwardItems.Clear();
        try
        {
            var list = await _api.GetPostAwardsAsync(_postId, take: 10).ConfigureAwait(true);
            foreach (var a in list)
            {
                AwardItems.Add(new SocialListItemViewModel(
                    a.Id.ToString("D"),
                    $"金额 {a.Amount:0.##}",
                    a.Message ?? "",
                    a.CreatedAt?.ToLocalTime().ToString("g") ?? "",
                    a));
            }

            AwardsSummary = list.Count == 0
                ? "暂无打赏记录"
                : $"最近打赏 {list.Count} 条";
        }
        catch (SolarApiException)
        {
            AwardsSummary = "打赏记录未加载";
        }
    }

    [RelayCommand]
    private async Task LoadMoreRepliesAsync()
    {
        if (IsLoadingReplies || !HasMoreReplies)
        {
            return;
        }

        try
        {
            IsLoadingReplies = true;
            await LoadRepliesInternalAsync(reset: false).ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"加载回复失败:{ex.Message}");
        }
        finally
        {
            IsLoadingReplies = false;
            NotifyReplyUi();
        }
    }

    [RelayCommand]
    private async Task SendReplyAsync()
    {
        var content = ReplyContent.Trim();
        if (content.Length == 0 || IsReplying || _postId == Guid.Empty)
        {
            return;
        }

        try
        {
            IsReplying = true;
            var pub = await ResolvePublisherNameAsync().ConfigureAwait(true);
            var request = new CreatePostRequest
            {
                Content = content,
                Visibility = 0,
                Type = 0,
                RepliedPostId = _postId,
            };

            var created = await _api.CreatePostAsync(request, pub).ConfigureAwait(true);
            Replies.Insert(0, new PostItemViewModel(created, _imageLoader));
            ReplyContent = string.Empty;
            RepliesCount++;
            RepliesHeader = $"回复 ({RepliesCount})";
            RefreshStats();
            _ = LoadReplyImagesAsync();
            _toast.Success("已回复");
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"回复失败:{ex.Message}");
        }
        finally
        {
            IsReplying = false;
            NotifyReplyUi();
            OnPropertyChanged(nameof(CanReply));
        }
    }

    private void NotifyReplyUi()
    {
        OnPropertyChanged(nameof(IsEmptyReplies));
        OnPropertyChanged(nameof(EmptyRepliesVisibility));
        OnPropertyChanged(nameof(ShowReplies));
    }

    [RelayCommand]
    private async Task ToggleLikeAsync()
    {
        if (_busyAction || _postId == Guid.Empty)
        {
            return;
        }

        try
        {
            _busyAction = true;
            // Same symbol POST toggles: add → 200, remove → 204.
            var reaction = await _api.ReactToPostAsync(
                _postId,
                new PostReactionRequest
                {
                    Symbol = PostItemViewModel.DefaultLikeSymbol,
                    Attitude = (int)PostReactionAttitude.Positive,
                }).ConfigureAwait(true);

            if (reaction is null)
            {
                IsLiked = false;
                Upvotes = Math.Max(0, Upvotes - 1);
                LikeButtonText = Upvotes > 0 ? $"赞 {Upvotes}" : "赞";
                _toast.Success("已取消点赞");
            }
            else
            {
                IsLiked = true;
                Upvotes++;
                LikeButtonText = $"赞 {Upvotes}";
                _toast.Success("已点赞");
            }

            RefreshStats();
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"点赞失败:{FriendlySphereError(ex)}");
        }
        finally
        {
            _busyAction = false;
        }
    }

    /// <summary>
    /// Quote-forward: POST /sphere/posts with <c>forwarded_post_id</c>
    /// (same as Solian "forward", not ActivityPub boost).
    /// </summary>
    [RelayCommand]
    private async Task ForwardAsync()
    {
        if (_busyAction || _postId == Guid.Empty)
        {
            return;
        }

        try
        {
            _busyAction = true;

            var pub = await ResolvePublisherNameAsync().ConfigureAwait(true);
            var request = new CreatePostRequest
            {
                // Empty body is allowed when Attachments is omitted (server only rejects empty+[]).
                Content = null,
                Visibility = 0,
                Type = 0,
                ForwardedPostId = _postId,
            };

            await _api.CreatePostAsync(request, pub).ConfigureAwait(true);

            IsBoosted = true;
            BoostCount++;
            BoostButtonText = "已转发";
            RefreshStats();
            _toast.Success("已发布引用帖");
        }
        catch (SolarApiException ex)
        {
            // Some deployments require non-empty content even with a forward target.
            if (IsContentRequiredError(ex))
            {
                try
                {
                    var pub = await ResolvePublisherNameAsync().ConfigureAwait(true);
                    await _api.CreatePostAsync(
                        new CreatePostRequest
                        {
                            Content = "转发",
                            Visibility = 0,
                            Type = 0,
                            ForwardedPostId = _postId,
                        },
                        pub).ConfigureAwait(true);

                    IsBoosted = true;
                    BoostCount++;
                    BoostButtonText = "已转发";
                    RefreshStats();
                    _toast.Success("已发布引用帖");
                    return;
                }
                catch (SolarApiException retryEx)
                {
                    _toast.Error($"转发失败:{FriendlySphereError(retryEx)}");
                    return;
                }
            }

            _toast.Error($"转发失败:{FriendlySphereError(ex)}");
        }
        finally
        {
            _busyAction = false;
        }
    }

    private static bool IsContentRequiredError(SolarApiException ex)
    {
        var body = ex.ResponseBody ?? string.Empty;
        var msg = ex.ApiMessage ?? ex.Message ?? string.Empty;
        return body.Contains("POST_CONTENT_REQUIRED", StringComparison.OrdinalIgnoreCase)
               || msg.Contains("Content is required", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Map known Sphere error codes to short Chinese hints.</summary>
    private static string FriendlySphereError(SolarApiException ex)
    {
        var body = ex.ResponseBody ?? string.Empty;
        var code = string.Empty;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("code", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    code = c.GetString() ?? string.Empty;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // ignore
            }
        }

        return code switch
        {
            "POST_REACTION_SUBSCRIPTION_REQUIRED" => "自定义反应需要订阅；已改用默认 thumb_up",
            "POST_CONTENT_REQUIRED" => "内容不能为空",
            "POST_FORWARD_TARGET_NOT_FOUND" => "被转发的帖子不存在",
            "POST_FORWARD_BLOCKED" => "无法转发：双方存在屏蔽关系",
            "PUBLISHER_NOT_FOUND" => "需要先创建发布者（Publisher）才能转发",
            "POST_REACTION_BLOCKED" => "无法点赞：双方存在屏蔽关系",
            _ => ex.ApiMessage ?? ex.Message,
        };
    }

    [RelayCommand]
    private async Task ToggleBookmarkAsync()
    {
        if (_busyAction || _postId == Guid.Empty)
        {
            return;
        }

        try
        {
            _busyAction = true;
            if (IsBookmarked)
            {
                await _api.UnbookmarkPostAsync(_postId).ConfigureAwait(true);
                IsBookmarked = false;
                BookmarkButtonText = "收藏";
                _toast.Success("已取消收藏");
            }
            else
            {
                await _api.BookmarkPostAsync(_postId).ConfigureAwait(true);
                IsBookmarked = true;
                BookmarkButtonText = "已收藏";
                _toast.Success("已收藏");
            }
        }
        catch (SolarApiException ex)
        {
            _toast.Error($"收藏失败:{FriendlySphereError(ex)}");
        }
        finally
        {
            _busyAction = false;
        }
    }

    private async Task LoadRepliesInternalAsync(bool reset)
    {
        if (reset)
        {
            Replies.Clear();
            _replyOffset = 0;
        }

        IsLoadingReplies = true;
        try
        {
            var list = await _api.GetPostRepliesAsync(_postId, _replyOffset, ReplyPageSize).ConfigureAwait(true);
            foreach (var reply in list)
            {
                Replies.Add(new PostItemViewModel(reply, _imageLoader));
            }

            _replyOffset += list.Count;
            HasMoreReplies = list.Count >= ReplyPageSize;
            RepliesHeader = RepliesCount > 0 ? $"回复 ({RepliesCount})" : "回复";
            _ = LoadReplyImagesAsync();
        }
        finally
        {
            IsLoadingReplies = false;
        }
    }

    private void ApplyHeader(PostItemViewModel item)
    {
        AuthorName = item.AuthorName;
        OnPropertyChanged(nameof(Initials));
        AuthorHandle = item.AuthorHandle;
        AuthorAccountName = item.AuthorAccountName;
        AuthorAccountId = item.AuthorAccountId;
        PublisherName = item.PublisherName;
        OnPropertyChanged(nameof(CanOpenAuthorProfile));
        AvatarImage = item.AvatarImage;
        Title = item.Title;
        TitleVisibility = item.TitleVisibility;
        ContentText = item.Post.Content ?? item.Post.Description ?? item.ContentText;
        StatsText = item.StatsText;
        Upvotes = item.Upvotes;
        BoostCount = item.BoostCount;
        RepliesCount = item.RepliesCount;
        IsLiked = item.IsLiked;
        IsBookmarked = item.IsBookmarked;
        IsBoosted = item.IsBoosted;
        LikeButtonText = Upvotes > 0 ? $"赞 {Upvotes}" : "赞";
        BoostButtonText = BoostCount > 0 ? $"转发 {BoostCount}" : "转发";
        BookmarkButtonText = IsBookmarked ? "已收藏" : "收藏";
        RepliesHeader = RepliesCount > 0 ? $"回复 ({RepliesCount})" : "回复";
        PostIdText = item.Id.ToString("D");

        var time = item.Post.PublishedAt ?? item.Post.CreatedAt;
        TimeText = time?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? item.TimeText;

        if (!string.IsNullOrWhiteSpace(item.ForwardedText))
        {
            ForwardedText = item.ForwardedText;
            ForwardedVisibility = Visibility.Visible;
        }
        else
        {
            ForwardedText = string.Empty;
            ForwardedVisibility = Visibility.Collapsed;
        }

        ImagesVisibility = item.HasImages ? Visibility.Visible : Visibility.Collapsed;
        _ = LoadImagesFromItemAsync(item);
    }

    private void ApplyPost(SnPost post)
    {
        var publisher = post.Publisher;
        AuthorName = publisher?.Nick ?? publisher?.Name ?? AuthorName;
        OnPropertyChanged(nameof(Initials));
        AuthorHandle = string.IsNullOrWhiteSpace(publisher?.Name) ? AuthorHandle : $"@{publisher.Name}";
        PublisherName = publisher?.Name ?? PublisherName;
        AuthorAccountName = publisher?.Account?.Name ?? AuthorAccountName;
        AuthorAccountId = publisher?.AccountId ?? publisher?.Account?.Id ?? AuthorAccountId;
        OnPropertyChanged(nameof(CanOpenAuthorProfile));
        Title = post.Title ?? string.Empty;
        TitleVisibility = string.IsNullOrWhiteSpace(post.Title) ? Visibility.Collapsed : Visibility.Visible;
        ContentText = post.Content ?? post.Description ?? string.Empty;

        var time = post.PublishedAt ?? post.CreatedAt;
        TimeText = time?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? TimeText;

        if (post.ForwardedPost is { } forwarded)
        {
            var fwdAuthor = forwarded.Publisher?.Nick ?? forwarded.Publisher?.Name ?? "未知";
            var fwdContent = forwarded.Content ?? forwarded.Description ?? string.Empty;
            ForwardedText = $"转发自 {fwdAuthor}:{fwdContent}";
            ForwardedVisibility = Visibility.Visible;
        }
        else
        {
            ForwardedText = string.Empty;
            ForwardedVisibility = Visibility.Collapsed;
        }

        Upvotes = post.Upvotes;
        BoostCount = post.BoostCount;
        RepliesCount = post.RepliesCount;
        IsBookmarked = post.IsBookmarked;
        IsLiked = post.ReactionsMade is { Count: > 0 } && post.ReactionsMade.Any(kv => kv.Value)
                  || post.ReactionsMade?.GetValueOrDefault(PostItemViewModel.DefaultLikeSymbol) == true;

        LikeButtonText = Upvotes > 0 ? $"赞 {Upvotes}" : "赞";
        BoostButtonText = BoostCount > 0 ? $"转发 {BoostCount}" : "转发";
        BookmarkButtonText = IsBookmarked ? "已收藏" : "收藏";
        RepliesHeader = RepliesCount > 0 ? $"回复 ({RepliesCount})" : "回复";
        PostIdText = post.Id.ToString("D");
        RefreshStats();

        var imageUrls = (post.Attachments ?? [])
            .Where(CloudFileUrlHelper.IsLikelyImage)
            .Select(CloudFileUrlHelper.Resolve)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Cast<string>()
            .ToList();
        ImagesVisibility = imageUrls.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var avatarUrl = CloudFileUrlHelper.Resolve(publisher?.Picture);
        if (!string.IsNullOrWhiteSpace(avatarUrl) && AvatarImage is null)
        {
            _ = LoadAvatarAsync(avatarUrl);
        }
    }

    private void RefreshStats()
    {
        StatsText = $"回复 {RepliesCount} · 转发 {BoostCount} · 赞 {Upvotes}";
    }

    private async Task LoadImagesFromItemAsync(PostItemViewModel item)
    {
        if (AvatarImage is null && !string.IsNullOrWhiteSpace(item.AvatarUrl))
        {
            await LoadAvatarAsync(item.AvatarUrl).ConfigureAwait(true);
        }

        foreach (var url in item.ImageUrls)
        {
            var bmp = await _imageLoader.LoadAsync(url).ConfigureAwait(true);
            if (bmp is not null)
            {
                Images.Add(bmp);
            }
        }

        ImagesVisibility = Images.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task LoadImagesFromPostAsync(SnPost post)
    {
        Images.Clear();
        var urls = (post.Attachments ?? [])
            .Where(CloudFileUrlHelper.IsLikelyImage)
            .Select(CloudFileUrlHelper.Resolve)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Cast<string>()
            .ToList();

        foreach (var url in urls)
        {
            var bmp = await _imageLoader.LoadAsync(url).ConfigureAwait(true);
            if (bmp is not null)
            {
                Images.Add(bmp);
            }
        }

        ImagesVisibility = Images.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task LoadAvatarAsync(string url)
    {
        var bmp = await _imageLoader.LoadAsync(url).ConfigureAwait(true);
        if (bmp is not null)
        {
            AvatarImage = bmp;
        }
    }

    private async Task LoadReplyImagesAsync()
    {
        foreach (var item in Replies.ToList())
        {
            if (item.HasAvatar && item.AvatarImage is null && !string.IsNullOrWhiteSpace(item.AvatarUrl))
            {
                var bmp = await _imageLoader.LoadSafeAsync(item.AvatarUrl).ConfigureAwait(true);
                if (bmp is not null)
                {
                    item.AvatarImage = bmp;
                }
            }
        }
    }

    private async Task<string?> ResolvePublisherNameAsync()
    {
        if (_publisherResolved)
        {
            return _publisherName;
        }

        _publisherResolved = true;
        var accountId = _auth.CurrentAccount?.Id ?? Guid.Empty;
        if (accountId == Guid.Empty)
        {
            return null;
        }

        try
        {
            var publishers = await _api.GetAccountPublishersAsync(accountId).ConfigureAwait(true);
            _publisherName = publishers.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Name))?.Name;
        }
        catch (SolarApiException)
        {
            // best-effort
        }

        return _publisherName;
    }
}
