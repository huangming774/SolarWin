namespace SolarWin.Models.Analytics;

public enum TrendGranularity
{
    Day,
    Week,
    Month,
}

public sealed record AnalyticsFilter
{
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset EndTime { get; init; }
    public string? ConversationId { get; init; }
    public string? CurrentUserId { get; init; }
    public bool OnlyCurrentUser { get; init; }
    public int TopN { get; init; } = 50;
    public TrendGranularity TrendGranularity { get; init; } = TrendGranularity.Day;
}

public sealed record ConversationOption(
    string? Id,
    string DisplayName,
    int RoomType,
    bool IsAll = false)
{
    public bool IsGroup => !IsAll && RoomType == 1;
}
