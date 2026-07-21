using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;
using SolarWin.Views;

namespace SolarWin.ViewModels;

public partial class SecurityViewModel : ObservableObject
{
    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;

    public SecurityViewModel(ISolarApiClient api, IToastService toast)
    {
        _api = api;
        _toast = toast;
    }

    /// <summary>Set by SecurityPage for dialogs.</summary>
    public XamlRoot? XamlRoot { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBusy { get; set; }

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string NewContactContent { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int NewContactTypeIndex { get; set; }

    [ObservableProperty]
    public partial string NewApiKeyLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? LastCreatedApiKeySecret { get; set; }

    [ObservableProperty]
    public partial string DeviceLabelDraft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ContactVerifyCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PasskeyLabel { get; set; } = "Windows Hello";

    public ObservableCollection<DeviceRowViewModel> Devices { get; } = [];
    public ObservableCollection<SessionRowViewModel> Sessions { get; } = [];
    public ObservableCollection<FactorRowViewModel> Factors { get; } = [];
    public ObservableCollection<ContactRowViewModel> Contacts { get; } = [];
    public ObservableCollection<AppRowViewModel> AuthorizedApps { get; } = [];
    public ObservableCollection<ApiKeyRowViewModel> ApiKeys { get; } = [];
    public ObservableCollection<ConnectionRowViewModel> Connections { get; } = [];
    public ObservableCollection<PendingChallengeRowViewModel> PendingChallenges { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;
        try
        {
            // Load sections independently so one failure does not block the rest.
            await LoadDevicesAsync().ConfigureAwait(true);
            await LoadSessionsAsync().ConfigureAwait(true);
            await LoadFactorsAsync().ConfigureAwait(true);
            await LoadContactsAsync().ConfigureAwait(true);
            await LoadAppsAsync().ConfigureAwait(true);
            await LoadApiKeysAsync().ConfigureAwait(true);
            await LoadConnectionsAsync().ConfigureAwait(true);
            await LoadPendingAsync().ConfigureAwait(true);
            StatusMessage = "安全信息已刷新";
        }
        catch (Exception ex)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(ex.Message) ? "加载失败" : ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadDevicesAsync()
    {
        try
        {
            var list = await _api.GetDevicesAsync().ConfigureAwait(true);
            Devices.Clear();
            foreach (var d in list)
            {
                Devices.Add(new DeviceRowViewModel(d));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = AppendError(ErrorMessage, "设备", ex);
        }
    }

    private async Task LoadSessionsAsync()
    {
        try
        {
            var list = await _api.GetSessionsAsync().ConfigureAwait(true);
            Sessions.Clear();
            foreach (var s in list)
            {
                Sessions.Add(new SessionRowViewModel(s));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = AppendError(ErrorMessage, "会话", ex);
        }
    }

    private async Task LoadFactorsAsync()
    {
        try
        {
            var list = await _api.GetAccountFactorsAsync().ConfigureAwait(true);
            Factors.Clear();
            foreach (var f in list)
            {
                Factors.Add(new FactorRowViewModel(f));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = AppendError(ErrorMessage, "认证因素", ex);
        }
    }

    private async Task LoadContactsAsync()
    {
        try
        {
            var list = await _api.GetContactsAsync().ConfigureAwait(true);
            Contacts.Clear();
            foreach (var c in list)
            {
                Contacts.Add(new ContactRowViewModel(c));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = AppendError(ErrorMessage, "联系方式", ex);
        }
    }

    private async Task LoadAppsAsync()
    {
        try
        {
            var list = await _api.GetAuthorizedAppsAsync().ConfigureAwait(true);
            AuthorizedApps.Clear();
            foreach (var a in list)
            {
                AuthorizedApps.Add(new AppRowViewModel(a));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = AppendError(ErrorMessage, "授权应用", ex);
        }
    }

    private async Task LoadApiKeysAsync()
    {
        try
        {
            var list = await _api.GetApiKeysAsync().ConfigureAwait(true);
            ApiKeys.Clear();
            foreach (var k in list)
            {
                ApiKeys.Add(new ApiKeyRowViewModel(k));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = AppendError(ErrorMessage, "API 密钥", ex);
        }
    }

    private async Task LoadConnectionsAsync()
    {
        try
        {
            var list = await _api.GetConnectionsAsync().ConfigureAwait(true);
            Connections.Clear();
            foreach (var c in list)
            {
                Connections.Add(new ConnectionRowViewModel(c));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = AppendError(ErrorMessage, "第三方连接", ex);
        }
    }

    private async Task LoadPendingAsync()
    {
        try
        {
            var list = await _api.GetPendingChallengesAsync().ConfigureAwait(true);
            PendingChallenges.Clear();
            foreach (var c in list)
            {
                PendingChallenges.Add(new PendingChallengeRowViewModel(c));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = AppendError(ErrorMessage, "待批准登录", ex);
        }
    }

    [RelayCommand]
    private async Task DeleteDeviceAsync(DeviceRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.DeleteDeviceAsync(row.Id).ConfigureAwait(true);
            Devices.Remove(row);
            _toast.Success("已移除设备");
        }, "移除设备失败");
    }

    [RelayCommand]
    private async Task SaveDeviceLabelAsync(DeviceRowViewModel? row)
    {
        if (row is null || string.IsNullOrWhiteSpace(row.LabelDraft))
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.UpdateDeviceLabelAsync(row.Id, row.LabelDraft.Trim()).ConfigureAwait(true);
            row.ApplyLabel(row.LabelDraft.Trim());
            _toast.Success("设备标签已更新");
        }, "更新设备标签失败");
    }

    [RelayCommand]
    private async Task SaveCurrentDeviceLabelAsync()
    {
        if (string.IsNullOrWhiteSpace(DeviceLabelDraft))
        {
            ErrorMessage = "请输入当前设备标签。";
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.UpdateCurrentDeviceLabelAsync(DeviceLabelDraft.Trim()).ConfigureAwait(true);
            _toast.Success("当前设备标签已更新");
            await LoadDevicesAsync().ConfigureAwait(true);
        }, "更新当前设备标签失败");
    }

    [RelayCommand]
    private async Task RevokeSessionAsync(SessionRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.RevokeSessionAsync(row.Id).ConfigureAwait(true);
            Sessions.Remove(row);
            _toast.Success("已撤销会话");
        }, "撤销会话失败");
    }

    [RelayCommand]
    private async Task EnableFactorAsync(FactorRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.EnableFactorAsync(row.Id).ConfigureAwait(true);
            await LoadFactorsAsync().ConfigureAwait(true);
            _toast.Success("已启用");
        }, "启用失败");
    }

    [RelayCommand]
    private async Task DisableFactorAsync(FactorRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.DisableFactorAsync(row.Id).ConfigureAwait(true);
            await LoadFactorsAsync().ConfigureAwait(true);
            _toast.Success("已禁用");
        }, "禁用失败");
    }

    [RelayCommand]
    private async Task DeleteFactorAsync(FactorRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.DeleteFactorAsync(row.Id).ConfigureAwait(true);
            Factors.Remove(row);
            _toast.Success("已删除认证因素");
        }, "删除失败");
    }

    [RelayCommand]
    private async Task CreateContactAsync()
    {
        if (string.IsNullOrWhiteSpace(NewContactContent))
        {
            ErrorMessage = "请输入邮箱或手机号。";
            return;
        }

        var type = NewContactTypeIndex switch
        {
            1 => AccountContactType.Phone,
            2 => AccountContactType.Im,
            _ => AccountContactType.Email,
        };

        await RunActionAsync(async () =>
        {
            await _api.CreateContactAsync(new ContactRequest
            {
                Type = type,
                Content = NewContactContent.Trim(),
                IsPublic = false,
            }).ConfigureAwait(true);
            NewContactContent = string.Empty;
            await LoadContactsAsync().ConfigureAwait(true);
            _toast.Success("联系方式已添加（可能需要验证）");
        }, "添加联系方式失败");
    }

    [RelayCommand]
    private async Task DeleteContactAsync(ContactRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.DeleteContactAsync(row.Id).ConfigureAwait(true);
            Contacts.Remove(row);
            _toast.Success("已删除联系方式");
        }, "删除联系方式失败");
    }

    [RelayCommand]
    private async Task SetPrimaryContactAsync(ContactRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.SetContactPrimaryAsync(row.Id).ConfigureAwait(true);
            await LoadContactsAsync().ConfigureAwait(true);
            _toast.Success("已设为主要联系方式");
        }, "设置失败");
    }

    [RelayCommand]
    private async Task RequestContactVerifyAsync(ContactRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            try
            {
                await _api.RequestContactVerificationAsync(row.Id).ConfigureAwait(true);
            }
            catch (SolarApiException)
            {
                // Some deployments only send code on create; still guide the user.
            }

            StatusMessage = $"已请求向 {row.Title} 发送验证码（若服务端支持）。请查收邮件/短信后输入验证码。";
            _toast.Success("已请求发送验证码");
        }, "请求验证码失败");
    }

    [RelayCommand]
    private async Task VerifyContactCodeAsync(ContactRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var code = string.IsNullOrWhiteSpace(row.VerifyCodeDraft)
            ? ContactVerifyCode
            : row.VerifyCodeDraft;
        if (string.IsNullOrWhiteSpace(code))
        {
            ErrorMessage = "请输入验证码。";
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.VerifyContactAsync(row.Id, code.Trim()).ConfigureAwait(true);
            row.VerifyCodeDraft = string.Empty;
            ContactVerifyCode = string.Empty;
            await LoadContactsAsync().ConfigureAwait(true);
            _toast.Success("联系方式已验证");
        }, "验证失败");
    }

    [RelayCommand]
    private async Task RegisterPasskeyAsync()
    {
        if (XamlRoot is null)
        {
            ErrorMessage = "界面未就绪。";
            return;
        }

        await RunActionAsync(async () =>
        {
            StatusMessage = "开始 Passkey 注册…";
            var optionsJson = await _api.StartPasskeyRegistrationWithDeviceAsync().ConfigureAwait(true);
            var dialog = new WebAuthnDialog("create", optionsJson) { XamlRoot = XamlRoot };
            _ = await dialog.ShowAsync();
            if (string.IsNullOrWhiteSpace(dialog.ResultJson))
            {
                throw new SolarApiException(dialog.ErrorMessage ?? "Passkey 注册已取消。");
            }

            using var doc = JsonDocument.Parse(dialog.ResultJson);
            var root = doc.RootElement;
            string? Get(params string[] names)
            {
                foreach (var n in names)
                {
                    if (root.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String)
                    {
                        return p.GetString();
                    }
                }

                return null;
            }

            var complete = new PasskeyRegistrationCompleteRequest
            {
                DeviceId = DeviceInfoHelper.GetDeviceId(),
                ClientDataJson = Get("clientDataJSON", "client_data_json"),
                AttestationObject = Get("attestationObject", "attestation_object"),
                Label = string.IsNullOrWhiteSpace(PasskeyLabel) ? "SolarWin" : PasskeyLabel.Trim(),
            };

            // Some APIs expect the raw credential JSON — try structured first, fallback to raw.
            try
            {
                await _api.CompletePasskeyRegistrationDetailedAsync(complete).ConfigureAwait(true);
            }
            catch (SolarApiException)
            {
                await _api.CompletePasskeyRegistrationAsync(dialog.ResultJson).ConfigureAwait(true);
            }

            await LoadFactorsAsync().ConfigureAwait(true);
            _toast.Success("Passkey 已注册");
            StatusMessage = "Passkey 注册成功。";
        }, "Passkey 注册失败");
    }

    [RelayCommand]
    private async Task RevokeAppAsync(AppRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.RevokeAuthorizedAppAsync(row.Id).ConfigureAwait(true);
            AuthorizedApps.Remove(row);
            _toast.Success("已撤销应用授权");
        }, "撤销失败");
    }

    [RelayCommand]
    private async Task CreateApiKeyAsync()
    {
        var label = string.IsNullOrWhiteSpace(NewApiKeyLabel) ? "SolarWin" : NewApiKeyLabel.Trim();
        await RunActionAsync(async () =>
        {
            var key = await _api.CreateApiKeyAsync(new CreateApiKeyRequest { Label = label }).ConfigureAwait(true);
            LastCreatedApiKeySecret = string.IsNullOrWhiteSpace(key.DisplaySecret)
                ? "（服务端未返回明文密钥，请在创建响应中查看）"
                : key.DisplaySecret;
            NewApiKeyLabel = string.Empty;
            await LoadApiKeysAsync().ConfigureAwait(true);
            _toast.Success("API 密钥已创建，请立即复制保存");
        }, "创建 API 密钥失败");
    }

    [RelayCommand]
    private async Task RotateApiKeyAsync(ApiKeyRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            var key = await _api.RotateApiKeyAsync(row.Id).ConfigureAwait(true);
            LastCreatedApiKeySecret = string.IsNullOrWhiteSpace(key.DisplaySecret)
                ? "（已轮换，但未返回明文）"
                : key.DisplaySecret;
            await LoadApiKeysAsync().ConfigureAwait(true);
            _toast.Success("密钥已轮换");
        }, "轮换失败");
    }

    [RelayCommand]
    private async Task DeleteApiKeyAsync(ApiKeyRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.DeleteApiKeyAsync(row.Id).ConfigureAwait(true);
            ApiKeys.Remove(row);
            _toast.Success("已删除 API 密钥");
        }, "删除失败");
    }

    [RelayCommand]
    private async Task DeleteConnectionAsync(ConnectionRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.DeleteConnectionAsync(row.Id).ConfigureAwait(true);
            Connections.Remove(row);
            _toast.Success("已断开连接");
        }, "断开失败");
    }

    [RelayCommand]
    private async Task ToggleConnectionVisibilityAsync(ConnectionRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.SetConnectionVisibilityAsync(row.Id, !row.IsPublic).ConfigureAwait(true);
            await LoadConnectionsAsync().ConfigureAwait(true);
            _toast.Success("可见性已更新");
        }, "更新可见性失败");
    }

    [RelayCommand]
    private async Task ApproveChallengeAsync(PendingChallengeRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.ApproveChallengeAsync(row.Id).ConfigureAwait(true);
            PendingChallenges.Remove(row);
            _toast.Success("已批准登录请求");
        }, "批准失败");
    }

    [RelayCommand]
    private async Task DeclineChallengeAsync(PendingChallengeRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        await RunActionAsync(async () =>
        {
            await _api.DeclineChallengeAsync(row.Id).ConfigureAwait(true);
            PendingChallenges.Remove(row);
            _toast.Success("已拒绝登录请求");
        }, "拒绝失败");
    }

    private async Task RunActionAsync(Func<Task> action, string failTitle)
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(ex.ApiMessage)
                ? $"{failTitle}：{ex.Message}"
                : $"{failTitle}：{ex.ApiMessage}";
            _toast.Error(ErrorMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"{failTitle}：{ex.Message}";
            _toast.Error(ErrorMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string AppendError(string? existing, string section, Exception ex)
    {
        var msg = ex is SolarApiException se && !string.IsNullOrWhiteSpace(se.ApiMessage)
            ? se.ApiMessage!
            : ex.Message;
        var line = $"{section}：{msg}";
        return string.IsNullOrWhiteSpace(existing) ? line : existing + "\n" + line;
    }
}

public sealed partial class DeviceRowViewModel : ObservableObject
{
    public DeviceRowViewModel(SnAuthClientWithSessions device)
    {
        Id = device.Id;
        Title = device.DeviceLabel ?? device.DeviceName ?? device.DeviceId ?? Id.ToString("D")[..8];
        Subtitle =
            $"{device.Platform} · 会话 {device.Sessions?.Count ?? 0}"
            + (device.CreatedAt is { } c ? $" · {c.ToLocalTime():g}" : string.Empty);
        LabelDraft = device.DeviceLabel ?? device.DeviceName ?? string.Empty;
    }

    public Guid Id { get; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Subtitle { get; set; }

    [ObservableProperty]
    public partial string LabelDraft { get; set; }

    public void ApplyLabel(string label)
    {
        Title = label;
        LabelDraft = label;
    }
}

public sealed class SessionRowViewModel
{
    public SessionRowViewModel(SnAuthSession session)
    {
        Id = session.Id;
        Title = session.Type ?? "会话";
        Subtitle = string.Join(" · ", new[]
        {
            session.IpAddress,
            session.UserAgent is { Length: > 48 } ua ? ua[..48] + "…" : session.UserAgent,
            session.LastGrantedAt?.ToLocalTime().ToString("g"),
            session.ExpiredAt is { } exp ? $"过期 {exp.ToLocalTime():g}" : null,
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
}

public sealed class FactorRowViewModel
{
    public FactorRowViewModel(SnAccountAuthFactor factor)
    {
        Id = factor.Id;
        Title = factor.TypeDisplayName;
        Subtitle = factor.IsEnabled
            ? $"已启用 · 信任度 {factor.Trustworthy}"
            : "未启用";
        IsEnabled = factor.IsEnabled;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public bool IsEnabled { get; }
}

public sealed partial class ContactRowViewModel : ObservableObject
{
    public ContactRowViewModel(SnAccountContact contact)
    {
        Id = contact.Id;
        Title = contact.Content ?? "(空)";
        IsVerified = contact.VerifiedAt is not null;
        var flags = new List<string> { contact.Type.ToString() };
        if (contact.IsPrimary)
        {
            flags.Add("主要");
        }

        if (IsVerified)
        {
            flags.Add("已验证");
        }

        if (contact.IsPublic)
        {
            flags.Add("公开");
        }

        Subtitle = string.Join(" · ", flags);
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public bool IsVerified { get; }

    [ObservableProperty]
    public partial string VerifyCodeDraft { get; set; } = string.Empty;
}

public sealed class AppRowViewModel
{
    public AppRowViewModel(AuthorizedAppResponse app)
    {
        Id = app.Id;
        Title = app.AppName ?? app.AppSlug ?? app.Id.ToString("D")[..8];
        Subtitle = string.Join(" · ", new[]
        {
            app.Type,
            app.LastAuthorizedAt?.ToLocalTime().ToString("g"),
            app.Scopes is { Count: > 0 } s ? string.Join(",", s.Take(3)) : null,
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
}

public sealed class ApiKeyRowViewModel
{
    public ApiKeyRowViewModel(SnApiKey key)
    {
        Id = key.Id;
        Title = key.DisplayLabel;
        Subtitle = string.Join(" · ", new[]
        {
            key.Prefix ?? key.KeyPrefix,
            key.CreatedAt?.ToLocalTime().ToString("g"),
            key.LastUsedAt is { } u ? $"上次 {u.ToLocalTime():g}" : null,
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
}

public sealed class ConnectionRowViewModel
{
    public ConnectionRowViewModel(SnAccountConnection connection)
    {
        Id = connection.Id;
        Title = connection.DisplayTitle;
        Subtitle = connection.DisplaySubtitle + (connection.IsPublic ? " · 公开" : " · 私密");
        IsPublic = connection.IsPublic;
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public bool IsPublic { get; }
}

public sealed class PendingChallengeRowViewModel
{
    public PendingChallengeRowViewModel(SnAuthChallenge challenge)
    {
        Id = challenge.Id;
        Title = challenge.DeviceName ?? challenge.DeviceId ?? "未知设备";
        Subtitle = string.Join(" · ", new[]
        {
            challenge.Platform.ToString(),
            challenge.IpAddress,
            challenge.CreatedAt?.ToLocalTime().ToString("g"),
            $"剩余 {challenge.StepRemain}/{Math.Max(1, challenge.StepTotal)} 步",
        }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    public Guid Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
}
