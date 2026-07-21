using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>
/// Home hub: personal growth + social (Passport).
/// Sections: overview, people, friends, realms, growth, calendar, tickets, nearby, fun extras.
/// </summary>
public partial class HomeViewModel : ObservableObject
{
    private readonly ISolarApiClient _api;
    private readonly IAuthService _authService;
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;
    private readonly ChatViewModel _chat;

    private SnAccount? _me;
    private SnAccountProfile? _profile;
    private Guid? _viewingAccountId;

    public HomeViewModel(
        ISolarApiClient api,
        IAuthService authService,
        IToastService toast,
        DysonFileImageLoader imageLoader,
        ChatViewModel chat)
    {
        _api = api;
        _authService = authService;
        _toast = toast;
        _imageLoader = imageLoader;
        _chat = chat;
    }

    /// <summary>Page navigates to UserProfilePage.</summary>
    public event EventHandler<UserProfileNavArgs>? NavigateToUserProfile;

    /// <summary>Page navigates to RealmDetailPage.</summary>
    public event EventHandler<RealmDetailNavArgs>? NavigateToRealmDetail;

    /// <summary>Page navigates to ChatDetailPage after DM is ready.</summary>
    public event EventHandler<ChatRoomListItem>? NavigateToChatDetail;

    [ObservableProperty]
    public partial string Title { get; set; } = "首页 · 个人与社交";

    [ObservableProperty]
    public partial string WelcomeText { get; set; } = "正在加载…";

    [ObservableProperty]
    public partial string LevelText { get; set; } = "-";

    [ObservableProperty]
    public partial string CreditsText { get; set; } = "-";

    [ObservableProperty]
    public partial string ProgressText { get; set; } = "-";

    [ObservableProperty]
    public partial double LevelProgress { get; set; }

    [ObservableProperty]
    public partial string AchievementStatsText { get; set; } = "-";

    [ObservableProperty]
    public partial string OverviewMeta { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    [ObservableProperty]
    public partial int SelectedSectionIndex { get; set; }

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProfileLookupName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ViewedProfileTitle { get; set; } = "选择用户查看资料";

    [ObservableProperty]
    public partial string ViewedProfileBody { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ViewedProfileMeta { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RelationshipActionHint { get; set; } = string.Empty;

    public ObservableCollection<UserSearchResultItem> SearchResults { get; } = [];
    public ObservableCollection<SocialListItemViewModel> ViewedBadges { get; } = [];
    public ObservableCollection<SocialListItemViewModel> ViewedBoard { get; } = [];
    public ObservableCollection<SocialListItemViewModel> ViewedConnections { get; } = [];

    public ObservableCollection<SocialListItemViewModel> FriendRequests { get; } = [];
    public ObservableCollection<SocialListItemViewModel> Friends { get; } = [];
    public ObservableCollection<SocialListItemViewModel> CloseFriends { get; } = [];

    [ObservableProperty]
    public partial string FriendsSummary { get; set; } = string.Empty;

    public ObservableCollection<SocialListItemViewModel> MyRealms { get; } = [];
    public ObservableCollection<SocialListItemViewModel> PublicRealms { get; } = [];
    public ObservableCollection<SocialListItemViewModel> RealmInvites { get; } = [];
    public ObservableCollection<SocialListItemViewModel> SelectedRealmMembers { get; } = [];
    public ObservableCollection<SocialListItemViewModel> SelectedRealmPermissions { get; } = [];

    [ObservableProperty]
    public partial string RealmQuotaText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedRealmSlug { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewRealmSlug { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewRealmName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InviteUserId { get; set; } = string.Empty;

    public ObservableCollection<SocialListItemViewModel> Badges { get; } = [];
    public ObservableCollection<SocialListItemViewModel> BoardItems { get; } = [];
    public ObservableCollection<SocialListItemViewModel> LevelingRecords { get; } = [];
    public ObservableCollection<SocialListItemViewModel> CreditRecords { get; } = [];
    public ObservableCollection<SocialListItemViewModel> Achievements { get; } = [];
    public ObservableCollection<SocialListItemViewModel> Quests { get; } = [];
    public ObservableCollection<SocialListItemViewModel> Grants { get; } = [];
    public ObservableCollection<SocialListItemViewModel> ActionLogs { get; } = [];

    public ObservableCollection<SocialListItemViewModel> CalendarEvents { get; } = [];
    public ObservableCollection<SocialListItemViewModel> CountdownItems { get; } = [];
    public ObservableCollection<SocialListItemViewModel> NotableDays { get; } = [];

    [ObservableProperty]
    public partial string CalendarDayText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewEventTitle { get; set; } = string.Empty;

    public ObservableCollection<SocialListItemViewModel> Tickets { get; } = [];

    [ObservableProperty]
    public partial string TicketCountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewTicketTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewTicketContent { get; set; } = string.Empty;

    public ObservableCollection<SocialListItemViewModel> MyPins { get; } = [];
    public ObservableCollection<SocialListItemViewModel> NearbyPins { get; } = [];
    public ObservableCollection<SocialListItemViewModel> MyMeets { get; } = [];
    public ObservableCollection<SocialListItemViewModel> NearbyMeets { get; } = [];
    public ObservableCollection<SocialListItemViewModel> NfcTags { get; } = [];

    [ObservableProperty]
    public partial string NfcLookupQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NfcLookupResult { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewPinName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewMeetNotes { get; set; } = string.Empty;

    // —— 趣味 extras: fortune / IP / notable-days / rewind / spells ——

    public ObservableCollection<SocialListItemViewModel> FortuneItems { get; } = [];
    public ObservableCollection<SocialListItemViewModel> UserNotableDays { get; } = [];

    [ObservableProperty]
    public partial string FortuneText { get; set; } = "点击抽签获取运势…";

    [ObservableProperty]
    public partial string FortuneSourceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string IpCheckText { get; set; } = "尚未检测";

    [ObservableProperty]
    public partial string IpGeoText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RewindSummary { get; set; } = "尚未加载年度回顾";

    [ObservableProperty]
    public partial string RewindCodeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RewindLookupCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial double RewindYear { get; set; } = DateTime.Now.Year;

    [ObservableProperty]
    public partial string NewNotableName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewNotableDescription { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpellWord { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpellPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SpellResultText { get; set; } = string.Empty;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            InfoMessage = null;

            await LoadOverviewAsync().ConfigureAwait(true);
            await SafeLoadAsync(LoadFriendsAsync).ConfigureAwait(true);
            await SafeLoadAsync(LoadRealmsAsync).ConfigureAwait(true);
            await SafeLoadAsync(LoadGrowthAsync).ConfigureAwait(true);
            await SafeLoadAsync(LoadCalendarAsync).ConfigureAwait(true);
            await SafeLoadAsync(LoadTicketsAsync).ConfigureAwait(true);
            await SafeLoadAsync(LoadNearbyAsync).ConfigureAwait(true);
            await SafeLoadAsync(LoadFunExtrasAsync).ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            WelcomeText = _authService.CurrentAccount is { } a
                ? $"离线缓存：{a.Nick ?? a.Name}"
                : "无法加载首页数据";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadOverviewAsync()
    {
        SnAccount me;
        try
        {
            me = await _api.GetPassportMeAsync().ConfigureAwait(true);
        }
        catch (SolarApiException)
        {
            me = await _api.GetMeAsync().ConfigureAwait(true);
        }

        _me = me;
        _profile = me.Profile;
        try
        {
            _profile = await _api.GetMyProfileAsync().ConfigureAwait(true);
        }
        catch (SolarApiException)
        {
        }

        var name = me.Nick ?? me.Name ?? "User";
        WelcomeText = $"欢迎回来，{name}";
        LevelText = $"Lv{_profile?.Level ?? 0} · Perk {me.PerkLevel}";
        CreditsText = $"社交积分 {_profile?.SocialCredits:0.##}（Lv{_profile?.SocialCreditsLevel ?? 0}）";
        var progress = Math.Clamp(_profile?.LevelingProgress ?? 0, 0, 1);
        LevelProgress = progress;
        ProgressText = $"经验进度 {(progress * 100):0.#}% · EXP {_profile?.Experience ?? 0}";

        try
        {
            var stats = await _api.GetMyAchievementStatsAsync().ConfigureAwait(true);
            AchievementStatsText = stats is null
                ? "成就数据暂无"
                : $"成就 {stats.CompletedCount}/{stats.TotalCount}（{stats.CompletionPercentage:0.#}%）";
        }
        catch (SolarApiException)
        {
            AchievementStatsText = "成就未加载";
        }

        var unreadRooms = 0;
        try
        {
            var chatSummary = await _api.GetChatSummaryAsync().ConfigureAwait(true);
            unreadRooms = chatSummary.Count(kv => kv.Value.UnreadCount > 0);
        }
        catch (SolarApiException)
        {
        }

        OverviewMeta =
            $"@{me.Name} · 未读会话 {unreadRooms}" +
            (string.IsNullOrWhiteSpace(_profile?.Bio) ? string.Empty : $"\n{_profile.Bio}");
    }

    private async Task LoadFriendsAsync()
    {
        FriendRequests.Clear();
        Friends.Clear();
        CloseFriends.Clear();

        try
        {
            var requests = await _api.GetRelationshipRequestsAsync().ConfigureAwait(true);
            foreach (var r in requests)
            {
                var other = PickOther(r);
                var display = other?.Nick ?? other?.Name ?? r.RelatedId.ToString("D")[..8];
                var otherId = other?.Id
                    ?? (r.RelatedId == _me?.Id ? r.AccountId : r.RelatedId);
                FriendRequests.Add(new SocialListItemViewModel(
                    otherId.ToString("D"),
                    display,
                    other?.Name is { } n ? $"@{n}" : "好友请求",
                    r.Status.ToString(),
                    r)
                {
                    AccountId = otherId,
                });
            }
        }
        catch (SolarApiException ex)
        {
            InfoMessage = AppendInfo(InfoMessage, "Friend requests: " + ex.Message);
        }

        try
        {
            var overview = await _api.GetFriendsOverviewAsync().ConfigureAwait(true);
            if (overview.Count > 0)
            {
                foreach (var f in overview)
                {
                    var acc = f.Account;
                    if (acc is null)
                    {
                        continue;
                    }

                    var status = f.Status?.Attitude.ToString() ?? "-";
                    Friends.Add(new SocialListItemViewModel(
                        acc.Id.ToString("D"),
                        acc.Nick ?? acc.Name ?? "User",
                        acc.Name is { } n ? $"@{n}" : acc.Id.ToString("D")[..8],
                        status,
                        f)
                    {
                        AccountId = acc.Id,
                    });
                }
            }
            else
            {
                var rels = await _api.GetRelationshipsAsync(take: 50).ConfigureAwait(true);
                foreach (var r in rels.Where(x => x.Status == RelationshipStatus.Friend))
                {
                    var other = PickOther(r);
                    Friends.Add(new SocialListItemViewModel(
                        (other?.Id ?? r.RelatedId).ToString("D"),
                        other?.Nick ?? other?.Name ?? r.Alias ?? "Friend",
                        other?.Name is { } n ? $"@{n}" : (r.Alias ?? ""),
                        r.Status.ToString(),
                        r)
                    {
                        AccountId = other?.Id ?? r.RelatedId,
                    });
                }
            }
        }
        catch (SolarApiException ex)
        {
            InfoMessage = AppendInfo(InfoMessage, "Friends: " + ex.Message);
        }

        try
        {
            var close = await _api.GetCloseFriendsAsync().ConfigureAwait(true);
            foreach (var acc in close)
            {
                CloseFriends.Add(new SocialListItemViewModel(
                    acc.Id.ToString("D"),
                    acc.Nick ?? acc.Name ?? "Close friend",
                    acc.Name is { } n ? $"@{n}" : "",
                    "Close friend",
                    acc)
                {
                    AccountId = acc.Id,
                });
            }
        }
        catch (SolarApiException)
        {
        }

        FriendsSummary = $"好友 {Friends.Count} · 请求 {FriendRequests.Count} · 密友 {CloseFriends.Count}";
    }

    private async Task LoadRealmsAsync()
    {
        MyRealms.Clear();
        PublicRealms.Clear();
        RealmInvites.Clear();

        try
        {
            var mine = await _api.GetMyRealmsAsync().ConfigureAwait(true);
            foreach (var r in mine)
            {
                MyRealms.Add(new SocialListItemViewModel(
                    r.Id.ToString("D"),
                    r.Name ?? r.Slug ?? "Realm",
                    r.Slug is { } s ? $"/{s}" : "",
                    r.IsPublic ? "Public" : "Private",
                    r)
                {
                    Slug = r.Slug,
                });
            }
        }
        catch (SolarApiException ex)
        {
            InfoMessage = AppendInfo(InfoMessage, "My realms: " + ex.Message);
        }

        try
        {
            var pub = await _api.GetPublicRealmsAsync().ConfigureAwait(true);
            foreach (var r in pub.Take(40))
            {
                PublicRealms.Add(new SocialListItemViewModel(
                    r.Id.ToString("D"),
                    r.Name ?? r.Slug ?? "Realm",
                    r.Description ?? (r.Slug is { } s ? $"/{s}" : ""),
                    $"Boost Lv{r.BoostLevel}",
                    r)
                {
                    Slug = r.Slug,
                });
            }
        }
        catch (SolarApiException)
        {
        }

        try
        {
            var invites = await _api.GetRealmInvitesAsync().ConfigureAwait(true);
            foreach (var m in invites)
            {
                var slug = m.Realm?.Slug ?? m.RealmId.ToString("D")[..8];
                RealmInvites.Add(new SocialListItemViewModel(
                    m.RealmId.ToString("D"),
                    m.Realm?.Name ?? slug,
                    $"Role {m.Role}",
                    "Invite",
                    m)
                {
                    Slug = m.Realm?.Slug,
                });
            }
        }
        catch (SolarApiException)
        {
        }

        try
        {
            var quota = await _api.GetRealmQuotaAsync().ConfigureAwait(true);
            RealmQuotaText = quota is null
                ? "Quota unknown"
                : $"Realm quota {quota.Used}/{quota.Total} (left {quota.Remaining})";
        }
        catch (SolarApiException)
        {
            RealmQuotaText = "Quota unavailable";
        }
    }

    private async Task LoadGrowthAsync()
    {
        Badges.Clear();
        BoardItems.Clear();
        LevelingRecords.Clear();
        CreditRecords.Clear();
        Achievements.Clear();
        Quests.Clear();
        Grants.Clear();
        ActionLogs.Clear();

        await FillListAsync(
            () => _api.GetMyBadgesAsync(),
            Badges,
            b => new SocialListItemViewModel(
                b.Id.ToString("D"),
                b.Label ?? b.Type ?? "Badge",
                b.Caption ?? "",
                b.ActivatedAt is null ? "Inactive" : "Active",
                b)).ConfigureAwait(true);

        await FillListAsync(
            () => _api.GetMyBoardAsync(),
            BoardItems,
            b => new SocialListItemViewModel(
                b.Id.ToString("D"),
                b.WidgetKey ?? b.Kind.ToString(),
                b.IsEnabled ? "Enabled" : "Disabled",
                $"order {b.Order}",
                b)).ConfigureAwait(true);

        await FillListAsync(
            () => _api.GetMyLevelingAsync(take: 30),
            LevelingRecords,
            r => new SocialListItemViewModel(
                r.Id.ToString("D"),
                r.Reason ?? r.ReasonType ?? "XP",
                $"d {r.Delta}" + (r.BonusMultiplier is > 0 and not 1 ? $" x{r.BonusMultiplier:0.##}" : ""),
                r.CreatedAt?.ToLocalTime().ToString("g") ?? "",
                r)).ConfigureAwait(true);

        await FillListAsync(
            () => _api.GetMyCreditsHistoryAsync(take: 30),
            CreditRecords,
            r => new SocialListItemViewModel(
                r.Id.ToString("D"),
                r.Reason ?? r.ReasonType ?? "Credits",
                $"d {r.Delta:0.##} | {r.Status}",
                r.CreatedAt?.ToLocalTime().ToString("g") ?? "",
                r)).ConfigureAwait(true);

        await FillListAsync(
            () => _api.GetMyAchievementsAsync(),
            Achievements,
            a => new SocialListItemViewModel(
                a.Identifier ?? Guid.NewGuid().ToString("N"),
                a.Title ?? a.Identifier ?? "Achievement",
                a.Summary ?? "",
                a.IsCompleted
                    ? "Done"
                    : $"{a.ProgressCount}/{Math.Max(1, a.TargetCount)}",
                a)).ConfigureAwait(true);

        await FillListAsync(
            () => _api.GetMyQuestsAsync(),
            Quests,
            q => new SocialListItemViewModel(
                q.Identifier ?? Guid.NewGuid().ToString("N"),
                q.Title ?? q.Identifier ?? "Quest",
                q.Summary ?? q.PeriodKey ?? "",
                q.IsCompleted
                    ? "Done"
                    : $"{q.ProgressCount}/{Math.Max(1, q.TargetCount)}",
                q)).ConfigureAwait(true);

        await FillListAsync(
            () => _api.GetMyProgressGrantsAsync(),
            Grants,
            g => new SocialListItemViewModel(
                g.Id.ToString("D"),
                g.DefinitionTitle ?? g.DefinitionIdentifier ?? "Grant",
                g.DefinitionType ?? "",
                g.CreatedAt?.ToLocalTime().ToString("g") ?? "",
                g)).ConfigureAwait(true);

        await FillListAsync(
            () => _api.GetMyActionsAsync(take: 30),
            ActionLogs,
            a => new SocialListItemViewModel(
                a.Id.ToString("D"),
                a.Action ?? "Action",
                a.IpAddress ?? a.UserAgent ?? "",
                a.CreatedAt?.ToLocalTime().ToString("g") ?? "",
                a)).ConfigureAwait(true);
    }

    private async Task LoadCalendarAsync()
    {
        CalendarEvents.Clear();
        CountdownItems.Clear();
        NotableDays.Clear();
        CalendarDayText = string.Empty;

        try
        {
            var day = await _api.GetMyCalendarDayAsync().ConfigureAwait(true);
            if (day is not null)
            {
                CalendarDayText = day.Date?.ToLocalTime().ToString("D")
                    ?? DateTimeOffset.Now.ToString("D");
                if (day.UserEvents is { Count: > 0 })
                {
                    foreach (var e in day.UserEvents)
                    {
                        CalendarEvents.Add(ToEventItem(e));
                    }
                }

                if (day.NotableDays is { Count: > 0 })
                {
                    foreach (var n in day.NotableDays)
                    {
                        NotableDays.Add(new SocialListItemViewModel(
                            n.OccurrenceKey ?? Guid.NewGuid().ToString("N"),
                            n.LocalName ?? n.GlobalName ?? "Notable day",
                            n.Description ?? n.CountryCode ?? "",
                            n.Date?.ToLocalTime().ToString("d") ?? "",
                            n));
                    }
                }
            }
        }
        catch (SolarApiException)
        {
        }

        if (CalendarEvents.Count == 0)
        {
            try
            {
                var events = await _api.GetMyCalendarEventsAsync().ConfigureAwait(true);
                foreach (var e in events)
                {
                    CalendarEvents.Add(ToEventItem(e));
                }
            }
            catch (SolarApiException ex)
            {
                InfoMessage = AppendInfo(InfoMessage, "Calendar: " + ex.Message);
            }
        }

        try
        {
            var countdown = await _api.GetMyCalendarCountdownAsync().ConfigureAwait(true);
            foreach (var c in countdown)
            {
                CountdownItems.Add(new SocialListItemViewModel(
                    (c.EventId ?? Guid.NewGuid()).ToString("D"),
                    c.Title ?? "Event",
                    c.IsOngoing ? "Ongoing" : $"In {c.DaysRemaining}d {c.HoursRemaining}h",
                    c.StartTime?.ToLocalTime().ToString("g") ?? "",
                    c));
            }
        }
        catch (SolarApiException)
        {
        }
    }

    /// <summary>Fortune · IP · user notable-days · rewind · spells.</summary>
    private async Task LoadFunExtrasAsync()
    {
        await LoadFortunePreviewAsync().ConfigureAwait(true);
        await LoadIpCheckAsync().ConfigureAwait(true);
        await LoadUserNotableDaysAsync().ConfigureAwait(true);
        await LoadRewindAsync().ConfigureAwait(true);
    }

    private async Task LoadFortunePreviewAsync()
    {
        FortuneItems.Clear();
        try
        {
            var list = await _api.GetRandomFortuneAsync("zh").ConfigureAwait(true);
            if (list.Count == 0)
            {
                list = await _api.GetFortuneSayingsAsync().ConfigureAwait(true);
            }

            foreach (var f in list.Take(8))
            {
                FortuneItems.Add(new SocialListItemViewModel(
                    Guid.NewGuid().ToString("N"),
                    f.Content ?? "…",
                    f.Source ?? "",
                    f.Language ?? "",
                    f));
            }

            if (list.Count > 0)
            {
                ApplyFortune(list[0]);
            }
        }
        catch (SolarApiException ex)
        {
            FortuneText = "运势暂不可用";
            FortuneSourceText = ex.ApiMessage ?? ex.Message;
        }
    }

    private void ApplyFortune(FortuneSaying f)
    {
        FortuneText = string.IsNullOrWhiteSpace(f.Content) ? "（空）" : f.Content!;
        FortuneSourceText = string.IsNullOrWhiteSpace(f.Source)
            ? (f.Language ?? "运势")
            : $"{f.Source} · {f.Language}";
    }

    [RelayCommand]
    private async Task DrawFortuneAsync()
    {
        try
        {
            IsBusy = true;
            var list = await _api.GetRandomFortuneAsync("zh").ConfigureAwait(true);
            if (list.Count == 0)
            {
                _toast.Show("没有抽到运势");
                return;
            }

            ApplyFortune(list[0]);
            FortuneItems.Clear();
            foreach (var f in list.Take(8))
            {
                FortuneItems.Add(new SocialListItemViewModel(
                    Guid.NewGuid().ToString("N"),
                    f.Content ?? "…",
                    f.Source ?? "",
                    f.Language ?? "",
                    f));
            }

            _toast.Success("已抽签");
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

    private async Task LoadIpCheckAsync()
    {
        try
        {
            var ip = await _api.GetIpCheckAsync().ConfigureAwait(true);
            var geo = ip?.Geo ?? await _api.GetIpGeoAsync().ConfigureAwait(true);
            var client = ip?.ClientIp ?? ip?.RemoteIp ?? ip?.XRealIp ?? ip?.CfConnectingIp ?? "—";
            IpCheckText = $"IP：{client}";
            if (geo is not null)
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(geo.Country))
                {
                    parts.Add(geo.Country!);
                }

                if (!string.IsNullOrWhiteSpace(geo.Subdivision))
                {
                    parts.Add(geo.Subdivision!);
                }

                if (!string.IsNullOrWhiteSpace(geo.City))
                {
                    parts.Add(geo.City!);
                }

                if (!string.IsNullOrWhiteSpace(geo.TimeZone))
                {
                    parts.Add(geo.TimeZone!);
                }

                if (geo.Latitude is not null && geo.Longitude is not null)
                {
                    parts.Add($"{geo.Latitude:F2}, {geo.Longitude:F2}");
                }

                IpGeoText = parts.Count > 0 ? string.Join(" · ", parts) : "地理信息未知";
            }
            else
            {
                IpGeoText = "无 Geo 数据";
            }
        }
        catch (SolarApiException ex)
        {
            IpCheckText = "IP 检测失败";
            IpGeoText = ex.ApiMessage ?? ex.Message;
        }
    }

    [RelayCommand]
    private async Task RefreshIpCheckAsync()
    {
        try
        {
            IsBusy = true;
            await LoadIpCheckAsync().ConfigureAwait(true);
            _toast.Success("IP 已刷新");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadUserNotableDaysAsync()
    {
        UserNotableDays.Clear();
        try
        {
            var list = await _api.GetNotableDaysAsync(DateTime.Now.Year, "CN", 50).ConfigureAwait(true);
            foreach (var n in list.OrderBy(x => x.StartDate))
            {
                UserNotableDays.Add(new SocialListItemViewModel(
                    n.Id == Guid.Empty ? Guid.NewGuid().ToString("N") : n.Id.ToString("D"),
                    n.LocalName ?? n.Name ?? "纪念日",
                    n.Description ?? n.Region ?? "",
                    FormatNotableRange(n),
                    n));
            }
        }
        catch (SolarApiException ex)
        {
            InfoMessage = AppendInfo(InfoMessage, "纪念日: " + ex.Message);
        }
    }

    private static string FormatNotableRange(SnNotableDay n)
    {
        var start = n.StartDate?.ToLocalTime().ToString("yyyy-MM-dd") ?? "?";
        var end = n.EndDate?.ToLocalTime().ToString("yyyy-MM-dd");
        if (string.IsNullOrWhiteSpace(end) || end == start)
        {
            return start + (n.IsRecurring ? " · 每年" : "");
        }

        return $"{start} ~ {end}" + (n.IsRecurring ? " · 每年" : "");
    }

    [RelayCommand]
    private async Task CreateNotableDayAsync()
    {
        var name = NewNotableName?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            _toast.Show("请填写纪念日名称");
            return;
        }

        try
        {
            IsBusy = true;
            var today = DateTimeOffset.Now.Date;
            var created = await _api.CreateNotableDayAsync(new NotableDayRequest
            {
                Name = name,
                LocalName = name,
                Description = string.IsNullOrWhiteSpace(NewNotableDescription)
                    ? null
                    : NewNotableDescription.Trim(),
                StartDate = today,
                EndDate = today,
                IsAllDay = true,
                Region = "CN",
            }).ConfigureAwait(true);

            NewNotableName = string.Empty;
            NewNotableDescription = string.Empty;
            UserNotableDays.Insert(0, new SocialListItemViewModel(
                created.Id.ToString("D"),
                created.LocalName ?? created.Name ?? name,
                created.Description ?? "",
                FormatNotableRange(created),
                created));
            _toast.Success("纪念日已添加");
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
    private async Task DeleteUserNotableDayAsync(SocialListItemViewModel? item)
    {
        if (item?.Payload is not SnNotableDay day || day.Id == Guid.Empty)
        {
            // try parse id
            if (item is null || !Guid.TryParse(item.Id, out var id) || id == Guid.Empty)
            {
                return;
            }

            try
            {
                await _api.DeleteNotableDayAsync(id).ConfigureAwait(true);
                UserNotableDays.Remove(item);
                _toast.Success("已删除");
            }
            catch (SolarApiException ex)
            {
                _toast.Error(ex.ApiMessage ?? ex.Message);
            }

            return;
        }

        try
        {
            await _api.DeleteNotableDayAsync(day.Id).ConfigureAwait(true);
            UserNotableDays.Remove(item);
            _toast.Success("已删除");
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.ApiMessage ?? ex.Message);
        }
    }

    private async Task LoadRewindAsync()
    {
        try
        {
            var point = await _api.GetMyRewindAsync().ConfigureAwait(true);
            ApplyRewind(point);
        }
        catch (SolarApiException ex)
        {
            RewindSummary = "回顾暂不可用：" + (ex.ApiMessage ?? ex.Message);
            RewindCodeText = string.Empty;
        }
    }

    private void ApplyRewind(SnRewindPoint? point)
    {
        if (point is null)
        {
            RewindSummary = "暂无年度回顾数据（可尝试生成公开/私密回顾）";
            RewindCodeText = string.Empty;
            return;
        }

        if (point.Year > 0)
        {
            RewindYear = point.Year;
        }

        // keep double for NumberBox

        var dataKeys = point.Data is { Count: > 0 }
            ? string.Join("、", point.Data.Keys.Take(8))
            : "无附加字段";
        RewindSummary =
            $"{point.Year} 年回顾 · schema v{point.SchemaVersion}\n数据字段：{dataKeys}";
        RewindCodeText = string.IsNullOrWhiteSpace(point.SharableCode)
            ? "（无私密分享码）"
            : $"分享码：{point.SharableCode}";
    }

    [RelayCommand]
    private async Task PublishRewindPublicAsync()
    {
        try
        {
            IsBusy = true;
            var year = RewindYear <= 0 ? DateTime.Now.Year : (int)RewindYear;
            var point = await _api.PublishRewindPublicAsync(year).ConfigureAwait(true);
            ApplyRewind(point);
            _toast.Success($"已生成 {year} 公开回顾");
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
    private async Task PublishRewindPrivateAsync()
    {
        try
        {
            IsBusy = true;
            var year = RewindYear <= 0 ? DateTime.Now.Year : (int)RewindYear;
            var point = await _api.PublishRewindPrivateAsync(year).ConfigureAwait(true);
            ApplyRewind(point);
            _toast.Success($"已生成 {year} 私密回顾");
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
    private async Task LookupRewindCodeAsync()
    {
        var code = RewindLookupCode?.Trim() ?? string.Empty;
        if (code.Length == 0)
        {
            _toast.Show("请输入回顾分享码");
            return;
        }

        try
        {
            IsBusy = true;
            var point = await _api.GetRewindByCodeAsync(code).ConfigureAwait(true);
            if (point is null)
            {
                _toast.Show("未找到该分享码");
                return;
            }

            ApplyRewind(point);
            _toast.Success("已加载分享回顾");
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
    private async Task LookupSpellAsync()
    {
        var word = SpellWord?.Trim() ?? string.Empty;
        if (word.Length == 0)
        {
            _toast.Show("请输入咒语");
            return;
        }

        try
        {
            IsBusy = true;
            var spell = await _api.LookupSpellAsync(word).ConfigureAwait(true);
            if (spell is null)
            {
                SpellResultText = "未找到该咒语（可能已过期或不存在）";
                return;
            }

            var exp = spell.ExpiresAt is { } e ? e.ToLocalTime().ToString("g") : "—";
            var aff = spell.AffectedAt is { } a ? a.ToLocalTime().ToString("g") : "未使用";
            SpellResultText = $"类型：{spell.Type} · Id {spell.Id:D}\n过期：{exp}\n生效：{aff}";
        }
        catch (SolarApiException ex)
        {
            SpellResultText = ex.ApiMessage ?? ex.Message;
            _toast.Error(SpellResultText);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplySpellAsync()
    {
        var word = SpellWord?.Trim() ?? string.Empty;
        if (word.Length == 0)
        {
            _toast.Show("请输入咒语");
            return;
        }

        try
        {
            IsBusy = true;
            var req = string.IsNullOrWhiteSpace(SpellPassword)
                ? null
                : new MagicSpellApplyRequest { NewPassword = SpellPassword };
            await _api.ApplySpellAsync(word, req).ConfigureAwait(true);
            SpellPassword = string.Empty;
            SpellResultText = "咒语已应用";
            _toast.Success("咒语已应用");
            await LookupSpellAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            SpellResultText = ex.ApiMessage ?? ex.Message;
            _toast.Error(SpellResultText);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResendSpellActivationAsync()
    {
        try
        {
            IsBusy = true;
            await _api.ResendSpellActivationAsync().ConfigureAwait(true);
            _toast.Success("已请求重发激活咒语");
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

    private async Task LoadTicketsAsync()
    {
        Tickets.Clear();
        try
        {
            var count = await _api.GetTicketCountAsync().ConfigureAwait(true);
            TicketCountText = $"Tickets {count}";
        }
        catch (SolarApiException)
        {
            TicketCountText = string.Empty;
        }

        try
        {
            var list = await _api.GetMyTicketsAsync().ConfigureAwait(true);
            foreach (var t in list)
            {
                Tickets.Add(new SocialListItemViewModel(
                    t.Id.ToString("D"),
                    t.Title ?? "Ticket",
                    $"{t.Type} | {t.Priority}",
                    t.Status.ToString(),
                    t));
            }
        }
        catch (SolarApiException ex)
        {
            InfoMessage = AppendInfo(InfoMessage, "Tickets: " + ex.Message);
        }
    }

    private async Task LoadNearbyAsync()
    {
        MyPins.Clear();
        NearbyPins.Clear();
        MyMeets.Clear();
        NearbyMeets.Clear();
        NfcTags.Clear();

        await FillListAsync(
            () => _api.GetMyPinsAsync(),
            MyPins,
            p => new SocialListItemViewModel(
                p.Id.ToString("D"),
                p.LocationName ?? "My pin",
                p.LocationAddress ?? p.LocationWkt ?? "",
                $"{p.Visibility} | {p.Status}",
                p)).ConfigureAwait(true);

        await FillListAsync(
            () => _api.GetNearbyPinsAsync(),
            NearbyPins,
            p => new SocialListItemViewModel(
                p.Id.ToString("D"),
                p.LocationName ?? p.Account?.Nick ?? "Nearby pin",
                p.LocationAddress ?? "",
                p.Visibility.ToString(),
                p)).ConfigureAwait(true);

        await FillListAsync(
            () => _api.GetMyMeetsAsync(),
            MyMeets,
            m => new SocialListItemViewModel(
                m.Id.ToString("D"),
                m.LocationName ?? m.Notes ?? "Meet",
                m.Host?.Nick ?? m.Host?.Name ?? "",
                m.Status.ToString(),
                m)).ConfigureAwait(true);

        await FillListAsync(
            () => _api.GetNearbyMeetsAsync(),
            NearbyMeets,
            m => new SocialListItemViewModel(
                m.Id.ToString("D"),
                m.LocationName ?? m.Notes ?? "Nearby meet",
                m.LocationAddress ?? "",
                m.Status.ToString(),
                m)).ConfigureAwait(true);

        await FillListAsync(
            () => _api.GetMyNfcTagsAsync(),
            NfcTags,
            t => new SocialListItemViewModel(
                t.Id.ToString("D"),
                t.Label ?? t.Uid ?? "NFC",
                t.IsLocked ? "Locked" : (t.IsActive ? "Active" : "Inactive"),
                t.Uid ?? "",
                t)).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SearchPeopleAsync()
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            var list = await _api.SearchAccountsAsync(SearchQuery.Trim(), take: 20).ConfigureAwait(true);
            foreach (var acc in list)
            {
                var item = new UserSearchResultItem(acc, _imageLoader);
                SearchResults.Add(item);
                if (item.HasAvatar && item.AvatarUrl is { } url)
                {
                    _ = LoadSearchAvatarAsync(item, url);
                }
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

    private async Task LoadSearchAvatarAsync(UserSearchResultItem item, string url)
    {
        try
        {
            var img = await _imageLoader.LoadAsync(url).ConfigureAwait(true);
            if (img is not null)
            {
                item.AvatarImage = img;
            }
        }
        catch
        {
        }
    }

    [RelayCommand]
    private Task ViewPersonAsync(UserSearchResultItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Name))
        {
            return Task.CompletedTask;
        }

        ProfileLookupName = item.Name;
        NavigateToUserProfile?.Invoke(
            this,
            new UserProfileNavArgs(item.Name, item.AccountId, item.Nick));
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task LoadPersonProfileAsync()
    {
        var name = ProfileLookupName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            ViewedBadges.Clear();
            ViewedBoard.Clear();
            ViewedConnections.Clear();

            var acc = await _api.GetAccountByNameAsync(name).ConfigureAwait(true);
            _viewingAccountId = acc.Id;
            var profile = acc.Profile;
            ViewedProfileTitle = $"{acc.Nick ?? acc.Name}  |  @{acc.Name}";
            ViewedProfileBody = profile?.Bio ?? "（无简介）";
            ViewedProfileMeta =
                $"Lv{profile?.Level ?? 0} | Credits {profile?.SocialCredits:0.##} | Perk {acc.PerkLevel}\n" +
                $"Location {profile?.Location ?? "-"} | Gender {profile?.Gender ?? "-"}";

            try
            {
                var status = await _api.GetAccountStatusAsync(name).ConfigureAwait(true);
                if (status is not null)
                {
                    ViewedProfileMeta += $"\nStatus {status.Attitude}" +
                        (string.IsNullOrWhiteSpace(status.Label) ? "" : $" | {status.Label}");
                }
            }
            catch (SolarApiException)
            {
            }

            try
            {
                var badges = await _api.GetAccountBadgesAsync(name).ConfigureAwait(true);
                foreach (var b in badges)
                {
                    ViewedBadges.Add(new SocialListItemViewModel(
                        b.Id.ToString("D"), b.Label ?? b.Type ?? "Badge", b.Caption ?? "", "", b));
                }
            }
            catch (SolarApiException)
            {
            }

            try
            {
                var board = await _api.GetAccountBoardAsync(name).ConfigureAwait(true);
                foreach (var b in board)
                {
                    ViewedBoard.Add(new SocialListItemViewModel(
                        b.Id.ToString("D"),
                        b.WidgetKey ?? b.Kind.ToString(),
                        b.IsEnabled ? "Enabled" : "Disabled",
                        "",
                        b));
                }
            }
            catch (SolarApiException)
            {
            }

            try
            {
                var conns = await _api.GetAccountConnectionsAsync(name).ConfigureAwait(true);
                foreach (var c in conns)
                {
                    ViewedConnections.Add(new SocialListItemViewModel(
                        c.Provider ?? Guid.NewGuid().ToString("N"),
                        c.Provider ?? "Connection",
                        c.ProvidedIdentifier ?? "",
                        c.Url ?? "",
                        c));
                }
            }
            catch (SolarApiException)
            {
            }

            try
            {
                var rel = await _api.GetRelationshipAsync(acc.Id).ConfigureAwait(true);
                RelationshipActionHint = rel is null
                    ? "尚无关系"
                    : $"Relation: {rel.Status}" +
                      (string.IsNullOrWhiteSpace(rel.Alias) ? "" : $" (alias {rel.Alias})");
            }
            catch (SolarApiException)
            {
                RelationshipActionHint = "关系状态未知";
            }
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            ViewedProfileTitle = "加载失败";
            ViewedProfileBody = ex.Message;
            _toast.Error(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SendFriendRequestAsync()
    {
        if (_viewingAccountId is not { } id)
        {
            _toast.Warning("请先查看一位用户");
            return;
        }

        try
        {
            await _api.SendFriendRequestAsync(id).ConfigureAwait(true);
            _toast.Success("已发送好友请求");
            RelationshipActionHint = "已发送好友请求";
            await LoadFriendsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task BlockViewedAsync()
    {
        if (_viewingAccountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.BlockAccountAsync(id).ConfigureAwait(true);
            _toast.Success("已拉黑");
            RelationshipActionHint = "已拉黑";
            await LoadFriendsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task UnblockViewedAsync()
    {
        if (_viewingAccountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.UnblockAccountAsync(id).ConfigureAwait(true);
            _toast.Success("已取消拉黑");
            await LoadFriendsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task MuteViewedAsync()
    {
        if (_viewingAccountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.MuteAccountAsync(id).ConfigureAwait(true);
            _toast.Success("已静音");
            RelationshipActionHint = "已静音";
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task UnmuteViewedAsync()
    {
        if (_viewingAccountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.UnmuteAccountAsync(id).ConfigureAwait(true);
            _toast.Success("已取消静音");
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task AcceptFriendAsync(SocialListItemViewModel? item)
    {
        if (item?.AccountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.AcceptFriendRequestAsync(id).ConfigureAwait(true);
            _toast.Success("已接受");
            await LoadFriendsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeclineFriendAsync(SocialListItemViewModel? item)
    {
        if (item?.AccountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.DeclineFriendRequestAsync(id).ConfigureAwait(true);
            _toast.Success("已拒绝");
            await LoadFriendsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private void OpenFriendProfile(SocialListItemViewModel? item)
    {
        if (TryResolveAccountFromItem(item, out var name, out var accountId, out var display))
        {
            NavigateToUserProfile?.Invoke(this, new UserProfileNavArgs(name!, accountId, display));
            return;
        }

        _toast.Warning("无法打开资料：缺少用户名");
    }

    [RelayCommand]
    private async Task StartDirectChatWithFriendAsync(SocialListItemViewModel? item)
    {
        if (item?.AccountId is not { } id || id == Guid.Empty)
        {
            // Try resolve from payload
            if (!TryResolveAccountFromItem(item, out _, out var aid, out _) || aid is null)
            {
                _toast.Warning("无法私聊：缺少用户 ID");
                return;
            }

            id = aid.Value;
        }

        if (id == _authService.CurrentAccount?.Id || id == _me?.Id)
        {
            _toast.Warning("不能与自己私聊");
            return;
        }

        try
        {
            IsBusy = true;
            var display = item?.Title;
            var roomItem = await _chat.EnsureDirectChatAsync(id, display).ConfigureAwait(true);
            if (roomItem is not null)
            {
                _toast.Success(string.IsNullOrWhiteSpace(display) ? "已打开私聊" : $"已打开与 {display} 的私聊");
                NavigateToChatDetail?.Invoke(this, roomItem);
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
    private async Task OpenViewedProfilePageAsync()
    {
        var name = ProfileLookupName?.Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(name))
        {
            _toast.Warning("请输入用户名");
            return;
        }

        NavigateToUserProfile?.Invoke(
            this,
            new UserProfileNavArgs(name, _viewingAccountId));
        await Task.CompletedTask.ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task StartDirectChatWithViewedAsync()
    {
        if (_viewingAccountId is not { } id || id == Guid.Empty)
        {
            _toast.Warning("请先预览用户资料");
            return;
        }

        if (id == _me?.Id || id == _authService.CurrentAccount?.Id)
        {
            _toast.Warning("不能与自己私聊");
            return;
        }

        try
        {
            IsBusy = true;
            var display = ViewedProfileTitle;
            var roomItem = await _chat.EnsureDirectChatAsync(id, display).ConfigureAwait(true);
            if (roomItem is not null)
            {
                _toast.Success("已打开私聊");
                NavigateToChatDetail?.Invoke(this, roomItem);
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
    private async Task SetCloseFriendAsync(SocialListItemViewModel? item)
    {
        if (item?.AccountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.SetCloseFriendAsync(id, isCloseFriend: true).ConfigureAwait(true);
            _toast.Success("已设为密友");
            await LoadFriendsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task RemoveFriendAsync(SocialListItemViewModel? item)
    {
        if (item?.AccountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.RemoveRelationshipAsync(id).ConfigureAwait(true);
            _toast.Success("已移除");
            await LoadFriendsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private void OpenRealmDetail(SocialListItemViewModel? item)
    {
        var slug = item?.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            _toast.Warning("缺少 Realm slug");
            return;
        }

        SelectedRealmSlug = slug;
        NavigateToRealmDetail?.Invoke(this, new RealmDetailNavArgs(slug, item?.Title));
    }

    [RelayCommand]
    private void SelectRealm(SocialListItemViewModel? item)
    {
        // Open dedicated Realm detail page (members / permissions / join).
        OpenRealmDetail(item);
    }

    [RelayCommand]
    private async Task CreateRealmAsync()
    {
        if (string.IsNullOrWhiteSpace(NewRealmSlug) || string.IsNullOrWhiteSpace(NewRealmName))
        {
            _toast.Warning("请填写 slug 与名称");
            return;
        }

        try
        {
            await _api.CreateRealmAsync(new RealmRequest
            {
                Slug = NewRealmSlug.Trim(),
                Name = NewRealmName.Trim(),
                IsPublic = true,
                IsCommunity = true,
            }).ConfigureAwait(true);
            _toast.Success("已创建 Realm");
            NewRealmSlug = string.Empty;
            NewRealmName = string.Empty;
            await LoadRealmsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task JoinRealmAsync(SocialListItemViewModel? item)
    {
        var slug = item?.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        try
        {
            await _api.JoinRealmAsync(slug).ConfigureAwait(true);
            _toast.Success("已加入");
            await LoadRealmsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task LeaveRealmAsync(SocialListItemViewModel? item)
    {
        var slug = item?.Slug ?? SelectedRealmSlug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        try
        {
            await _api.LeaveRealmAsync(slug).ConfigureAwait(true);
            _toast.Success("已退出");
            await LoadRealmsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task AcceptRealmInviteAsync(SocialListItemViewModel? item)
    {
        var slug = item?.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        try
        {
            await _api.AcceptRealmInviteAsync(slug).ConfigureAwait(true);
            _toast.Success("已接受邀请");
            await LoadRealmsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeclineRealmInviteAsync(SocialListItemViewModel? item)
    {
        var slug = item?.Slug;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        try
        {
            await _api.DeclineRealmInviteAsync(slug).ConfigureAwait(true);
            _toast.Success("已拒绝邀请");
            await LoadRealmsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task InviteToSelectedRealmAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedRealmSlug))
        {
            _toast.Warning("请先选择 Realm");
            return;
        }

        if (!Guid.TryParse(InviteUserId.Trim(), out var userId))
        {
            _toast.Warning("请填写有效的用户 GUID");
            return;
        }

        try
        {
            await _api.InviteToRealmAsync(SelectedRealmSlug, new RealmMemberRequest
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
    private async Task ActivateBadgeAsync(SocialListItemViewModel? item)
    {
        if (item?.Payload is not SnAccountBadge badge)
        {
            return;
        }

        try
        {
            await _api.ActivateMyBadgeAsync(badge.Id).ConfigureAwait(true);
            _toast.Success("徽章已激活");
            await LoadGrowthAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task CreateEventAsync()
    {
        if (string.IsNullOrWhiteSpace(NewEventTitle))
        {
            _toast.Warning("请填写事件标题");
            return;
        }

        try
        {
            var start = DateTimeOffset.Now.AddHours(1);
            await _api.CreateCalendarEventAsync(new CreateCalendarEventRequest
            {
                Title = NewEventTitle.Trim(),
                StartTime = start,
                EndTime = start.AddHours(1),
                IsAllDay = false,
            }).ConfigureAwait(true);
            _toast.Success("已创建日程");
            NewEventTitle = string.Empty;
            await LoadCalendarAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteEventAsync(SocialListItemViewModel? item)
    {
        if (!Guid.TryParse(item?.Id, out var id))
        {
            return;
        }

        try
        {
            await _api.DeleteCalendarEventAsync(id).ConfigureAwait(true);
            _toast.Success("已删除");
            await LoadCalendarAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task CreateTicketAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTicketTitle))
        {
            _toast.Warning("请填写工单标题");
            return;
        }

        try
        {
            await _api.CreateTicketAsync(new CreateTicketRequest
            {
                Title = NewTicketTitle.Trim(),
                Content = string.IsNullOrWhiteSpace(NewTicketContent)
                    ? NewTicketTitle.Trim()
                    : NewTicketContent.Trim(),
                Type = TicketType.General,
                Priority = TicketPriority.Normal,
            }).ConfigureAwait(true);
            _toast.Success("工单已提交");
            NewTicketTitle = string.Empty;
            NewTicketContent = string.Empty;
            await LoadTicketsAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task CreatePinAsync()
    {
        try
        {
            await _api.CreatePinAsync(new CreatePinRequest
            {
                LocationName = string.IsNullOrWhiteSpace(NewPinName) ? "My pin" : NewPinName.Trim(),
                Visibility = LocationVisibility.Friends,
                KeepOnDisconnect = true,
            }).ConfigureAwait(true);
            _toast.Success("已创建 Pin");
            NewPinName = string.Empty;
            await LoadNearbyAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeletePinAsync(SocialListItemViewModel? item)
    {
        if (!Guid.TryParse(item?.Id, out var id))
        {
            return;
        }

        try
        {
            await _api.DeletePinAsync(id).ConfigureAwait(true);
            _toast.Success("已删除 Pin");
            await LoadNearbyAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task CreateMeetAsync()
    {
        try
        {
            await _api.CreateMeetAsync(new CreateMeetRequest
            {
                Notes = string.IsNullOrWhiteSpace(NewMeetNotes) ? "Meet" : NewMeetNotes.Trim(),
                Visibility = LocationVisibility.Friends,
                ExpiresInSeconds = 3600,
            }).ConfigureAwait(true);
            _toast.Success("已创建聚会");
            NewMeetNotes = string.Empty;
            await LoadNearbyAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task JoinMeetAsync(SocialListItemViewModel? item)
    {
        if (!Guid.TryParse(item?.Id, out var id))
        {
            return;
        }

        try
        {
            await _api.JoinMeetAsync(id).ConfigureAwait(true);
            _toast.Success("已加入聚会");
            await LoadNearbyAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task CompleteMeetAsync(SocialListItemViewModel? item)
    {
        if (!Guid.TryParse(item?.Id, out var id))
        {
            return;
        }

        try
        {
            await _api.CompleteMeetAsync(id).ConfigureAwait(true);
            _toast.Success("已结束聚会");
            await LoadNearbyAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task LookupNfcAsync()
    {
        if (string.IsNullOrWhiteSpace(NfcLookupQuery))
        {
            return;
        }

        try
        {
            var result = await _api.LookupNfcAsync(NfcLookupQuery.Trim()).ConfigureAwait(true);
            if (result is null)
            {
                NfcLookupResult = "未找到";
                return;
            }

            var acc = result.Account;
            NfcLookupResult =
                $"{acc?.Nick ?? acc?.Name ?? result.Id} | friend={result.IsFriend} | claimed={result.IsClaimed}";
            if (acc?.Name is { } n)
            {
                ProfileLookupName = n;
            }
        }
        catch (SolarApiException ex)
        {
            NfcLookupResult = ex.Message;
            _toast.Error(ex.Message);
        }
    }

    private SnAccount? PickOther(SnAccountRelationship r)
    {
        if (_me is null)
        {
            return r.Related ?? r.Account;
        }

        if (r.Related is not null && r.Related.Id != _me.Id)
        {
            return r.Related;
        }

        if (r.Account is not null && r.Account.Id != _me.Id)
        {
            return r.Account;
        }

        return r.Related ?? r.Account;
    }

    private bool TryResolveAccountFromItem(
        SocialListItemViewModel? item,
        out string? name,
        out Guid? accountId,
        out string? displayName)
    {
        name = null;
        accountId = item?.AccountId;
        displayName = item?.Title;

        if (item?.Payload is FriendOverviewItem { Account: { } fo })
        {
            name = fo.Name;
            accountId = fo.Id;
            displayName = fo.Nick ?? fo.Name;
            return !string.IsNullOrWhiteSpace(name);
        }

        if (item?.Payload is SnAccount acc)
        {
            name = acc.Name;
            accountId = acc.Id;
            displayName = acc.Nick ?? acc.Name;
            return !string.IsNullOrWhiteSpace(name);
        }

        if (item?.Payload is SnAccountRelationship rel)
        {
            var other = PickOther(rel);
            name = other?.Name;
            accountId = other?.Id ?? item.AccountId;
            displayName = other?.Nick ?? other?.Name ?? item.Title;
            return !string.IsNullOrWhiteSpace(name);
        }

        // Subtitle sometimes holds @handle
        if (item?.Subtitle is { Length: > 1 } sub && sub.StartsWith('@'))
        {
            name = sub[1..];
            return true;
        }

        return false;
    }

    private static SocialListItemViewModel ToEventItem(SnUserCalendarEvent e)
        => new(
            e.Id.ToString("D"),
            e.Title ?? "Event",
            e.Location ?? e.Description ?? "",
            e.StartTime?.ToLocalTime().ToString("g") ?? "",
            e);

    private static async Task SafeLoadAsync(Func<Task> load)
    {
        try
        {
            await load().ConfigureAwait(true);
        }
        catch (SolarApiException)
        {
        }
        catch (Exception)
        {
        }
    }

    private static async Task FillListAsync<T>(
        Func<Task<List<T>>> fetch,
        ObservableCollection<SocialListItemViewModel> target,
        Func<T, SocialListItemViewModel> map)
    {
        try
        {
            var list = await fetch().ConfigureAwait(true);
            foreach (var item in list)
            {
                target.Add(map(item));
            }
        }
        catch (SolarApiException)
        {
        }
    }

    private static string AppendInfo(string? existing, string next)
        => string.IsNullOrWhiteSpace(existing) ? next : existing + "\n" + next;
}
