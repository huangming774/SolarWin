using System.Globalization;
using Microsoft.Data.Sqlite;
using SolarWin.Data;
using SolarWin.Models.Analytics;
using SolarWin.Repositories;
using SolarWin.Services;

namespace SolarWin.Tests;

public sealed class ChatAnalyticsServiceTests
{
    [Fact]
    public void SundayMapsToIndexSix()
        => Assert.Equal(6, ChatAnalyticsService.ConvertDayOfWeekToMondayIndex(DayOfWeek.Sunday));

    [Fact]
    public async Task HeatmapAlwaysContains168Cells()
    {
        var service = CreateService(new FakeRepository());
        var cells = await service.GetActivityHeatmapAsync(Filter(), CancellationToken.None);
        Assert.Equal(168, cells.Count);
        Assert.Equal(Enumerable.Range(0, 24), cells.Take(24).Select(static x => x.Hour));
    }

    [Fact]
    public async Task TrendFillsMissingDays()
    {
        var offset = TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 1, 1));
        var filter = Filter(new DateTimeOffset(2026, 1, 1, 0, 0, 0, offset), new DateTimeOffset(2026, 1, 4, 0, 0, 0, offset));
        var repo = new FakeRepository
        {
            Trend =
            [
                new MessageTrendPoint(filter.StartTime.ToLocalTime(), "01-01", 2),
                new MessageTrendPoint(filter.StartTime.ToLocalTime().AddDays(2), "01-03", 4),
            ],
        };
        var points = await CreateService(repo).GetMessageTrendAsync(filter, CancellationToken.None);
        Assert.Equal(3, points.Count);
        Assert.Equal([2L, 0L, 4L], points.Select(static x => x.MessageCount));
    }

    [Fact]
    public async Task WeeklyTrendStartsOnMonday()
    {
        var start = new DateTimeOffset(2026, 1, 7, 0, 0, 0, TimeSpan.Zero); // Wednesday
        var points = await CreateService(new FakeRepository()).GetMessageTrendAsync(
            Filter(start, start.AddDays(10)) with { TrendGranularity = TrendGranularity.Week },
            CancellationToken.None);
        Assert.NotEmpty(points);
        Assert.Equal(DayOfWeek.Monday, points[0].BucketStart.DayOfWeek);
    }

    [Fact]
    public async Task MonthlyTrendCrossesYearCorrectly()
    {
        var start = new DateTimeOffset(2025, 12, 15, 0, 0, 0, TimeSpan.Zero);
        var points = await CreateService(new FakeRepository()).GetMessageTrendAsync(
            Filter(start, new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero)) with { TrendGranularity = TrendGranularity.Month },
            CancellationToken.None);
        Assert.Equal([(2025, 12), (2026, 1), (2026, 2)], points.Select(static x => (x.BucketStart.Year, x.BucketStart.Month)));
    }

    [Fact]
    public async Task HourlyActivityAlwaysContains24Points()
    {
        var repo = new FakeRepository { Hourly = [new HourlyActivityPoint(5, 9)] };
        var points = await CreateService(repo).GetHourlyActivityAsync(Filter(), CancellationToken.None);
        Assert.Equal(24, points.Count);
        Assert.Equal(9, points[5].MessageCount);
    }

    [Fact]
    public void PeakPeriodWrapsAcrossMidnight()
    {
        var points = Enumerable.Range(0, 24).Select(static h => new HourlyActivityPoint(h, h is 23 or 0 or 1 ? 100 : 1)).ToList();
        Assert.Equal(23, ChatAnalyticsService.CalculatePeakPeriodStart(points));
    }

    [Fact]
    public async Task ContributorsBeyondTopTenMergeIntoOther()
    {
        var raw = Enumerable.Range(0, 12).Select(i => new GroupContributionItem
        {
            SenderId = i.ToString(CultureInfo.InvariantCulture),
            DisplayName = "成员" + i,
            MessageCount = 12 - i,
            IsOther = i >= 10,
        }).ToList();
        var repo = new FakeRepository { Contributions = raw };
        var result = await CreateService(repo).GetGroupContributionAsync(
            Filter() with { ConversationId = Guid.NewGuid().ToString("D"), TopN = 10 },
            CancellationToken.None);
        Assert.Equal(11, result.Count);
        Assert.Equal(3, result.Single(static x => x.IsOther).MessageCount);
    }

    [Fact]
    public async Task ContributionPercentagesSumToApproximately100()
    {
        var repo = new FakeRepository
        {
            Contributions =
            [
                new GroupContributionItem { SenderId = "a", DisplayName = "A", MessageCount = 2 },
                new GroupContributionItem { SenderId = "b", DisplayName = "B", MessageCount = 3 },
            ],
        };
        var result = await CreateService(repo).GetGroupContributionAsync(
            Filter() with { ConversationId = Guid.NewGuid().ToString("D") }, CancellationToken.None);
        Assert.InRange(result.Sum(static x => x.Percentage), 99.999, 100.001);
    }

    [Fact]
    public void TokenizerFiltersStopwordsUrlsNumbersAndPunctuation()
    {
        var tokens = new LocalTextTokenizer().Tokenize("我是测试 https://example.com 12345 ！！！ 本地分析").ToList();
        Assert.DoesNotContain("我", tokens);
        Assert.DoesNotContain(tokens, static x => x.Contains("http", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("12345", tokens);
        Assert.DoesNotContain("！！！", tokens);
        Assert.Contains(tokens, static x => x.Contains("测试", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EnglishTokensMergeIgnoringCase()
    {
        var repo = new FakeRepository { TextMessages = ["Hello hello HELLO"] };
        var words = await CreateService(repo, new LocalTextTokenizer()).GetWordFrequenciesAsync(Filter(), CancellationToken.None);
        var hello = Assert.Single(words, static x => x.Word == "hello");
        Assert.Equal(3, hello.Count);
    }

    [Fact]
    public async Task EmptyDatabaseReturnsEmptyAggregatesWithoutCrashing()
    {
        await using var db = await TempAnalyticsDatabase.CreateAsync();
        var repo = new ChatAnalyticsRepository(db);
        Assert.Empty(await repo.GetActivityHeatmapAsync(Filter(), CancellationToken.None));
        Assert.Empty(await repo.GetMessageTrendAsync(Filter(), CancellationToken.None));
        Assert.Empty(await repo.GetHourlyActivityAsync(Filter(), CancellationToken.None));
    }

    [Fact]
    public async Task DeletedAndSystemMessagesAreExcluded()
    {
        await using var db = await TempAnalyticsDatabase.CreateAsync();
        var room = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await db.InsertMessageAsync(Guid.NewGuid(), room, "text", start.AddHours(1), null, "正常");
        await db.InsertMessageAsync(Guid.NewGuid(), room, "text", start.AddHours(2), start.AddDays(1), "已删除");
        await db.InsertMessageAsync(Guid.NewGuid(), room, "system.member.joined", start.AddHours(3), null, null);
        var deletedTarget = Guid.NewGuid();
        await db.InsertMessageAsync(deletedTarget, room, "text", start.AddHours(4), null, "待删除");
        await db.InsertMessageAsync(Guid.NewGuid(), room, "messages.delete", start.AddHours(5), null, null, deletedTarget);

        var rows = await new ChatAnalyticsRepository(db).GetHourlyActivityAsync(Filter(start, start.AddDays(1)), CancellationToken.None);
        Assert.Equal(1, rows.Sum(static x => x.MessageCount));
    }

    [Fact]
    public async Task CancellationTokenCancelsRepositoryQuery()
    {
        await using var db = await TempAnalyticsDatabase.CreateAsync();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ChatAnalyticsRepository(db).GetActivityHeatmapAsync(Filter(), cts.Token));
    }

    [Fact]
    public async Task TimeRangeIsLeftClosedRightOpen()
    {
        await using var db = await TempAnalyticsDatabase.CreateAsync();
        var room = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(1);
        await db.InsertMessageAsync(Guid.NewGuid(), room, "text", start, null, "开始");
        await db.InsertMessageAsync(Guid.NewGuid(), room, "text", end.AddTicks(-10), null, "结束前");
        await db.InsertMessageAsync(Guid.NewGuid(), room, "text", end, null, "结束边界");
        var rows = await new ChatAnalyticsRepository(db).GetHourlyActivityAsync(Filter(start, end), CancellationToken.None);
        Assert.Equal(2, rows.Sum(static x => x.MessageCount));
    }

    [Fact]
    public async Task SqlInjectionConversationTextCannotChangeQueryStructure()
    {
        await using var db = await TempAnalyticsDatabase.CreateAsync();
        var repo = new ChatAnalyticsRepository(db);
        await Assert.ThrowsAsync<ArgumentException>(() => repo.GetActivityHeatmapAsync(
            Filter() with { ConversationId = "' OR 1=1 --" }, CancellationToken.None));
    }

    [Fact]
    public async Task OnlyCurrentUserUsesPayloadAccountIdInsteadOfMemberId()
    {
        await using var db = await TempAnalyticsDatabase.CreateAsync();
        var room = Guid.NewGuid();
        var currentAccount = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await db.InsertMessageAsync(Guid.NewGuid(), room, "text", start.AddHours(1), null, "我的", senderAccountId: currentAccount);
        await db.InsertMessageAsync(Guid.NewGuid(), room, "text", start.AddHours(2), null, "他人", senderAccountId: Guid.NewGuid());
        var rows = await new ChatAnalyticsRepository(db).GetHourlyActivityAsync(
            Filter(start, start.AddDays(1)) with
            {
                OnlyCurrentUser = true,
                CurrentUserId = currentAccount.ToString("D"),
            }, CancellationToken.None);
        Assert.Equal(1, rows.Sum(static x => x.MessageCount));
    }

    private static AnalyticsFilter Filter(DateTimeOffset? start = null, DateTimeOffset? end = null)
        => new()
        {
            StartTime = start ?? DateTimeOffset.UtcNow.AddDays(-30),
            EndTime = end ?? DateTimeOffset.UtcNow,
            TopN = 50,
        };

    private static ChatAnalyticsService CreateService(FakeRepository repository, ITextTokenizer? tokenizer = null)
        => new(repository, tokenizer ?? new FakeTokenizer(), new FakeWordCloudLayout());

    private sealed class FakeRepository : IChatAnalyticsRepository
    {
        public IReadOnlyList<MessageTrendPoint> Trend { get; init; } = [];
        public IReadOnlyList<HourlyActivityPoint> Hourly { get; init; } = [];
        public IReadOnlyList<GroupContributionItem> Contributions { get; init; } = [];
        public IReadOnlyList<string> TextMessages { get; init; } = [];
        public Task<IReadOnlyList<ConversationOption>> GetConversationsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ConversationOption>>([]);
        public Task<IReadOnlyList<ActivityHeatmapCell>> GetActivityHeatmapAsync(AnalyticsFilter filter, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ActivityHeatmapCell>>([]);
        public Task<IReadOnlyList<MessageTrendPoint>> GetMessageTrendAsync(AnalyticsFilter filter, CancellationToken cancellationToken) => Task.FromResult(Trend);
        public Task<IReadOnlyList<GroupContributionItem>> GetGroupContributionAsync(string groupConversationId, DateTimeOffset startTime, DateTimeOffset endTime, int topN, CancellationToken cancellationToken) => Task.FromResult(Contributions);
        public Task<IReadOnlyList<HourlyActivityPoint>> GetHourlyActivityAsync(AnalyticsFilter filter, CancellationToken cancellationToken) => Task.FromResult(Hourly);
        public async IAsyncEnumerable<string> StreamTextMessagesAsync(AnalyticsFilter filter, int maximumMessageCount, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var text in TextMessages) { cancellationToken.ThrowIfCancellationRequested(); yield return text; await Task.Yield(); }
        }
    }

    private sealed class FakeTokenizer : ITextTokenizer
    {
        public IEnumerable<string> Tokenize(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed class FakeWordCloudLayout : IWordCloudLayoutService
    {
        public IReadOnlyList<WordCloudItem> Layout(
            IReadOnlyList<WordFrequencyItem> words,
            double width,
            double height,
            CancellationToken cancellationToken = default) => [];
    }

    private sealed class TempAnalyticsDatabase : IDbConnectionFactory, IAsyncDisposable
    {
        private readonly string _path;
        private TempAnalyticsDatabase(string path) => _path = path;

        public static async Task<TempAnalyticsDatabase> CreateAsync()
        {
            SQLitePCL.Batteries_V2.Init();
            var path = Path.Combine(Path.GetTempPath(), "solarwin-analytics-" + Guid.NewGuid().ToString("N") + ".db");
            var db = new TempAnalyticsDatabase(path);
            await using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE chat_messages (
                    RowId TEXT PRIMARY KEY, MessageId TEXT NOT NULL, RoomId TEXT NOT NULL,
                    RoomSequence INTEGER NOT NULL, CreatedAt TEXT NULL, UpdatedAt TEXT NULL,
                    DeletedAt TEXT NULL, Type TEXT NULL, Content TEXT NULL, SenderId TEXT NOT NULL,
                    ClientMessageId TEXT NULL, IsEncrypted INTEGER NOT NULL, PayloadJson TEXT NULL,
                    SyncedAt TEXT NOT NULL, Source INTEGER NOT NULL);
                CREATE TABLE chat_rooms (
                    RoomId TEXT PRIMARY KEY, Type INTEGER NOT NULL, Pinned INTEGER NOT NULL,
                    LastActivity TEXT NOT NULL, Name TEXT NULL, PayloadJson TEXT NULL, DeletedAt TEXT NULL);
                """;
            await command.ExecuteNonQueryAsync();
            return db;
        }

        public async Task<SqliteConnection> OpenReadOnlyConnectionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var connection = new SqliteConnection($"Data Source={_path};Mode=ReadOnly");
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        public async Task InsertMessageAsync(
            Guid messageId,
            Guid roomId,
            string type,
            DateTimeOffset createdAt,
            DateTimeOffset? deletedAt,
            string? content,
            Guid? deletedTarget = null,
            Guid? senderAccountId = null)
        {
            await using var connection = new SqliteConnection($"Data Source={_path}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO chat_messages
                    (RowId, MessageId, RoomId, RoomSequence, CreatedAt, DeletedAt, Type, Content, SenderId, IsEncrypted, PayloadJson, SyncedAt, Source)
                VALUES
                    (@row, @message, @room, 1, @created, @deleted, @type, @content, @sender, 0, @payload, @synced, 0);
                """;
            command.Parameters.AddWithValue("@row", Guid.NewGuid().ToString("D"));
            command.Parameters.AddWithValue("@message", messageId.ToString("D"));
            command.Parameters.AddWithValue("@room", roomId.ToString("D"));
            command.Parameters.AddWithValue("@created", Format(createdAt));
            command.Parameters.AddWithValue("@deleted", deletedAt is null ? DBNull.Value : Format(deletedAt.Value));
            command.Parameters.AddWithValue("@type", type);
            command.Parameters.AddWithValue("@content", content is null ? DBNull.Value : content);
            command.Parameters.AddWithValue("@sender", Guid.NewGuid().ToString("D"));
            var payload = new Dictionary<string, object>();
            if (deletedTarget is { } target)
            {
                payload["meta"] = new { message_id = target.ToString("D") };
            }
            if (senderAccountId is { } account)
            {
                payload["sender"] = new { account_id = account.ToString("D") };
            }
            command.Parameters.AddWithValue("@payload", System.Text.Json.JsonSerializer.Serialize(payload));
            command.Parameters.AddWithValue("@synced", Format(DateTimeOffset.UtcNow));
            await command.ExecuteNonQueryAsync();
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            try { File.Delete(_path); } catch { }
            return ValueTask.CompletedTask;
        }

        private static string Format(DateTimeOffset value)
            => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture);
    }
}
