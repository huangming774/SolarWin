using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SolarWin.Data;
using SolarWin.Models;

namespace SolarWin.Repositories;

/// <summary>
/// SQLite-backed chat search. Follows <c>ChatAnalyticsRepository</c> conventions: opens a
/// <see cref="IDbConnectionFactory"/> read-only connection, builds a parameterised command,
/// and reads results with explicit ordinal columns. No string interpolation into SQL.
/// </summary>
public sealed class ChatSearchRepository : IChatSearchRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ChatSearchRepository(IDbConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        _connectionFactory = connectionFactory;
    }

    // Column ordinals — keep stable; reused by both queries.
    private const int ColMessageId = 0;
    private const int ColRoomId = 1;
    private const int ColRoomSequence = 2;
    private const int ColCreatedAt = 3;
    private const int ColSenderId = 4;
    private const int ColSenderName = 5;
    private const int ColRoomName = 6;
    private const int ColContent = 7;

    // Searchable-text predicate — the single place that decides what counts as a hit. The FTS
    // index mirrors Content unconditionally (see ChatMessagesFtsDdl), so all filtering lives here.
    // Kept as one literal block so the SQL string is trivially auditable.
    //
    // The retraction probe's expression must stay VERBATIM identical to the expression indexed by
    // ChatMessagesFtsDdl.CreateRetractionIndex, including the absence of COALESCE. SQLite matches
    // expression indexes textually; a COALESCE wrapper here makes the index unusable and the probe
    // falls back to parsing every room-mate's ~11 KB PayloadJson per candidate hit, which grows
    // with the square of the room size (measured 16,337 ms -> 12.35 ms on an 8000-message room).
    // The wrapper was dropped because it changes nothing: a retraction row with no message_id
    // yields NULL in the index, and `NULL = <guid>` is not true, which is the same outcome as the
    // old `'' = <guid>`. ChatSearchRepositoryTests asserts the plan actually uses the index.
    private const string SearchablePredicate = @"
        m.Type = 'text'
        AND m.DeletedAt IS NULL
        AND m.Content IS NOT NULL
        AND trim(m.Content) != ''
        AND m.Content NOT LIKE '🔒%'
        AND NOT EXISTS (
            SELECT 1
            FROM chat_messages AS deletion
            WHERE deletion.RoomId = m.RoomId
              AND deletion.Type = 'messages.delete'
              AND lower(json_extract(deletion.PayloadJson, '$.meta.message_id')) = lower(m.MessageId)
        )";

    // Sender / room name fallback chain mirrors GetConversationsAsync in the analytics repo.
    private const string SenderNameExpr = @"
        COALESCE(
            NULLIF(json_extract(m.PayloadJson, '$.sender.nick'), ''),
            NULLIF(json_extract(m.PayloadJson, '$.sender.username'), ''),
            NULLIF(json_extract(m.PayloadJson, '$.sender.account.nick'), ''),
            NULLIF(json_extract(m.PayloadJson, '$.sender.account.name'), ''),
            NULLIF(json_extract(m.PayloadJson, '$.sender.account.username'), '')
        )";

    private const string RoomNameExpr = @"
        COALESCE(
            NULLIF(r.Name, ''),
            NULLIF(json_extract(r.PayloadJson, '$.account.nick'), ''),
            NULLIF(json_extract(r.PayloadJson, '$.account.name'), ''),
            NULLIF(json_extract(r.PayloadJson, '$.title'), ''),
            NULLIF(json_extract(r.PayloadJson, '$.name'), ''),
            '私聊'
        )";

    private const string SelectColumns = @"
        m.MessageId           AS MessageId,
        m.RoomId              AS RoomId,
        m.RoomSequence        AS RoomSequence,
        m.CreatedAt           AS CreatedAt,
        m.SenderId            AS SenderId,
        " + SenderNameExpr + @" AS SenderName,
        " + RoomNameExpr + @"    AS RoomName,
        m.Content             AS Content";

    /// <summary>
    /// The statement issued by <see cref="SearchByFtsAsync"/>. Public so tests can run
    /// <c>EXPLAIN QUERY PLAN</c> against the exact text that ships — the retraction probe only
    /// stays cheap while its expression matches <c>ChatMessagesFtsDdl.CreateRetractionIndex</c>
    /// verbatim, and nothing about a mismatch surfaces as an error.
    /// </summary>
    public static string BuildFtsQuery() => @"
SELECT " + SelectColumns + @"
FROM chat_messages_fts AS fts
JOIN chat_messages AS m ON fts.rowid = m._rowid_
LEFT JOIN chat_rooms AS r ON r.RoomId = m.RoomId
WHERE chat_messages_fts MATCH @phrase
  AND " + SearchablePredicate + @"
  AND (@roomId IS NULL OR m.RoomId = @roomId)
ORDER BY m.CreatedAt DESC, m.RoomSequence DESC
LIMIT @take OFFSET @offset;";

    /// <summary>The statement issued by <see cref="SearchByLikeAsync"/>. See <see cref="BuildFtsQuery"/>.</summary>
    public static string BuildLikeQuery() => @"
SELECT " + SelectColumns + @"
FROM chat_messages AS m
LEFT JOIN chat_rooms AS r ON r.RoomId = m.RoomId
WHERE m.Content LIKE @pattern ESCAPE '\'
  AND " + SearchablePredicate + @"
  AND (@roomId IS NULL OR m.RoomId = @roomId)
ORDER BY m.CreatedAt DESC, m.RoomSequence DESC
LIMIT @take OFFSET @offset;";

    public async Task<IReadOnlyList<ChatSearchRow>> SearchByFtsAsync(
        string phrase,
        Guid? roomId,
        int take,
        int offset,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(phrase);

        // Join on _rowid_, not rowid: the entity's Guid RowId column shadows SQLite's implicit
        // rowid (see ChatMessagesFtsDdl for the full explanation).
        var sql = BuildFtsQuery();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqliteParameter("@phrase", SqliteType.Text) { Value = phrase });
        command.Parameters.Add(new SqliteParameter("@roomId", SqliteType.Text)
        {
            Value = roomId is { } g ? NormaliseGuid(g) : DBNull.Value,
        });
        command.Parameters.Add(new SqliteParameter("@take", SqliteType.Integer) { Value = take });
        command.Parameters.Add(new SqliteParameter("@offset", SqliteType.Integer) { Value = offset });

        return await ReadRowsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ChatSearchRow>> SearchByLikeAsync(
        string likePattern,
        Guid? roomId,
        int take,
        int offset,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(likePattern);

        var sql = BuildLikeQuery();

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqliteParameter("@pattern", SqliteType.Text) { Value = likePattern });
        command.Parameters.Add(new SqliteParameter("@roomId", SqliteType.Text)
        {
            Value = roomId is { } g ? NormaliseGuid(g) : DBNull.Value,
        });
        command.Parameters.Add(new SqliteParameter("@take", SqliteType.Integer) { Value = take });
        command.Parameters.Add(new SqliteParameter("@offset", SqliteType.Integer) { Value = offset });

        return await ReadRowsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _connectionFactory
                .OpenReadOnlyConnectionAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SqliteException ex)
        {
            // chat_messages_fts may not exist yet on a brand-new account that just got bound
            // before the migration ran, or on a database mid-upgrade. Surface a friendly hint
            // rather than the raw "no such table" text.
            throw new InvalidOperationException(
                "搜索索引不可用 —— 聊天全文搜索虚表不存在。请确认已完成数据库迁移后重试。",
                ex);
        }
    }

    private async Task<IReadOnlyList<ChatSearchRow>> ReadRowsAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<ChatSearchRow>(capacity: 32);
        await using var reader = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // CreatedAt is persisted as TEXT by EF Core's DateTimeOffsetToStringConverter
            // (round-trip "o"-ish format). Parse explicitly so we get a real DTO regardless
            // of the column's declared type affinity.
            var createdAt = reader.IsDBNull(ColCreatedAt)
                ? (DateTimeOffset?)null
                : DateTimeOffset.Parse(
                    reader.GetString(ColCreatedAt),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

            rows.Add(new ChatSearchRow(
                MessageId: Guid.ParseExact(reader.GetString(ColMessageId), "D"),
                RoomId: Guid.ParseExact(reader.GetString(ColRoomId), "D"),
                RoomSequence: reader.GetInt64(ColRoomSequence),
                CreatedAt: createdAt,
                SenderId: Guid.ParseExact(reader.GetString(ColSenderId), "D"),
                SenderName: reader.IsDBNull(ColSenderName) ? null : reader.GetString(ColSenderName),
                RoomName: reader.IsDBNull(ColRoomName) ? null : reader.GetString(ColRoomName),
                Content: reader.GetString(ColContent)));
        }

        return rows;
    }

    // EF Core's SQLite provider persists Guid as TEXT in UPPER-case "D" format, and the column
    // has no COLLATE NOCASE, so comparisons are case-sensitive. Normalising to lower-case here
    // would make every room-filtered search match zero rows.
    private static string NormaliseGuid(Guid value) => value.ToString("D").ToUpperInvariant();
}