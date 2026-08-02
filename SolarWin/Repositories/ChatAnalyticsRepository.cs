using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using SolarWin.Data;
using SolarWin.Models.Analytics;

namespace SolarWin.Repositories;

public sealed class ChatAnalyticsRepository : IChatAnalyticsRepository
{
    private const string TextMessageType = "text";
    private readonly IDbConnectionFactory _connections;

    public ChatAnalyticsRepository(IDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<ConversationOption>> GetConversationsAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT RoomId,
                   Type,
                   COALESCE(
                       NULLIF(Name, ''),
                       NULLIF(json_extract(PayloadJson, '$.account.nick'), ''),
                       NULLIF(json_extract(PayloadJson, '$.account.name'), ''),
                       CASE WHEN Type = 1 THEN '未命名群聊' ELSE '私聊' END)
            FROM chat_rooms
            WHERE DeletedAt IS NULL
            ORDER BY Pinned DESC, LastActivity DESC;
            """;

        return await ExecuteAsync(async connection =>
        {
            var result = new List<ConversationOption>();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                result.Add(new ConversationOption(
                    reader.GetString(0),
                    reader.GetString(2),
                    reader.GetInt32(1)));
            }

            return (IReadOnlyList<ConversationOption>)result;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ActivityHeatmapCell>> GetActivityHeatmapAsync(
        AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        ValidateFilter(filter);
        var whereClause = BuildWhereClause(filter);
        var sql = $"""
            SELECT (CAST(strftime('%w', datetime(m.CreatedAt), 'localtime') AS INTEGER) + 6) % 7 AS DayIndex,
                   CAST(strftime('%H', datetime(m.CreatedAt), 'localtime') AS INTEGER) AS HourValue,
                   COUNT(*) AS MessageCount
            FROM chat_messages AS m
            WHERE {whereClause}
            GROUP BY DayIndex, HourValue
            ORDER BY DayIndex, HourValue;
            """;

        return await ExecuteAsync(async connection =>
        {
            var result = new List<ActivityHeatmapCell>();
            await using var command = CreateFilteredCommand(connection, sql, filter);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var day = reader.GetInt32(0);
                result.Add(new ActivityHeatmapCell
                {
                    DayOfWeekIndex = day,
                    DayLabel = DayLabels[day],
                    Hour = reader.GetInt32(1),
                    MessageCount = reader.GetInt64(2),
                });
            }

            return (IReadOnlyList<ActivityHeatmapCell>)result;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MessageTrendPoint>> GetMessageTrendAsync(
        AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        ValidateFilter(filter);
        var whereClause = BuildWhereClause(filter);
        var bucketExpression = filter.TrendGranularity switch
        {
            TrendGranularity.Week =>
                "date(datetime(m.CreatedAt), 'localtime', '-' || ((CAST(strftime('%w', datetime(m.CreatedAt), 'localtime') AS INTEGER) + 6) % 7) || ' days')",
            TrendGranularity.Month => "strftime('%Y-%m-01', datetime(m.CreatedAt), 'localtime')",
            _ => "strftime('%Y-%m-%d', datetime(m.CreatedAt), 'localtime')",
        };
        var sql = $"""
            SELECT {bucketExpression} AS BucketStart, COUNT(*) AS MessageCount
            FROM chat_messages AS m
            WHERE {whereClause}
            GROUP BY BucketStart
            ORDER BY BucketStart;
            """;

        return await ExecuteAsync(async connection =>
        {
            var result = new List<MessageTrendPoint>();
            await using var command = CreateFilteredCommand(connection, sql, filter);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var text = reader.GetString(0);
                var localDate = DateTime.SpecifyKind(
                    DateTime.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    DateTimeKind.Local);
                var date = new DateTimeOffset(localDate);
                result.Add(new MessageTrendPoint(date, FormatBucketLabel(date, filter.TrendGranularity), reader.GetInt64(1)));
            }

            return (IReadOnlyList<MessageTrendPoint>)result;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GroupContributionItem>> GetGroupContributionAsync(
        string groupConversationId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        int topN,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(groupConversationId, out var roomId) || roomId == Guid.Empty)
        {
            throw new ArgumentException("群聊会话 ID 无效。", nameof(groupConversationId));
        }

        if (endTime <= startTime) throw new ArgumentException("统计结束时间必须晚于开始时间。");
        topN = Math.Clamp(topN, 1, 100);

        var filter = new AnalyticsFilter
        {
            StartTime = startTime,
            EndTime = endTime,
            ConversationId = roomId.ToString("D"),
            TopN = topN,
        };
        var whereClause = BuildWhereClause(filter);
        var sql = $"""
            WITH sender_totals AS (
                SELECT m.SenderId,
                       MAX(COALESCE(
                           NULLIF(json_extract(m.PayloadJson, '$.sender.nick'), ''),
                           NULLIF(json_extract(m.PayloadJson, '$.sender.username'), ''),
                           NULLIF(json_extract(m.PayloadJson, '$.sender.account.nick'), ''),
                           NULLIF(json_extract(m.PayloadJson, '$.sender.account.name'), ''),
                           '')) AS DisplayName,
                       COUNT(*) AS MessageCount
                FROM chat_messages AS m
                WHERE {whereClause}
                  AND EXISTS (
                      SELECT 1 FROM chat_rooms AS room
                      WHERE room.RoomId = m.RoomId AND room.Type = 1 AND room.DeletedAt IS NULL)
                GROUP BY m.SenderId
            )
            SELECT SenderId, DisplayName, MessageCount,
                   CASE WHEN ROW_NUMBER() OVER (ORDER BY MessageCount DESC, SenderId) > @topN THEN 1 ELSE 0 END
            FROM sender_totals
            ORDER BY MessageCount DESC, SenderId;
            """;

        return await ExecuteAsync(async connection =>
        {
            var result = new List<GroupContributionItem>();
            await using var command = CreateFilteredCommand(connection, sql, filter);
            command.Parameters.Add(new SqliteParameter("@topN", SqliteType.Integer) { Value = topN });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var senderId = reader.GetString(0);
                var displayName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                result.Add(new GroupContributionItem
                {
                    SenderId = senderId,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? MaskId(senderId) : displayName,
                    MessageCount = reader.GetInt64(2),
                    IsOther = reader.GetInt32(3) != 0,
                });
            }

            return (IReadOnlyList<GroupContributionItem>)result;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<HourlyActivityPoint>> GetHourlyActivityAsync(
        AnalyticsFilter filter,
        CancellationToken cancellationToken)
    {
        ValidateFilter(filter);
        var whereClause = BuildWhereClause(filter);
        var sql = $"""
            SELECT CAST(strftime('%H', datetime(m.CreatedAt), 'localtime') AS INTEGER) AS HourValue,
                   COUNT(*) AS MessageCount
            FROM chat_messages AS m
            WHERE {whereClause}
            GROUP BY HourValue
            ORDER BY HourValue;
            """;

        return await ExecuteAsync(async connection =>
        {
            var result = new List<HourlyActivityPoint>();
            await using var command = CreateFilteredCommand(connection, sql, filter);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                result.Add(new HourlyActivityPoint(reader.GetInt32(0), reader.GetInt64(1)));
            }

            return (IReadOnlyList<HourlyActivityPoint>)result;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<string> StreamTextMessagesAsync(
        AnalyticsFilter filter,
        int maximumMessageCount,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ValidateFilter(filter);
        maximumMessageCount = Math.Clamp(maximumMessageCount, 1, 100_000);
        var whereClause = BuildWhereClause(filter);
        var sql = $"""
            SELECT m.Content
            FROM chat_messages AS m
            WHERE {whereClause}
              AND m.IsEncrypted = 0
              AND m.Content IS NOT NULL
              AND trim(m.Content) != ''
            ORDER BY m.CreatedAt DESC
            LIMIT @maximumMessageCount;
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = CreateFilteredCommand(connection, sql, filter);
        command.Parameters.Add(new SqliteParameter("@maximumMessageCount", SqliteType.Integer)
        {
            Value = maximumMessageCount,
        });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return reader.GetString(0);
        }
    }

    internal static string BuildWhereClause(AnalyticsFilter filter)
    {
        var clauses = new List<string>
        {
            "m.Type = @textMessageType",
            "m.DeletedAt IS NULL",
            "m.CreatedAt IS NOT NULL",
            "m.CreatedAt >= @startTime",
            "m.CreatedAt < @endTime",
        };

        // Only fixed, program-owned SQL fragments are selected here. Values remain parameters.
        if (NormalizeGuid(filter.ConversationId) is not null)
        {
            clauses.Add("m.RoomId = @conversationId");
        }

        if (filter.OnlyCurrentUser)
        {
            clauses.Add("lower(COALESCE(json_extract(m.PayloadJson, '$.sender.account_id'), '')) = @currentUserId");
        }

        clauses.Add("""
            NOT EXISTS (
                SELECT 1
                FROM chat_messages AS deletion
                WHERE deletion.RoomId = m.RoomId
                  AND deletion.Type = 'messages.delete'
                  AND lower(COALESCE(json_extract(deletion.PayloadJson, '$.meta.message_id'), '')) = lower(m.MessageId)
            )
            """);
        return string.Join("\nAND ", clauses);
    }

    private static readonly string[] DayLabels =
        ["星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日"];

    private SqliteCommand CreateFilteredCommand(
        SqliteConnection connection,
        string sql,
        AnalyticsFilter filter)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqliteParameter("@startTime", SqliteType.Text)
        {
            Value = FormatSqliteTimestamp(filter.StartTime),
        });
        command.Parameters.Add(new SqliteParameter("@endTime", SqliteType.Text)
        {
            Value = FormatSqliteTimestamp(filter.EndTime),
        });
        if (NormalizeGuid(filter.ConversationId) is { } room)
        {
            command.Parameters.Add(new SqliteParameter("@conversationId", SqliteType.Text) { Value = room });
        }

        if (filter.OnlyCurrentUser)
        {
            command.Parameters.Add(new SqliteParameter("@currentUserId", SqliteType.Text)
            {
                Value = NormalizeGuid(filter.CurrentUserId)?.ToLowerInvariant() ?? string.Empty,
            });
        }

        command.Parameters.Add(new SqliteParameter("@textMessageType", SqliteType.Text)
        {
            Value = TextMessageType,
        });
        return command;
    }

    private async Task<T> ExecuteAsync<T>(
        Func<SqliteConnection, Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            return await action(connection).ConfigureAwait(false);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("本地聊天数据库结构不完整，请先完成聊天数据迁移。", ex);
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException($"读取本地聊天统计失败（SQLite {ex.SqliteErrorCode}）。", ex);
        }
    }

    private Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => _connections.OpenReadOnlyConnectionAsync(cancellationToken);

    private static void ValidateFilter(AnalyticsFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.EndTime <= filter.StartTime)
        {
            throw new ArgumentException("统计结束时间必须晚于开始时间。", nameof(filter));
        }

        if (filter.OnlyCurrentUser && NormalizeGuid(filter.CurrentUserId) is null)
        {
            throw new InvalidOperationException("无法确定当前登录用户，不能启用“仅看我发送”。");
        }

        if (!string.IsNullOrWhiteSpace(filter.ConversationId)
            && NormalizeGuid(filter.ConversationId) is null)
        {
            throw new ArgumentException("会话 ID 格式无效。", nameof(filter));
        }
    }

    private static string FormatSqliteTimestamp(DateTimeOffset value)
        => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture);

    private static string? NormalizeGuid(string? value)
        => Guid.TryParse(value, out var id) && id != Guid.Empty ? id.ToString("D") : null;

    private static string FormatBucketLabel(DateTimeOffset value, TrendGranularity granularity)
        => granularity switch
        {
            TrendGranularity.Month => value.ToString("yyyy年MM月", CultureInfo.CurrentCulture),
            TrendGranularity.Week => value.ToString("MM-dd", CultureInfo.CurrentCulture) + " 周",
            _ => value.ToString("MM-dd", CultureInfo.CurrentCulture),
        };

    private static string MaskId(string id)
    {
        var compact = id.Replace("-", string.Empty, StringComparison.Ordinal);
        return compact.Length >= 8 ? $"用户 {compact[..4]}…{compact[^4..]}" : "未知成员";
    }
}
