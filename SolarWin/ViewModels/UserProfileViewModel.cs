using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

/// <summary>Other account profile (Passport) with relationship actions and one-tap DM.</summary>
public partial class UserProfileViewModel : ObservableObject
{
    private readonly ISolarApiClient _api;
    private readonly IAuthService _authService;
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;
    private readonly ChatViewModel _chat;

    private SnAccount? _account;
    private Guid? _accountId;

    public UserProfileViewModel(
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

    public ObservableCollection<SocialListItemViewModel> Badges { get; } = [];
    public ObservableCollection<SocialListItemViewModel> BoardItems { get; } = [];
    public ObservableCollection<SocialListItemViewModel> Connections { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "用户资料";

    [ObservableProperty]
    public partial string DisplayName { get; set; } = "—";

    [ObservableProperty]
    public partial string Handle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Bio { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Meta { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RelationshipText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Initials { get; set; } = "?";

    [ObservableProperty]
    public partial BitmapImage? AvatarImage { get; set; }

    [ObservableProperty]
    public partial BitmapImage? BackgroundImage { get; set; }

    [ObservableProperty]
    public partial double AvatarOpacity { get; set; }

    [ObservableProperty]
    public partial double InitialsOpacity { get; set; } = 1.0;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsMessaging { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool CanMessage { get; set; }

    /// <summary>Raised when DM room is ready; page navigates to ChatDetailPage.</summary>
    public event EventHandler<ChatRoomListItem>? DirectChatReady;

    public void Initialize(UserProfileNavArgs? args)
    {
        if (args is null || string.IsNullOrWhiteSpace(args.Name))
        {
            ErrorMessage = "缺少用户名";
            return;
        }

        Title = args.DisplayName is { Length: > 0 } d ? d : $"@{args.Name}";
        Handle = $"@{args.Name.TrimStart('@')}";
        if (args.AccountId is { } id)
        {
            _accountId = id;
        }

        _ = LoadAsync(args.Name.TrimStart('@'));
    }

    [RelayCommand]
    private async Task LoadAsync(string? name = null)
    {
        var accountName = (name ?? Handle.TrimStart('@')).Trim();
        if (string.IsNullOrWhiteSpace(accountName))
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;
            Badges.Clear();
            BoardItems.Clear();
            Connections.Clear();

            // Handle only — reject display nicks (e.g. 清沫) before the network call.
            if (!PostItemViewModel.LooksLikeAccountHandle(accountName))
            {
                ErrorMessage = $"「{accountName}」看起来是昵称而不是用户名，无法打开主页。请用 @用户名 打开。";
                return;
            }

            SnAccount acc;
            try
            {
                acc = await _api.GetAccountByNameAsync(accountName).ConfigureAwait(true);
            }
            catch (SolarApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound)
            {
                // Soft fallback: search by query in case the handle was mistyped.
                try
                {
                    var found = await _api.SearchAccountsAsync(accountName, take: 5).ConfigureAwait(true);
                    var match = found.FirstOrDefault(a =>
                        string.Equals(a.Name, accountName, StringComparison.OrdinalIgnoreCase))
                        ?? found.FirstOrDefault(a =>
                            string.Equals(a.Nick, accountName, StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(a.Name));
                    if (match is null || string.IsNullOrWhiteSpace(match.Name))
                    {
                        ErrorMessage = $"未找到用户 @{accountName}";
                        return;
                    }

                    acc = await _api.GetAccountByNameAsync(match.Name!).ConfigureAwait(true);
                }
                catch
                {
                    ErrorMessage = $"未找到用户 @{accountName}";
                    return;
                }
            }

            _account = acc;
            _accountId = acc.Id;
            ApplyAccount(acc);

            await LoadAvatarAsync(acc.Profile?.Picture).ConfigureAwait(true);
            await LoadBackgroundAsync(acc.Profile?.Background).ConfigureAwait(true);

            try
            {
                var status = await _api.GetAccountStatusAsync(accountName).ConfigureAwait(true);
                StatusText = status is null
                    ? "状态未知"
                    : $"{status.Attitude}" +
                      (string.IsNullOrWhiteSpace(status.Label) ? "" : $" · {status.Label}") +
                      (status.IsOnline ? " · 在线" : "");
            }
            catch (SolarApiException)
            {
                StatusText = "状态未知";
            }

            try
            {
                var rel = await _api.GetRelationshipAsync(acc.Id).ConfigureAwait(true);
                RelationshipText = rel is null
                    ? "尚无关系"
                    : $"关系：{rel.Status}" +
                      (string.IsNullOrWhiteSpace(rel.Alias) ? "" : $"（备注 {rel.Alias}）");
            }
            catch (SolarApiException)
            {
                RelationshipText = "关系未知";
            }

            CanMessage = acc.Id != Guid.Empty && acc.Id != _authService.CurrentAccount?.Id;

            await FillOptionalAsync(
                () => _api.GetAccountBadgesAsync(accountName),
                Badges,
                b => new SocialListItemViewModel(
                    b.Id.ToString("D"),
                    b.Label ?? b.Type ?? "徽章",
                    b.Caption ?? "",
                    b.ActivatedAt is null ? "" : "已激活",
                    b)).ConfigureAwait(true);

            await FillOptionalAsync(
                () => _api.GetAccountBoardAsync(accountName),
                BoardItems,
                b => new SocialListItemViewModel(
                    b.Id.ToString("D"),
                    b.WidgetKey ?? b.Kind.ToString(),
                    b.IsEnabled ? "启用" : "禁用",
                    $"#{b.Order}",
                    b)).ConfigureAwait(true);

            await FillOptionalAsync(
                () => _api.GetAccountConnectionsAsync(accountName),
                Connections,
                c => new SocialListItemViewModel(
                    c.Provider ?? Guid.NewGuid().ToString("N"),
                    c.Provider ?? "连接",
                    c.ProvidedIdentifier ?? "",
                    c.Url ?? "",
                    c)).ConfigureAwait(true);
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
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);

    [RelayCommand]
    private async Task StartDirectChatAsync()
    {
        if (_accountId is not { } id || id == Guid.Empty)
        {
            _toast.Warning("无法发起私聊：缺少用户 ID");
            return;
        }

        if (id == _authService.CurrentAccount?.Id)
        {
            _toast.Warning("不能与自己私聊");
            return;
        }

        try
        {
            IsMessaging = true;
            var item = await _chat.EnsureDirectChatAsync(id, DisplayName).ConfigureAwait(true);
            if (item is not null)
            {
                DirectChatReady?.Invoke(this, item);
            }
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
        finally
        {
            IsMessaging = false;
        }
    }

    [RelayCommand]
    private async Task SendFriendRequestAsync()
    {
        if (_accountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.SendFriendRequestAsync(id).ConfigureAwait(true);
            _toast.Success("已发送好友请求");
            RelationshipText = "好友请求已发送";
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task BlockAsync()
    {
        if (_accountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.BlockAccountAsync(id).ConfigureAwait(true);
            _toast.Success("已拉黑");
            RelationshipText = "已拉黑";
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task UnblockAsync()
    {
        if (_accountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.UnblockAccountAsync(id).ConfigureAwait(true);
            _toast.Success("已取消拉黑");
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task MuteAsync()
    {
        if (_accountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.MuteAccountAsync(id).ConfigureAwait(true);
            _toast.Success("已静音");
            RelationshipText = "已静音";
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task UnmuteAsync()
    {
        if (_accountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.UnmuteAccountAsync(id).ConfigureAwait(true);
            _toast.Success("已取消静音");
            await LoadAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    [RelayCommand]
    private async Task SetCloseFriendAsync()
    {
        if (_accountId is not { } id)
        {
            return;
        }

        try
        {
            await _api.SetCloseFriendAsync(id, isCloseFriend: true).ConfigureAwait(true);
            _toast.Success("已设为密友");
            RelationshipText = "密友";
        }
        catch (SolarApiException ex)
        {
            _toast.Error(ex.Message);
        }
    }

    private void ApplyAccount(SnAccount acc)
    {
        var profile = acc.Profile;
        DisplayName = acc.Nick ?? acc.Name ?? "用户";
        Handle = string.IsNullOrWhiteSpace(acc.Name) ? string.Empty : $"@{acc.Name}";
        Bio = profile?.Bio ?? "（无简介）";
        Meta =
            $"Lv{profile?.Level ?? 0} · 积分 {profile?.SocialCredits:0.##} · Perk {acc.PerkLevel}\n" +
            $"位置 {profile?.Location ?? "—"} · 性别 {profile?.Gender ?? "—"}";
        Title = DisplayName;
        Initials = DisplayName.Length > 0 ? DisplayName[..1].ToUpperInvariant() : "?";
    }

    private async Task LoadAvatarAsync(SnCloudFile? picture)
    {
        var url = CloudFileUrlHelper.Resolve(picture);
        if (string.IsNullOrWhiteSpace(url))
        {
            AvatarImage = null;
            AvatarOpacity = 0;
            InitialsOpacity = 1;
            return;
        }

        try
        {
            var img = await _imageLoader.LoadAsync(url).ConfigureAwait(true);
            AvatarImage = img;
            AvatarOpacity = img is null ? 0 : 1;
            InitialsOpacity = img is null ? 1 : 0;
        }
        catch
        {
            AvatarOpacity = 0;
            InitialsOpacity = 1;
        }
    }

    private async Task LoadBackgroundAsync(SnCloudFile? background)
    {
        var url = CloudFileUrlHelper.Resolve(background);
        if (string.IsNullOrWhiteSpace(url))
        {
            BackgroundImage = null;
            return;
        }

        try
        {
            BackgroundImage = await _imageLoader.LoadAsync(url).ConfigureAwait(true);
        }
        catch
        {
            BackgroundImage = null;
        }
    }

    private static async Task FillOptionalAsync<T>(
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
}
