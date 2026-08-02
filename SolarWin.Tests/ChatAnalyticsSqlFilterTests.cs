using SolarWin.Models.Analytics;
using SolarWin.Repositories;

namespace SolarWin.Tests;

public sealed class ChatAnalyticsSqlFilterTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AllConversations_OmitsOptionalPredicatesAndParameters()
    {
        var sql = ChatAnalyticsRepository.BuildWhereClause(CreateFilter());

        Assert.DoesNotContain("@conversationId", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@currentUserId", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@onlyCurrentUser", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("@normalStatus", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedConversation_UsesFixedParameterizedEquality()
    {
        var id = Guid.NewGuid().ToString("D");
        var sql = ChatAnalyticsRepository.BuildWhereClause(CreateFilter() with { ConversationId = id });

        Assert.Contains("m.RoomId = @conversationId", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(id, sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@conversationId IS NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyCurrentUser_UsesFixedParameterizedJsonPredicate()
    {
        var id = Guid.NewGuid().ToString("D");
        var sql = ChatAnalyticsRepository.BuildWhereClause(CreateFilter() with
        {
            CurrentUserId = id,
            OnlyCurrentUser = true,
        });

        Assert.Contains("@currentUserId", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(id, sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@onlyCurrentUser", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidConversationValue_IsNeverEmbeddedInSql()
    {
        const string hostile = "x' OR 1=1 --";
        var sql = ChatAnalyticsRepository.BuildWhereClause(CreateFilter() with { ConversationId = hostile });

        Assert.DoesNotContain(hostile, sql, StringComparison.Ordinal);
        Assert.DoesNotContain("m.RoomId =", sql, StringComparison.Ordinal);
    }

    private static AnalyticsFilter CreateFilter() => new()
    {
        StartTime = Start,
        EndTime = Start.AddDays(1),
    };
}
