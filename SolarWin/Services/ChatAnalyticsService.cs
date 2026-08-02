using SolarWin.Models.Analytics;
using SolarWin.Repositories;

namespace SolarWin.Services;

public sealed class ChatAnalyticsService : IChatAnalyticsService
{
    public const int MaximumWordMessages = 100_000;
    private static readonly string[] DayLabels =
        ["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];

    private readonly IChatAnalyticsRepository _repository;
    private readonly ITextTokenizer _tokenizer;
    private readonly IWordCloudLayoutService _wordCloudLayout;

    public ChatAnalyticsService(
        IChatAnalyticsRepository repository,
        ITextTokenizer tokenizer,
        IWordCloudLayoutService wordCloudLayout)
    {
        _repository = repository;
        _tokenizer = tokenizer;
        _wordCloudLayout = wordCloudLayout;
    }

    public Task<IReadOnlyList<ConversationOption>> GetConversationsAsync(CancellationToken cancellationToken)
        => _repository.GetConversationsAsync(cancellationToken);

    public async Task<IReadOnlyList<ActivityHeatmapCell>> GetActivityHeatmapAsync(
        AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var raw = await _repository.GetActivityHeatmapAsync(filter, cancellationToken).ConfigureAwait(false);
        var lookup = raw.ToDictionary(static x => (x.DayOfWeekIndex, x.Hour));
        var max = raw.Count == 0 ? 0 : raw.Max(static x => x.MessageCount);
        var denominator = Math.Log(1 + max);
        var result = new List<ActivityHeatmapCell>(168);
        for (var day = 0; day < 7; day++)
        {
            for (var hour = 0; hour < 24; hour++)
            {
                var count = lookup.TryGetValue((day, hour), out var cell) ? cell.MessageCount : 0;
                result.Add(new ActivityHeatmapCell
                {
                    DayOfWeekIndex = day,
                    DayLabel = DayLabels[day],
                    Hour = hour,
                    MessageCount = count,
                    Intensity = denominator <= 0 ? 0 : Math.Log(1 + count) / denominator,
                });
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<MessageTrendPoint>> GetMessageTrendAsync(
        AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var raw = await _repository.GetMessageTrendAsync(filter, cancellationToken).ConfigureAwait(false);
        var lookup = raw.ToDictionary(static x => x.BucketStart.Date);
        var result = new List<MessageTrendPoint>();
        var current = FirstBucket(filter.StartTime.ToLocalTime(), filter.TrendGranularity);
        var end = filter.EndTime.ToLocalTime();
        while (current < end)
        {
            var key = current.Date;
            result.Add(lookup.TryGetValue(key, out var point)
                ? point
                : new MessageTrendPoint(current, FormatLabel(current, filter.TrendGranularity), 0));
            current = NextBucket(current, filter.TrendGranularity);
        }

        return result;
    }

    public async Task<IReadOnlyList<GroupContributionItem>> GetGroupContributionAsync(
        AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filter.ConversationId)) return [];
        var raw = await _repository.GetGroupContributionAsync(
            filter.ConversationId,
            filter.StartTime,
            filter.EndTime,
            Math.Clamp(filter.TopN, 1, 100),
            cancellationToken).ConfigureAwait(false);
        if (raw.Count == 0) return [];

        var top = raw.Where(static x => !x.IsOther).ToList();
        var otherCount = raw.Where(static x => x.IsOther).Sum(static x => x.MessageCount);
        if (otherCount > 0)
        {
            top.Add(new GroupContributionItem
            {
                SenderId = "other",
                DisplayName = "其他",
                MessageCount = otherCount,
                IsOther = true,
            });
        }

        var total = top.Sum(static x => x.MessageCount);
        return top.Select(x => x with
        {
            Percentage = total == 0 ? 0 : x.MessageCount * 100d / total,
        }).ToList();
    }

    public async Task<IReadOnlyList<HourlyActivityPoint>> GetHourlyActivityAsync(
        AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var raw = await _repository.GetHourlyActivityAsync(filter, cancellationToken).ConfigureAwait(false);
        var lookup = raw.ToDictionary(static x => x.Hour);
        return Enumerable.Range(0, 24)
            .Select(hour => lookup.TryGetValue(hour, out var point) ? point : new HourlyActivityPoint(hour, 0))
            .ToList();
    }

    public async Task<IReadOnlyList<WordFrequencyItem>> GetWordFrequenciesAsync(
        AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await foreach (var text in _repository.StreamTextMessagesAsync(filter, MaximumWordMessages, cancellationToken)
                           .ConfigureAwait(false))
        {
            foreach (var token in _tokenizer.Tokenize(text))
            {
                cancellationToken.ThrowIfCancellationRequested();
                counts[token] = counts.TryGetValue(token, out var count) ? count + 1 : 1;
            }
        }

        return counts
            .OrderByDescending(static x => x.Value)
            .ThenBy(static x => x.Key, StringComparer.Ordinal)
            .Take(Math.Clamp(filter.TopN, 1, 100))
            .Select(static x => new WordFrequencyItem(x.Key, x.Value, Math.Log(1 + x.Value)))
            .ToList();
    }

    public Task<IReadOnlyList<WordCloudItem>> LayoutWordCloudAsync(
        IReadOnlyList<WordFrequencyItem> words,
        double width,
        double height,
        CancellationToken cancellationToken)
        => Task.Run(() => _wordCloudLayout.Layout(words, width, height, cancellationToken), cancellationToken);

    public ChatAnalyticsSummary CalculateSummary(
        AnalyticsFilter filter,
        IReadOnlyList<ActivityHeatmapCell> heatmap,
        IReadOnlyList<HourlyActivityPoint> hourly)
    {
        var total = heatmap.Sum(static x => x.MessageCount);
        var days = Math.Max(1d, (filter.EndTime - filter.StartTime).TotalDays);
        var perDay = heatmap.GroupBy(static x => x.DayOfWeekIndex)
            .Select(static g => new { Day = g.Key, Count = g.Sum(static x => x.MessageCount) })
            .OrderByDescending(static x => x.Count)
            .ThenBy(static x => x.Day)
            .FirstOrDefault();
        var peakHour = hourly.OrderByDescending(static x => x.MessageCount).ThenBy(static x => x.Hour).FirstOrDefault();
        var peakStart = CalculatePeakPeriodStart(hourly);

        return new ChatAnalyticsSummary
        {
            TotalMessages = total,
            AveragePerDay = total / days,
            MostActiveDay = perDay is { Count: > 0 } ? DayLabels[perDay.Day] : "—",
            PeakHour = peakHour is { MessageCount: > 0 } ? $"{peakHour.Hour:00}:00" : "—",
            PeakPeriod = total > 0 ? $"{peakStart:00}:00–{(peakStart + 2) % 24:00}:59" : "—",
            AveragePerHour = total / 24d,
        };
    }

    public static int ConvertDayOfWeekToMondayIndex(DayOfWeek day)
        => ((int)day + 6) % 7;

    public static int CalculatePeakPeriodStart(IReadOnlyList<HourlyActivityPoint> points)
    {
        var counts = new long[24];
        foreach (var point in points)
        {
            if (point.Hour is >= 0 and < 24) counts[point.Hour] = point.MessageCount;
        }

        var bestHour = 0;
        var best = long.MinValue;
        for (var hour = 0; hour < 24; hour++)
        {
            var sum = counts[hour] + counts[(hour + 1) % 24] + counts[(hour + 2) % 24];
            if (sum > best)
            {
                best = sum;
                bestHour = hour;
            }
        }

        return bestHour;
    }

    private static DateTimeOffset FirstBucket(DateTimeOffset value, TrendGranularity granularity)
    {
        var date = value.LocalDateTime.Date;
        if (granularity == TrendGranularity.Month) return AtLocalMidnight(new DateTime(value.Year, value.Month, 1));
        if (granularity == TrendGranularity.Week)
        {
            date = date.AddDays(-ConvertDayOfWeekToMondayIndex(date.DayOfWeek));
        }

        return AtLocalMidnight(date);
    }

    private static DateTimeOffset NextBucket(DateTimeOffset value, TrendGranularity granularity)
        => granularity switch
        {
            TrendGranularity.Week => AtLocalMidnight(value.LocalDateTime.Date.AddDays(7)),
            TrendGranularity.Month => AtLocalMidnight(value.LocalDateTime.Date.AddMonths(1)),
            _ => AtLocalMidnight(value.LocalDateTime.Date.AddDays(1)),
        };

    private static DateTimeOffset AtLocalMidnight(DateTime date)
        => new(DateTime.SpecifyKind(date.Date, DateTimeKind.Local));

    private static string FormatLabel(DateTimeOffset value, TrendGranularity granularity)
        => granularity switch
        {
            TrendGranularity.Month => value.ToString("yyyy年MM月"),
            TrendGranularity.Week => value.ToString("MM-dd") + " 周",
            _ => value.ToString("MM-dd"),
        };
}
