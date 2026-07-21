using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>Publisher management: profile, members, stats, features, collections, subscribe.</summary>
public partial class PublisherDetailViewModel : ObservableObject
{
    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;

    private string _name = string.Empty;

    public PublisherDetailViewModel(
        ISolarApiClient api,
        IToastService toast,
        DysonFileImageLoader imageLoader)
    {
        _api = api;
        _toast = toast;
        _imageLoader = imageLoader;
    }

    public ObservableCollection<SocialListItemViewModel> Members { get; } = [];
    public ObservableCollection<SocialListItemViewModel> Features { get; } = [];
    public ObservableCollection<SocialListItemViewModel> Collections { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "发布者";

    [ObservableProperty]
    public partial string Handle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Bio { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Meta { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SubscriptionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MembershipText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditNick { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditBio { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InviteUserId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewFeatureFlag { get; set; } = string.Empty;

    [ObservableProperty]
    public partial BitmapImage? PictureImage { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsSubscribed { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public void Initialize(PublisherNavArgs? args)
    {
        if (args is null || string.IsNullOrWhiteSpace(args.Name))
        {
            ErrorMessage = "缺少发布者名称";
            return;
        }

        _name = args.Name.Trim().TrimStart('@');
        Handle = $"@{_name}";
        Title = args.DisplayName is { Length: > 0 } d ? d : _name;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            Members.Clear();
            Features.Clear();
            Collections.Clear();

            var pub = await _api.GetPublisherAsync(_name).ConfigureAwait(true);
            Title = pub.Nick ?? pub.Name ?? _name;
            Handle = pub.Name is { } n ? $"@{n}" : Handle;
            Bio = pub.Bio ?? "（无简介）";
            Meta =
                $"{pub.Type} · 评分 {pub.Rating:0.##} (Lv{pub.RatingLevel})" +
                (pub.IsShadowbanned == true ? " · 已限流" : "") +
                (pub.GatekeptFollows == true ? " · 关注需审核" : "");
            EditNick = pub.Nick ?? string.Empty;
            EditBio = pub.Bio ?? string.Empty;

            await LoadPictureAsync(pub.Picture).ConfigureAwait(true);

            try
            {
                var stats = await _api.GetPublisherStatsAsync(_name).ConfigureAwait(true);
                StatsText = stats is null
                    ? "统计暂无"
                    : $"帖子 {stats.PostsCreated} · 订阅者 {stats.SubscribersCount} · 赞 {stats.UpvoteReceived} · 踩 {stats.DownvoteReceived}";
            }
            catch (SolarApiException)
            {
                StatsText = "统计未加载";
            }

            try
            {
                var sub = await _api.GetPublisherSubscriptionAsync(_name).ConfigureAwait(true);
                IsSubscribed = sub is { IsActive: true };
                SubscriptionText = IsSubscribed ? "已订阅" : "未订阅";
            }
            catch (SolarApiException)
            {
                SubscriptionText = "订阅状态未知";
            }

            try
            {
                var me = await _api.GetMyPublisherMembershipAsync(_name).ConfigureAwait(true);
                MembershipText = me is null ? "非成员" : $"我的角色：{me.Role}";
            }
            catch (SolarApiException)
            {
                MembershipText = "成员身份未知";
            }

            try
            {
                var members = await _api.GetPublisherMembersAsync(_name).ConfigureAwait(true);
                foreach (var m in members)
                {
                    var acc = m.Account;
                    Members.Add(new SocialListItemViewModel(
                        m.AccountId.ToString("D"),
                        acc?.Nick ?? acc?.Name ?? "成员",
                        acc?.Name is { } an ? $"@{an}" : "",
                        m.Role.ToString(),
                        m)
                    {
                        AccountId = m.AccountId,
                    });
                }
            }
            catch (SolarApiException)
            {
            }

            try
            {
                var features = await _api.GetPublisherFeaturesAsync(_name).ConfigureAwait(true);
                foreach (var kv in features)
                {
                    Features.Add(new SocialListItemViewModel(
                        kv.Key,
                        kv.Key,
                        kv.Value ? "启用" : "关闭",
                        "",
                        kv));
                }
            }
            catch (SolarApiException)
            {
            }

            try
            {
                var cols = await _api.GetPublisherCollectionsAsync(_name).ConfigureAwait(true);
                foreach (var c in cols)
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
            catch (SolarApiException)
            {
            }
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubscribeAsync()
    {
        try
        {
            await _api.SubscribePublisherAsync(_name).ConfigureAwait(true);
            IsSubscribed = true;
            SubscriptionText = "已订阅";
            _toast.Success("已订阅发布者");
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task UnsubscribeAsync()
    {
        try
        {
            await _api.UnsubscribePublisherAsync(_name).ConfigureAwait(true);
            IsSubscribed = false;
            SubscriptionText = "未订阅";
            _toast.Success("已取消订阅");
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        try
        {
            await _api.UpdatePublisherAsync(_name, new PublisherRequest
            {
                Nick = EditNick.Trim(),
                Bio = EditBio.Trim(),
            }).ConfigureAwait(true);
            _toast.Success("已保存");
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task InviteMemberAsync()
    {
        if (!Guid.TryParse(InviteUserId.Trim(), out var userId))
        {
            _toast.Warning("请填写用户 GUID");
            return;
        }

        try
        {
            await _api.InvitePublisherMemberAsync(_name, new PublisherMemberRequest
            {
                RelatedUserId = userId,
                Role = PublisherMemberRole.Member,
            }).ConfigureAwait(true);
            _toast.Success("已发送邀请");
            InviteUserId = string.Empty;
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task RemoveMemberAsync(SocialListItemViewModel? item)
    {
        if (item?.AccountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.RemovePublisherMemberAsync(_name, id).ConfigureAwait(true);
            _toast.Success("已移除成员");
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task LeaveAsync()
    {
        try
        {
            await _api.LeavePublisherAsync(_name).ConfigureAwait(true);
            _toast.Success("已退出发布者");
            MembershipText = "非成员";
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task AddFeatureAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFeatureFlag))
        {
            _toast.Warning("请填写 feature flag");
            return;
        }

        try
        {
            await _api.AddPublisherFeatureAsync(_name, new PublisherFeatureRequest
            {
                Flag = NewFeatureFlag.Trim(),
            }).ConfigureAwait(true);
            _toast.Success("已添加 feature");
            NewFeatureFlag = string.Empty;
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    /// <summary>Page navigates to filtered post feed.</summary>
    public event EventHandler<PostFeedNavArgs>? NavigateToFeed;

    [RelayCommand]
    private void OpenCollectionFeed(SocialListItemViewModel? item)
    {
        var slug = item?.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        NavigateToFeed?.Invoke(
            this,
            new PostFeedNavArgs(PostFeedKind.Collection, slug, item?.Title, _name));
    }

    [RelayCommand]
    private void OpenPublisherFeed()
    {
        if (string.IsNullOrWhiteSpace(_name))
        {
            return;
        }

        NavigateToFeed?.Invoke(
            this,
            new PostFeedNavArgs(PostFeedKind.Publisher, _name, Title));
    }

    [RelayCommand]
    private async Task SubscribeCollectionAsync(SocialListItemViewModel? item)
    {
        var slug = item?.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        try
        {
            await _api.SubscribeCollectionAsync(_name, slug).ConfigureAwait(true);
            _toast.Success("已订阅合集");
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    private async Task LoadPictureAsync(SnCloudFile? picture)
    {
        var url = CloudFileUrlHelper.Resolve(picture);
        if (string.IsNullOrWhiteSpace(url))
        {
            PictureImage = null;
            return;
        }

        try
        {
            PictureImage = await _imageLoader.LoadAsync(url).ConfigureAwait(true);
        }
        catch
        {
            PictureImage = null;
        }
    }
}
