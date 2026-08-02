namespace SolarWin.Models.Analytics;

public sealed record HourlyActivityPoint(int Hour, long MessageCount)
{
    public string TooltipText => $"{Hour:00}:00–{Hour:00}:59，共 {MessageCount} 条消息";
}
