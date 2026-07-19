using CommunityToolkit.Mvvm.ComponentModel;
using SolarWin.Models;

namespace SolarWin.ViewModels;

public partial class NotificationItemViewModel : ObservableObject
{
    public NotificationItemViewModel(SnNotification notification)
    {
        Notification = notification;
        Id = notification.Id;
        Title = string.IsNullOrWhiteSpace(notification.Title) ? "通知" : notification.Title!;
        Subtitle = notification.Subtitle ?? string.Empty;
        Content = notification.Content ?? string.Empty;
        Topic = notification.Topic ?? string.Empty;
        TimeText = FormatTime(notification.CreatedAt);
        IsUnread = notification.ViewedAt is null;
        UnreadOpacity = IsUnread ? 1.0 : 0.0;
        TitleWeightName = IsUnread ? "SemiBold" : "Normal";
        CardOpacity = IsUnread ? 1.0 : 0.85;
    }

    public SnNotification Notification { get; }

    public Guid Id { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string Content { get; }

    public string Topic { get; }

    public string TimeText { get; }

    public bool IsUnread { get; private set; }

    public double UnreadOpacity { get; private set; }

    public double CardOpacity { get; private set; }

    /// <summary>Font weight name for XAML FontWeight conversion via resource or code-behind free string.</summary>
    public string TitleWeightName { get; private set; }

    public string UnreadLabel => IsUnread ? "未读" : "已读";

    public void MarkReadLocal()
    {
        IsUnread = false;
        UnreadOpacity = 0.0;
        CardOpacity = 0.85;
        TitleWeightName = "Normal";
        OnPropertyChanged(nameof(IsUnread));
        OnPropertyChanged(nameof(UnreadOpacity));
        OnPropertyChanged(nameof(CardOpacity));
        OnPropertyChanged(nameof(TitleWeightName));
        OnPropertyChanged(nameof(UnreadLabel));
    }

    private static string FormatTime(DateTimeOffset? time)
    {
        if (time is null)
        {
            return string.Empty;
        }

        var local = time.Value.ToLocalTime();
        var now = DateTimeOffset.Now;
        if (local.Date == now.Date)
        {
            return local.ToString("HH:mm");
        }

        if (local.Year == now.Year)
        {
            return local.ToString("MM-dd HH:mm");
        }

        return local.ToString("yyyy-MM-dd HH:mm");
    }
}
