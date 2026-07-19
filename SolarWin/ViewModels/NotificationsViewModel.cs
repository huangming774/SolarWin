using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolarWin.Services;

namespace SolarWin.ViewModels;

public partial class NotificationsViewModel : ObservableObject
{
    private readonly ISolarApiClient _api;
    private readonly IToastService _toast;
    private readonly MainViewModel _main;

    public NotificationsViewModel(ISolarApiClient api, IToastService toast, MainViewModel main)
    {
        _api = api;
        _toast = toast;
        _main = main;
    }

    public ObservableCollection<NotificationItemViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(ShowContent))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(ShowContent))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial int UnreadCount { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEmpty => !IsBusy && !HasError && Items.Count == 0;

    public bool ShowContent => !IsBusy && !HasError && Items.Count > 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            Items.Clear();
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ShowContent));

            var list = await _api.GetNotificationsAsync(offset: 0, take: 20).ConfigureAwait(true);
            foreach (var n in list.OrderByDescending(x => x.CreatedAt))
            {
                Items.Add(new NotificationItemViewModel(n));
            }

            UnreadCount = Items.Count(i => i.IsUnread);
            StatusText = $"共 {Items.Count} 条 · 未读 {UnreadCount}";
            await _main.RefreshNotificationBadgeAsync().ConfigureAwait(true);
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "加载失败";
            _toast.Error("通知加载失败");
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ShowContent));
            OnPropertyChanged(nameof(HasError));
        }
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await _api.MarkAllNotificationsReadAsync().ConfigureAwait(true);

            foreach (var item in Items)
            {
                item.MarkReadLocal();
            }

            UnreadCount = 0;
            StatusText = $"共 {Items.Count} 条 · 未读 0";
            await _main.RefreshNotificationBadgeAsync().ConfigureAwait(true);
            _toast.Success("已全部标为已读");
        }
        catch (SolarApiException ex)
        {
            ErrorMessage = ex.Message;
            _toast.Error("标记已读失败");
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(ShowContent));
            OnPropertyChanged(nameof(HasError));
        }
    }
}
