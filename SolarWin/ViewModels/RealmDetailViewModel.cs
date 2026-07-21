using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>Realm community detail: info, members, permissions, invites.</summary>
public partial class RealmDetailViewModel : ObservableObject
{
    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;

    private string _slug = string.Empty;

    public RealmDetailViewModel(
        ISolarApiClient api,
        IToastService toast,
        DysonFileImageLoader imageLoader)
    {
        _api = api;
        _toast = toast;
        _imageLoader = imageLoader;
    }

    public ObservableCollection<SocialListItemViewModel> Members { get; } = [];
    public ObservableCollection<SocialListItemViewModel> Permissions { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "Realm";

    [ObservableProperty]
    public partial string SlugText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Meta { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MembershipText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InviteUserId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial BitmapImage? PictureImage { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsMember { get; set; }

    /// <summary>Open a member's profile page.</summary>
    public event EventHandler<UserProfileNavArgs>? OpenUserProfile;

    public void Initialize(RealmDetailNavArgs? args)
    {
        if (args is null || string.IsNullOrWhiteSpace(args.Slug))
        {
            ErrorMessage = "缺少 Realm slug";
            return;
        }

        _slug = args.Slug.Trim().TrimStart('/');
        SlugText = $"/{_slug}";
        Title = args.DisplayName is { Length: > 0 } d ? d : _slug;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(_slug))
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            Members.Clear();
            Permissions.Clear();

            SnRealm realm;
            try
            {
                realm = await _api.GetRealmAsync(_slug).ConfigureAwait(true);
            }
            catch (SolarApiException)
            {
                // Fallback: find in my/public lists
                realm = await FindRealmFallbackAsync(_slug).ConfigureAwait(true)
                    ?? throw new SolarApiException($"找不到 Realm：{_slug}");
            }

            Title = realm.Name ?? realm.Slug ?? _slug;
            SlugText = realm.Slug is { } s ? $"/{s}" : $"/{_slug}";
            Description = realm.Description ?? "（无描述）";
            Meta =
                $"{(realm.IsPublic ? "公开" : "私有")}" +
                $"{(realm.IsCommunity ? " · 社区" : "")}" +
                $" · Boost Lv{realm.BoostLevel} ({realm.BoostPoints:0.##})";

            await LoadPictureAsync(realm.Picture).ConfigureAwait(true);

            try
            {
                var members = await _api.GetRealmMembersAsync(_slug).ConfigureAwait(true);
                foreach (var m in members)
                {
                    var acc = m.Account;
                    Members.Add(new SocialListItemViewModel(
                        m.AccountId.ToString("D"),
                        m.Nick ?? acc?.Nick ?? acc?.Name ?? "成员",
                        acc?.Name is { } n ? $"@{n}" : $"角色 {m.Role}",
                        $"Lv{m.Level} · 角色 {m.Role}" +
                        (m.Label?.Name is { } lb ? $" · {lb}" : ""),
                        m)
                    {
                        AccountId = m.AccountId,
                        Slug = acc?.Name,
                    });
                }

                MembershipText = $"成员 {Members.Count}";
            }
            catch (SolarApiException ex)
            {
                MembershipText = "成员列表不可用";
                InfoSoft(ex.Message);
            }

            try
            {
                var perms = await _api.GetRealmRolePermissionsAsync(_slug).ConfigureAwait(true);
                foreach (var p in perms)
                {
                    var flags = new List<string>();
                    if (p.CanChat) flags.Add("聊天");
                    if (p.CanPost) flags.Add("发帖");
                    if (p.CanComment) flags.Add("评论");
                    if (p.CanUploadMedia) flags.Add("媒体");
                    if (p.CanModeratePosts) flags.Add("审帖");
                    if (p.CanModerateChat) flags.Add("审聊");
                    if (p.CanManageMembers) flags.Add("管成员");
                    if (p.CanManageRealm) flags.Add("管社区");
                    Permissions.Add(new SocialListItemViewModel(
                        p.Id.ToString("D"),
                        $"角色等级 {p.RoleLevel}",
                        flags.Count == 0 ? "无特殊权限" : string.Join(" · ", flags),
                        "",
                        p));
                }
            }
            catch (SolarApiException)
            {
            }

            // Best-effort membership: if current user appears in members list
            var me = (await SafeGetMyIdAsync().ConfigureAwait(true));
            IsMember = me is { } mid && Members.Any(x => x.AccountId == mid);
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
    private async Task JoinAsync()
    {
        try
        {
            await _api.JoinRealmAsync(_slug).ConfigureAwait(true);
            _toast.Success("已加入");
            IsMember = true;
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
            await _api.LeaveRealmAsync(_slug).ConfigureAwait(true);
            _toast.Success("已退出");
            IsMember = false;
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task InviteAsync()
    {
        if (!Guid.TryParse(InviteUserId.Trim(), out var userId))
        {
            _toast.Warning("请填写有效的用户 GUID");
            return;
        }

        try
        {
            await _api.InviteToRealmAsync(_slug, new RealmMemberRequest
            {
                RelatedUserId = userId,
                Role = 0,
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
    private void OpenMemberProfile(SocialListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        // Prefer account name in Slug field (we store name there for members)
        var name = item.Slug;
        if (string.IsNullOrWhiteSpace(name) && item.Payload is SnRealmMember { Account.Name: { } n })
        {
            name = n;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            _toast.Warning("该成员没有公开用户名，无法打开资料");
            return;
        }

        OpenUserProfile?.Invoke(this, new UserProfileNavArgs(name, item.AccountId, item.Title));
    }

    private async Task<SnRealm?> FindRealmFallbackAsync(string slug)
    {
        try
        {
            var mine = await _api.GetMyRealmsAsync().ConfigureAwait(true);
            var hit = mine.FirstOrDefault(r =>
                string.Equals(r.Slug, slug, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                return hit;
            }
        }
        catch (SolarApiException)
        {
        }

        try
        {
            var pub = await _api.GetPublicRealmsAsync().ConfigureAwait(true);
            return pub.FirstOrDefault(r =>
                string.Equals(r.Slug, slug, StringComparison.OrdinalIgnoreCase));
        }
        catch (SolarApiException)
        {
            return null;
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

    private async Task<Guid?> SafeGetMyIdAsync()
    {
        try
        {
            var me = await _api.GetPassportMeAsync().ConfigureAwait(true);
            return me.Id;
        }
        catch (SolarApiException)
        {
            try
            {
                return (await _api.GetMeAsync().ConfigureAwait(true)).Id;
            }
            catch (SolarApiException)
            {
                return null;
            }
        }
    }

    private void InfoSoft(string message)
    {
        if (string.IsNullOrWhiteSpace(ErrorMessage))
        {
            // keep non-fatal
            _ = message;
        }
    }
}
