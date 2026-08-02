namespace SolarWin.Models.Analytics;

public sealed record MessageTrendPoint(
    DateTimeOffset BucketStart,
    string Label,
    long MessageCount);
