using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SolarWin.Helpers;
using SolarWin.Services;
using Windows.ApplicationModel;

namespace SolarWin.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IToastService _toast;

    public SettingsViewModel(IAuthService authService, IToastService toast)
    {
        _authService = authService;
        _toast = toast;
        VersionText = ResolveVersion();
        SelectedThemeIndex = ThemeHelper.GetSavedTheme() switch
        {
            ElementTheme.Light => 1,
            ElementTheme.Dark => 2,
            _ => 0,
        };
        ThemeLabel = ThemeHelper.ToDisplayName(ThemeHelper.GetSavedTheme());
    }

    [ObservableProperty]
    public partial string VersionText { get; set; } = "1.0.0";

    [ObservableProperty]
    public partial string ThemeLabel { get; set; } = "跟随系统";

    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    public event EventHandler? LoggedOut;

    [RelayCommand]
    private void ApplyTheme()
    {
        var theme = SelectedThemeIndex switch
        {
            1 => ElementTheme.Light,
            2 => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        ThemeHelper.SaveTheme(theme);
        ThemeHelper.ApplyToWindow(App.Window, theme);
        ThemeLabel = ThemeHelper.ToDisplayName(theme);
        StatusMessage = $"主题已切换为：{ThemeLabel}";
        _toast.Success(StatusMessage);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _authService.LogoutAsync().ConfigureAwait(true);
        StatusMessage = "已退出登录";
        _toast.Success("已退出登录");
        LoggedOut?.Invoke(this, EventArgs.Empty);
    }

    private static string ResolveVersion()
    {
        try
        {
            var v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
        catch
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        }
    }
}
