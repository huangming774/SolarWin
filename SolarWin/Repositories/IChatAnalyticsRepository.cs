using SolarWin.Models.Analytics;

namespace SolarWin.Repositories;

public interface IChatAnalyticsRepository
{
    Task<IReadOnlyList<ConversationOption>> GetConversationsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ActivityHeatmapCell>> GetActivityHeatmapAsync(
        AnalyticsFilter filter,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MessageTrendPoint>> GetMessageTrendAsync(
        AnalyticsFilter filter,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GroupContributionItem>> GetGroupContributionAsync(
        string groupConversationId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int topN,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HourlyActivityPoint>> GetHourlyActivityAsync(
        AnalyticsFilter filter,
        CancellationToken cancellationToken);

    IAsyncEnumerable<string> StreamTextMessagesAsync(
        AnalyticsFilter filter,
        int maximumMessageCount,
        CancellationToken cancellationToken);
}
