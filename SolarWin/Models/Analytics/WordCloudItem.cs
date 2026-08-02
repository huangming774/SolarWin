namespace SolarWin.Models.Analytics;

public sealed record WordCloudItem
{
    public string Word { get; init; } = string.Empty;
    public int Count { get; init; }
    public double FontSize { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Rotation { get; init; }
    public string ColorKey { get; init; } = "TextFillColorPrimaryBrush";
}
