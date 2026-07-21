using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly ISolarApiClient _api;
    private readonly IAuthService _authService;
    private readonly IToastService _toast;
    private readonly DysonFileImageLoader _imageLoader;

    private SnAccount? _account;
    private SnAccountProfile? _profile;
    private string? _currentPictureId;
    private string? _pendingPictureId;

    public ProfileViewModel(
        ISolarApiClient api,
        IAuthService authService,
        IToastService toast,
        DysonFileImageLoader imageLoader)
    {
        _api = api;
        _authService = authService;
        _toast = toast;
        _imageLoader = imageLoader;
    }

    [ObservableProperty]
    public partial string DisplayName { get; set; } = "—";

    [ObservableProperty]
    public partial string Handle { get; set; } = "—";

    [ObservableProperty]
    public partial string Bio { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Meta { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Initials { get; set; } = "?";

    [ObservableProperty]
    public partial string? AvatarUrl { get; set; }

    [ObservableProperty]
    public partial BitmapImage? AvatarImage { get; set; }

    [ObservableProperty]
    public partial double AvatarOpacity { get; set; }

    [ObservableProperty]
    public partial double InitialsOpacity { get; set; } = 1.0;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "状态未知";

    [ObservableProperty]
    public partial string StatusDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int SelectedAttitudeIndex { get; set; }

    [ObservableProperty]
    public partial string CheckInSummary { get; set; } = "尚未加载签到信息";

    [ObservableProperty]
    public partial string FortuneText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsCheckingIn { get; set; }

    [ObservableProperty]
    public partial bool IsSettingStatus { get; set; }

    [ObservableProperty]
    public partial bool IsUploadingAvatar { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? InfoMessage { get; set; }

    /// <summary>Edit dialog draft fields.</summary>
    public string EditFirstName { get; set; } = string.Empty;
    public string EditLastName { get; set; } = string.Empty;
    public string EditBio { get; set; } = string.Empty;
    public string EditLocation { get; set; } = string.Empty;
    public string EditGender { get; set; } = string.Empty;

    /// <summary>Preview image while editing (may be newly picked).</summary>
    public BitmapImage? EditAvatarPreview { get; set; }

    public string? PendingPictureId => _pendingPictureId ?? _currentPictureId;

    public event EventHandler? LoggedOut;
    public event EventHandler? EditProfileRequested;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            InfoMessage = null;

            SnAccount me;
            try
            {
                me = await _api.GetPassportMeAsync().ConfigureAwait(true);
            }
            catch (SolarApiException)
            {
                me = await _api.GetMeAsync().ConfigureAwait(true);
            }

            _account = me;
            _profile = me.Profile;
            try
            {
                _profile = await _api.GetMyProfileAsync().ConfigureAwait(true);
            }
            catch (SolarApiException)
            {
                // keep nested
            }

            ApplyAccountUi(me, _profile);
            await LoadAvatarAsync(_profile?.Picture).ConfigureAwait(true);
            await LoadStatusAsync().ConfigureAwait(true);
            await LoadCheckInInfoAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            ApplyCachedFallback();
            _ = LoadAvatarAsync(_authService.CurrentAccount?.Profile?.Picture);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RequestEditProfile()
    {
        EditFirstName = _profile?.FirstName ?? string.Empty;
        EditLastName = _profile?.LastName ?? string.Empty;
        EditBio = _profile?.Bio ?? string.Empty;
        EditLocation = _profile?.Location ?? string.Empty;
        EditGender = _profile?.Gender ?? string.Empty;
        _pendingPictureId = null;
        EditAvatarPreview = AvatarImage;
        EditProfileRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Upload avatar and immediately PATCH profile.picture_id (does not clear other fields).
    /// </summary>
    public async Task<bool> ChangeAvatarAndSaveAsync(Stream stream, string fileName, string contentType, long size)
    {
        var uploaded = await UploadAvatarAsync(stream, fileName, contentType, size).ConfigureAwait(true);
        if (!uploaded || string.IsNullOrWhiteSpace(_pendingPictureId))
        {
            return false;
        }

        return await SaveProfileAsync(
            _profile?.FirstName ?? EditFirstName,
            _profile?.LastName ?? EditLastName,
            _profile?.Bio ?? EditBio,
            _profile?.Location ?? EditLocation,
            _profile?.Gender ?? EditGender,
            _pendingPictureId).ConfigureAwait(true);
    }

    /// <summary>Upload avatar to DysonFS; keeps file id for profile save.</summary>
    public async Task<bool> UploadAvatarAsync(Stream stream, string fileName, string contentType, long size)
    {
        try
        {
            IsUploadingAvatar = true;
            ErrorMessage = null;

            var file = await _api.UploadFileDirectAsync(
                stream,
                fileName,
                contentType,
                size,
                parentId: null,
                progress: null,
                CancellationToken.None).ConfigureAwait(true);

            var id = file.Id ?? CloudFileUrlHelper.ResolveFileId(file);
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new SolarApiException("上传成功但未返回文件 id。");
            }

            _pendingPictureId = id;
            var url = CloudFileUrlHelper.DriveFileUrl(id);
            var bmp = await _imageLoader.LoadAsync(id).ConfigureAwait(true)
                      ?? await _imageLoader.LoadAsync(url).ConfigureAwait(true);
            if (bmp is not null)
            {
                EditAvatarPreview = bmp;
            }

            InfoMessage = "头像已上传，保存资料后生效";
            _toast.Success("头像已上传");
            return true;
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("头像上传失败");
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("头像上传失败");
            return false;
        }
        finally
        {
            IsUploadingAvatar = false;
        }
    }

    public async Task<bool> SaveProfileAsync(
        string firstName,
        string lastName,
        string bio,
        string location,
        string gender,
        string? pictureId = null)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var pic = NullIfWhiteSpace(pictureId) ?? NullIfWhiteSpace(_pendingPictureId);

            var request = new ProfileRequest
            {
                FirstName = NullIfWhiteSpace(firstName),
                LastName = NullIfWhiteSpace(lastName),
                Bio = NullIfWhiteSpace(bio),
                Location = NullIfWhiteSpace(location),
                Gender = NullIfWhiteSpace(gender),
                PictureId = pic,
            };

            _profile = await _api.UpdateMyProfileAsync(request).ConfigureAwait(true);
            if (_account is not null)
            {
                _account.Profile = _profile;
            }

            _currentPictureId = CloudFileUrlHelper.ResolveFileId(_profile.Picture) ?? pic;
            _pendingPictureId = null;

            ApplyAccountUi(_account, _profile);
            await LoadAvatarAsync(_profile.Picture).ConfigureAwait(true);

            InfoMessage = "资料已更新";
            _toast.Success("资料已更新");
            return true;
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("资料更新失败");
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckInAsync()
    {
        try
        {
            IsCheckingIn = true;
            ErrorMessage = null;
            InfoMessage = null;

            var result = await _api.DoCheckInAsync().ConfigureAwait(true);
            ApplyCheckInResult(result, justCheckedIn: true);
            InfoMessage = "签到成功";
            _toast.Success("签到成功");
        }
        catch (SolarApiException ex)
        {
            try
            {
                var existing = await _api.GetCheckInAsync().ConfigureAwait(true);
                if (existing is not null)
                {
                    ApplyCheckInResult(existing, justCheckedIn: false);
                    InfoMessage = "今日已签到";
                    _toast.Warning("今日已签到");
                    return;
                }
            }
            catch (SolarApiException)
            {
                // ignore
            }

            ErrorMessage = ex.Message;
            _toast.Error("签到失败");
        }
        finally
        {
            IsCheckingIn = false;
        }
    }

    [RelayCommand]
    private async Task SetStatusOnlineAsync() => await SetAttitudeAsync(StatusAttitude.Online).ConfigureAwait(true);

    [RelayCommand]
    private async Task SetStatusIdleAsync() => await SetAttitudeAsync(StatusAttitude.Idle).ConfigureAwait(true);

    [RelayCommand]
    private async Task SetStatusDndAsync() => await SetAttitudeAsync(StatusAttitude.DoNotDisturb).ConfigureAwait(true);

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try
        {
            await _authService.LogoutAsync().ConfigureAwait(false);
        }
        catch
        {
            // still leave locally
        }

        void FinishOnUi()
        {
            if (App.Window is MainWindow mainWindow)
            {
                mainWindow.NavigateToLogin();
            }

            LoggedOut?.Invoke(this, EventArgs.Empty);
        }

        var dq = App.DispatcherQueue;
        if (dq is null || dq.HasThreadAccess)
        {
            FinishOnUi();
        }
        else
        {
            dq.TryEnqueue(FinishOnUi);
        }
    }

    private async Task LoadAvatarAsync(SnCloudFile? picture)
    {
        var id = CloudFileUrlHelper.ResolveFileId(picture);
        var url = CloudFileUrlHelper.Resolve(picture);
        AvatarUrl = url;
        _currentPictureId = id;

        if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(url))
        {
            AvatarImage = null;
            AvatarOpacity = 0;
            InitialsOpacity = 1;
            return;
        }

        try
        {
            var bmp = await _imageLoader.LoadAsync(id ?? url).ConfigureAwait(true);
            if (bmp is not null)
            {
                AvatarImage = bmp;
                AvatarOpacity = 1;
                InitialsOpacity = 0;
                return;
            }
        }
        catch
        {
            // fall through
        }

        AvatarImage = null;
        AvatarOpacity = 0;
        InitialsOpacity = 1;
    }

    private async Task SetAttitudeAsync(StatusAttitude attitude)
    {
        try
        {
            IsSettingStatus = true;
            ErrorMessage = null;

            var request = new StatusRequest
            {
                Attitude = attitude,
                Type = StatusType.None,
                IsAutomated = false,
                Label = attitude switch
                {
                    StatusAttitude.Online => "在线",
                    StatusAttitude.Idle => "离开",
                    StatusAttitude.DoNotDisturb => "请勿打扰",
                    _ => null,
                },
                AppIdentifier = "SolarWin",
            };

            var status = await _api.SetMyStatusAsync(request).ConfigureAwait(true);
            ApplyStatusUi(status);
            SelectedAttitudeIndex = (int)attitude;
            InfoMessage = $"状态已设为：{StatusText}";
            _toast.Success(InfoMessage);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("状态设置失败");
        }
        finally
        {
            IsSettingStatus = false;
        }
    }

    private async Task LoadStatusAsync()
    {
        try
        {
            var status = await _api.GetMyStatusAsync().ConfigureAwait(true);
            ApplyStatusUi(status);
            SelectedAttitudeIndex = (int)status.Attitude;
        }
        catch (SolarApiException)
        {
            StatusText = "状态未知";
            StatusDetail = string.Empty;
        }
    }

    private async Task LoadCheckInInfoAsync()
    {
        try
        {
            var result = await _api.GetCheckInAsync().ConfigureAwait(true);
            if (result is null)
            {
                CheckInSummary = "今日尚未签到";
                FortuneText = string.Empty;
                return;
            }

            ApplyCheckInResult(result, justCheckedIn: false);
        }
        catch (SolarApiException)
        {
            CheckInSummary = "无法获取签到信息";
            FortuneText = string.Empty;
        }
    }

    private void ApplyCheckInResult(SnCheckInResult result, bool justCheckedIn)
    {
        var levelName = result.Level.ToString();
        CheckInSummary =
            $"{(justCheckedIn ? "签到成功" : "今日已签到")} · 运势等级 {levelName}\n" +
            $"奖励积分：{result.RewardPoints?.ToString("0.##") ?? "—"} · 经验：{result.RewardExperience?.ToString() ?? "—"}";

        var fortune = result.FortuneReport;
        if (fortune is null)
        {
            FortuneText = result.Tips is { Count: > 0 }
                ? string.Join("\n", result.Tips.Select(t => $"· {t.Title}: {t.Content}"))
                : string.Empty;
            return;
        }

        FortuneText = string.Join("\n", new[]
        {
            fortune.Summary is null ? null : $"摘要：{fortune.Summary}",
            fortune.Poem is null ? null : $"诗词：{fortune.Poem}",
            fortune.LuckyColor is null ? null : $"幸运色：{fortune.LuckyColor}",
            fortune.LuckyDirection is null ? null : $"方位：{fortune.LuckyDirection}",
            fortune.Career is null ? null : $"事业：{fortune.Career}",
            fortune.Love is null ? null : $"感情：{fortune.Love}",
            fortune.Health is null ? null : $"健康：{fortune.Health}",
        }.Where(s => s is not null)!);
    }

    private void ApplyStatusUi(SnAccountStatus status)
    {
        StatusText = status.Attitude switch
        {
            StatusAttitude.Online => "在线",
            StatusAttitude.Idle => "离开",
            StatusAttitude.DoNotDisturb => "请勿打扰",
            _ => status.Label ?? "状态",
        };

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(status.Label) && status.Label != StatusText)
        {
            parts.Add(status.Label!);
        }

        if (!string.IsNullOrWhiteSpace(status.Symbol))
        {
            parts.Add(status.Symbol!);
        }

        parts.Add(status.IsOnline ? "在线标记" : "离线标记");
        if (status.IsIdle)
        {
            parts.Add("空闲中");
        }

        StatusDetail = string.Join(" · ", parts);
    }

    private void ApplyAccountUi(SnAccount? me, SnAccountProfile? profile)
    {
        var nick = me?.Nick ?? me?.Name ?? "用户";
        DisplayName = nick;
        Handle = me?.Name is null ? "—" : $"@{me.Name}";
        Bio = string.IsNullOrWhiteSpace(profile?.Bio) ? "（暂无简介）" : profile!.Bio!;
        Meta = $"Lv{profile?.Level ?? 0} · 经验 {profile?.Experience ?? 0} · Perk {me?.PerkLevel ?? 0}";
        AvatarUrl = CloudFileUrlHelper.Resolve(profile?.Picture);
        _currentPictureId = CloudFileUrlHelper.ResolveFileId(profile?.Picture);
        Initials = string.IsNullOrWhiteSpace(nick) ? "?" : nick[..1].ToUpperInvariant();
    }

    private void ApplyCachedFallback()
    {
        var cached = _authService.CurrentAccount;
        if (cached is null)
        {
            return;
        }

        ApplyAccountUi(cached, cached.Profile);
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
