namespace SolarWin.Models.Analytics;

public sealed record GroupContributionItem
{
    public string SenderId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public long MessageCount { get; init; }
    public double Percentage { get; init; }
    public bool IsOther { get; init; }
    public string PercentageText => $"{Percentage:0.#}%";
}
