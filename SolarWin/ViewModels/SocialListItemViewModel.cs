using CommunityToolkit.Mvvm.ComponentModel;

namespace SolarWin.ViewModels;

/// <summary>Generic row for home social lists (friends, realms, tickets, etc.).</summary>
public partial class SocialListItemViewModel : ObservableObject
{
    public SocialListItemViewModel(
        string id,
        string title,
        string subtitle = "",
        string meta = "",
        object? payload = null)
    {
        Id = id;
        Title = title;
        Subtitle = subtitle;
        Meta = meta;
        Payload = payload;
    }

    public string Id { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string Meta { get; }

    public object? Payload { get; }

    /// <summary>Optional secondary id (e.g. account Guid) for actions.</summary>
    public Guid? AccountId { get; init; }

    public string? Slug { get; init; }
}
