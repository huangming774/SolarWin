using SolarWin.Models.Analytics;

namespace SolarWin.Services;

public interface IChatAnalyticsService
{
    Task<IReadOnlyList<ConversationOption>> GetConversationsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ActivityHeatmapCell>> GetActivityHeatmapAsync(AnalyticsFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<MessageTrendPoint>> GetMessageTrendAsync(AnalyticsFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<GroupContributionItem>> GetGroupContributionAsync(AnalyticsFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<HourlyActivityPoint>> GetHourlyActivityAsync(AnalyticsFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<WordFrequencyItem>> GetWordFrequenciesAsync(AnalyticsFilter filter, CancellationToken cancellationToken);
    Task<IReadOnlyList<WordCloudItem>> LayoutWordCloudAsync(IReadOnlyList<WordFrequencyItem> words, double width, double height, CancellationToken cancellationToken);
    ChatAnalyticsSummary CalculateSummary(AnalyticsFilter filter, IReadOnlyList<ActivityHeatmapCell> heatmap, IReadOnlyList<HourlyActivityPoint> hourly);
}
