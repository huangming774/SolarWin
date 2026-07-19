using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly ISolarApiClient _api;
    private readonly IAuthService _authService;

    public HomeViewModel(ISolarApiClient api, IAuthService authService)
    {
        _api = api;
        _authService = authService;
    }

    [ObservableProperty]
    public partial string Title { get; set; } = "首页";

    [ObservableProperty]
    public partial string Summary { get; set; } = "正在加载…";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var me = await _api.GetMeAsync().ConfigureAwait(true);
            var unreadRooms = 0;
            try
            {
                var chatSummary = await _api.GetChatSummaryAsync().ConfigureAwait(true);
                unreadRooms = chatSummary.Count(kv => kv.Value.UnreadCount > 0);
            }
            catch (SolarApiException)
            {
                // Chat service may be unavailable; still show account.
            }

            var name = me.Nick ?? me.Name ?? "用户";
            Summary =
                $"欢迎回来，{name}\n" +
                $"等级 Lv{me.Profile?.Level ?? 0} · Perk {me.PerkLevel}\n" +
                $"未读会话：{unreadRooms}";
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            Summary = _authService.CurrentAccount is { } a
                ? $"离线缓存：{a.Nick ?? a.Name}"
                : "无法加载首页数据";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
