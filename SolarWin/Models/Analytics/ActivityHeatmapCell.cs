namespace SolarWin.Models.Analytics;

public sealed record ActivityHeatmapCell
{
    public int DayOfWeekIndex { get; init; }
    public string DayLabel { get; init; } = string.Empty;
    public int Hour { get; init; }
    public long MessageCount { get; init; }
    public double Intensity { get; init; }

    public string TooltipText => $"{DayLabel} {Hour:00}:00–{Hour:00}:59，共 {MessageCount} 条消息";
}
