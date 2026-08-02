namespace SolarWin.Models.Analytics;

public sealed record ChatAnalyticsSummary
{
    public long TotalMessages { get; init; }
    public double AveragePerDay { get; init; }
    public string MostActiveDay { get; init; } = "—";
    public string PeakHour { get; init; } = "—";
    public string PeakPeriod { get; init; } = "—";
    public double AveragePerHour { get; init; }
}
