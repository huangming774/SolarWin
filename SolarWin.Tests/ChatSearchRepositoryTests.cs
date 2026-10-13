using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;
using SolarWin.Data;
using SolarWin.Repositories;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.Tests;

/// <summary>
/// End-to-end repository tests against a real SQLite database. The scratch schema reproduces
/// what EF Core actually emits — most importantly a <c>TEXT PRIMARY KEY</c> column named
/// <c>RowId</c>, which shadows SQLite's implicit <c>rowid</c> and is what made the first cut of
/// the FTS wiring silently return zero results. GUIDs are written in UPPER-case "D" format
/// because that is what EF Core persists, and the columns carry no <c>COLLATE NOCASE</c>.
///
/// Everything here exercises the shipping DDL via <see cref="ChatMessagesFtsDdl.UpStatements"/>,
/// so a regression in the migration fails these tests instead of leaking into a release.
/// </summary>
public class ChatSearchRepositoryTests
{
    private const string Placeholder = "\U0001F512 无法解密的 MLS 消息";

    // ----- FTS path (3+ character queries) -----

    [Fact]
    public async Task Fts_FindsSubstringInsideLongerCjkText()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        await db.AddMessageAsync("虽然现在没什么钱的说");
        var repo = db.CreateRepository();

        var rows = await repo.SearchByFtsAsync("\"现在没\"", null, 50, 0, default);

        Assert.Single(rows);
        Assert.Equal("虽然现在没什么钱的说", rows[0].Content);
    }

    [Fact]
    public async Task Fts_ReturnsNothingForTextThatWasNeverIndexed()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        await db.AddMessageAsync("明天去露营吧");
        var repo = db.CreateRepository();

        var rows = await repo.SearchByFtsAsync("\"爬山\"", null, 50, 0, default);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Fts_IndexesRowsThatPredateTheMigration()
    {
        await using var db = await TempChatDatabase.CreateAsync(applyFts: false);
        await db.AddMessageAsync("明天去露营吧");   // written before the FTS DDL runs
        await db.ApplyFtsMigrationAsync();
        var repo = db.CreateRepository();

        var rows = await repo.SearchByFtsAsync("\"去露营\"", null, 50, 0, default);

        Assert.Single(rows);
    }

    [Fact]
    public async Task Fts_MapsAllProjectedColumns()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        var roomId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        await db.AddRoomAsync(roomId, name: "露营群");
        await db.AddMessageAsync(
            "明天去露营吧",
            roomId: roomId,
            messageId: messageId,
            senderId: senderId,
            senderNick: "小明",
            roomSequence: 42);
        var repo = db.CreateRepository();

        var row = Assert.Single(
            await repo.SearchByFtsAsync("\"去露营\"", null, 50, 0, default));

        Assert.Equal(messageId, row.MessageId);
        Assert.Equal(roomId, row.RoomId);
        Assert.Equal(senderId, row.SenderId);
        Assert.Equal(42, row.RoomSequence);
        Assert.Equal("小明", row.SenderName);
        Assert.Equal("露营群", row.RoomName);
        Assert.NotNull(row.CreatedAt);
    }

    [Fact]
    public async Task Fts_OrdersNewestFirst()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        await db.AddMessageAsync("露营第一天", roomSequence: 1, createdAt: new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        await db.AddMessageAsync("露营第二天", roomSequence: 2, createdAt: new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero));
        await db.AddMessageAsync("露营第三天", roomSequence: 3, createdAt: new DateTimeOffset(2026, 9, 3, 0, 0, 0, TimeSpan.Zero));
        var repo = db.CreateRepository();

        var rows = await repo.SearchByFtsAsync("\"露营第\"", null, 50, 0, default);

        Assert.Equal(["露营第三天", "露营第二天", "露营第一天"], rows.Select(r => r.Content));
    }

    [Fact]
    public async Task Fts_HonoursLimitAndOffset()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        for (var i = 1; i <= 5; i++)
        {
            await db.AddMessageAsync($"露营第{i}天", roomSequence: i, createdAt: new DateTimeOffset(2026, 9, i, 0, 0, 0, TimeSpan.Zero));
        }
        var repo = db.CreateRepository();

        var page = await repo.SearchByFtsAsync("\"露营第\"", null, 2, 1, default);

        Assert.Equal(["露营第4天", "露营第3天"], page.Select(r => r.Content));
    }

    // ----- LIKE path (1-2 character queries) -----

    [Theory]
    [InlineData("\U0001F600\U0001F600", ChatSearchSource.Like)]
    [InlineData("\U00020000\U00020001", ChatSearchSource.Like)]
    [InlineData("a\U00020000", ChatSearchSource.Like)]
    [InlineData("ab\U00020000", ChatSearchSource.Fts)]
    public async Task SearchService_FindsSupplementaryUnicodeSubstring(string query, ChatSearchSource expected)
    {
        await using var db = await TempChatDatabase.CreateAsync();
        await db.AddMessageAsync("before " + query + " after");
        var service = new ChatSearchService(db.CreateRepository());

        var page = await service.SearchAsync(new ChatSearchRequest(query, new ChatSearchFilter()), default);

        Assert.Equal(expected, page.Source);
        Assert.Single(page.Rows);
    }

    [Fact]
    public async Task Like_FindsTwoCharacterCjkQuery()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        await db.AddMessageAsync("明天去露营吧");
        await db.AddMessageAsync("今天加班");
        var repo = db.CreateRepository();

        var rows = await repo.SearchByLikeAsync("%露营%", null, 50, 0, default);

        Assert.Single(rows);
        Assert.Equal("明天去露营吧", rows[0].Content);
    }

    [Fact]
    public async Task Like_TreatsWildcardsInPatternAsLiteralsWhenEscaped()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        await db.AddMessageAsync("进度 100% 了");
        await db.AddMessageAsync("无关消息");
        var repo = db.CreateRepository();

        var rows = await repo.SearchByLikeAsync("%100\\%%", null, 50, 0, default);

        Assert.Single(rows);
        Assert.Equal("进度 100% 了", rows[0].Content);
    }

    // ----- Query-time filtering -----

    [Fact]
    public async Task UndecryptablePlaceholder_IsNeverReturned()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        await db.AddMessageAsync(Placeholder);
        var repo = db.CreateRepository();

        Assert.Empty(await repo.SearchByFtsAsync("\"无法解密\"", null, 50, 0, default));
        Assert.Empty(await repo.SearchByLikeAsync("%无法%", null, 50, 0, default));
    }

    [Fact]
    public async Task SoftDeletedMessage_IsExcluded()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        await db.AddMessageAsync("明天去露营吧", deletedAt: DateTimeOffset.UtcNow);
        var repo = db.CreateRepository();

        Assert.Empty(await repo.SearchByFtsAsync("\"去露营\"", null, 50, 0, default));
    }

    [Fact]
    public async Task NonTextMessageType_IsExcluded()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        await db.AddMessageAsync("明天去露营吧", type: "messages.sync.links");
        var repo = db.CreateRepository();

        Assert.Empty(await repo.SearchByFtsAsync("\"去露营\"", null, 50, 0, default));
    }

    [Fact]
    public async Task BlankContent_IsExcluded()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        await db.AddMessageAsync("   ");
        var repo = db.CreateRepository();

        var rows = await repo.SearchByLikeAsync("% %", null, 50, 0, default);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task RetractedMessage_IsExcluded()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        var roomId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        await db.AddMessageAsync("明天去露营吧", roomId: roomId, messageId: targetId);
        var repo = db.CreateRepository();
        Assert.Single(await repo.SearchByFtsAsync("\"去露营\"", null, 50, 0, default));

        // A retraction event arrives as its own row referencing the target by message id.
        await db.AddMessageAsync(
            content: null,
            roomId: roomId,
            type: "messages.delete",
            deletedTarget: targetId);

        Assert.Empty(await repo.SearchByFtsAsync("\"去露营\"", null, 50, 0, default));
    }

    // ----- Room filter (regression: case-sensitive GUID comparison) -----

    [Fact]
    public async Task RoomFilter_MatchesUpperCasedStoredGuid()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        var targetRoom = Guid.Parse("00000000-0000-0000-0000-00000000008B");
        await db.AddMessageAsync("明天去露营吧", roomId: targetRoom);
        await db.AddMessageAsync("明天去露营吧", roomId: Guid.NewGuid());
        var repo = db.CreateRepository();

        var rows = await repo.SearchByFtsAsync("\"去露营\"", targetRoom, 50, 0, default);

        Assert.Single(rows);
        Assert.Equal(targetRoom, rows[0].RoomId);
    }

    [Fact]
    public async Task RoomFilter_EmptyWhenNoMatchInThatRoom()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        await db.AddMessageAsync("明天去露营吧", roomId: Guid.NewGuid());
        var repo = db.CreateRepository();

        var rows = await repo.SearchByFtsAsync(
            "\"去露营\"", Guid.NewGuid(), 50, 0, default);

        Assert.Empty(rows);
    }

    // ----- Trigger synchronisation -----

    [Fact]
    public async Task DecryptRecovery_LeavesNoStaleTokensBehind()
    {
        // The hazard the mirror-style triggers exist to prevent: a message lands as an
        // undecryptable placeholder, then the user recovers it from the encryption settings.
        // If the old text were not mirrored out of the index, the recovered row would keep
        // matching queries it no longer contains.
        await using var db = await TempChatDatabase.CreateAsync();
        var messageId = Guid.NewGuid();
        await db.AddMessageAsync(Placeholder, messageId: messageId);
        var repo = db.CreateRepository();

        await db.UpdateContentAsync(messageId, "露营真开心");

        Assert.Empty(await repo.SearchByFtsAsync("\"无法解密\"", null, 50, 0, default));
        var rows = await repo.SearchByFtsAsync("\"露营真开心\"", null, 50, 0, default);
        Assert.Single(rows);
    }

    [Fact]
    public async Task DeletingARowRemovesItFromTheIndex()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        var messageId = Guid.NewGuid();
        await db.AddMessageAsync("明天去露营吧", messageId: messageId);
        var repo = db.CreateRepository();
        Assert.Single(await repo.SearchByFtsAsync("\"去露营\"", null, 50, 0, default));

        await db.DeleteMessageAsync(messageId);

        Assert.Empty(await repo.SearchByFtsAsync("\"去露营\"", null, 50, 0, default));
    }

    [Fact]
    public async Task MigrationIsIdempotent()
    {
        await using var db = await TempChatDatabase.CreateAsync(applyFts: false);
        await db.AddMessageAsync("明天去露营吧");
        await db.ApplyFtsMigrationAsync();
        await db.ApplyFtsMigrationAsync();
        var repo = db.CreateRepository();

        var rows = await repo.SearchByFtsAsync("\"去露营\"", null, 50, 0, default);

        Assert.Single(rows);
        Assert.Equal(1, await db.CountIndexedRowsAsync());
    }

    [Fact]
    public async Task MigrationRepairsAnIndexBuiltByTheBrokenRevision()
    {
        // Simulates a database that ran the first revision, where content_rowid pointed at the
        // shadowing RowId column and every key collapsed to a numeric-prefix of the GUID.
        await using var db = await TempChatDatabase.CreateAsync(applyFts: false);
        await db.AddMessageAsync("明天去露营吧");
        await db.ExecuteAsync("""
            CREATE VIRTUAL TABLE chat_messages_fts USING fts5(
                Content, content='chat_messages', content_rowid='rowid', tokenize='trigram');
            INSERT INTO chat_messages_fts(chat_messages_fts) VALUES('rebuild');
            """);
        var repo = db.CreateRepository();
        Assert.Empty(await repo.SearchByFtsAsync("\"去露营\"", null, 50, 0, default));

        await db.ApplyFtsMigrationAsync();

        Assert.Single(await repo.SearchByFtsAsync("\"去露营\"", null, 50, 0, default));
    }

    [Fact]
    public async Task DownStatementsRemoveEveryArtifact()
    {
        await using var db = await TempChatDatabase.CreateAsync();
        await db.ApplyFtsMigrationAsync();

        foreach (var statement in ChatMessagesFtsDdl.DownStatements)
        {
            await db.ExecuteAsync(statement);
        }

        Assert.Equal(0, await db.CountObjectsAsync("chat_messages_fts"));
        Assert.Equal(0, await db.CountObjectsAsync("chat_messages_ai"));
        Assert.Equal(0, await db.CountObjectsAsync("chat_messages_ad"));
        Assert.Equal(0, await db.CountObjectsAsync("chat_messages_au"));
        Assert.Equal(0, await db.CountObjectsAsync("ix_chat_messages_retraction"));
    }

    // ----- Retraction probe performance guard -----

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RetractionProbe_IsAnsweredByTheExpressionIndex(bool ftsPath)
    {
        // The probe stays cheap only while its expression matches
        // ChatMessagesFtsDdl.CreateRetractionIndex verbatim -- no COALESCE wrapper, no extra
        // rounding. When it stops matching nothing errors: the probe just reverts to parsing
        // every room-mate's ~11 KB PayloadJson once per candidate hit, which measured 16,337 ms
        // against 12.35 ms on an 8000-message room. Pin the plan so a silent rewrite cannot ship.
        await using var db = await TempChatDatabase.CreateAsync();
        await db.AddMessageAsync("明天去露营吧");
        await db.AddMessageAsync(null, type: "messages.delete");

        var sql = ftsPath ? ChatSearchRepository.BuildFtsQuery() : ChatSearchRepository.BuildLikeQuery();
        var plan = await db.ExplainPlanAsync(
            sql,
            ("@phrase", "\"去露营\""),
            ("@pattern", "%露营%"),
            ("@roomId", null),
            ("@take", 60),
            ("@offset", 0));

        Assert.Contains(plan, line => line.Contains("ix_chat_messages_retraction", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RetractionProbe_DoesNotDegradeQuadraticallyWithRoomSize()
    {
        // The regression this guards is quadratic, so a plan assertion alone is not enough -- the
        // index has to actually be selective. One wide room, all rows matching the query, which is
        // the shape that blew up: with the COALESCE-wrapped predicate this same query measured
        // 16.3 seconds on 8000 messages. The bound below is ~200x the fixed cost, so it separates
        // the two states by a wide margin without being timing-flaky.
        await using var db = await TempChatDatabase.CreateAsync();
        var room = Guid.NewGuid();
        await db.AddRoomAsync(room, "大群");
        await db.AddMessagesBulkAsync(room, 8000, i => $"露营第{i}天");

        await db.AddMessageAsync(null, roomId: room, type: "messages.delete");
        await db.ExecuteAsync("INSERT INTO chat_messages_fts(chat_messages_fts) VALUES('rebuild')");
        var repo = db.CreateRepository();

        var elapsed = Stopwatch.StartNew();
        var rows = await repo.SearchByFtsAsync("\"露营第\"", null, 60, 0, default);
        elapsed.Stop();

        Assert.Equal(60, rows.Count);
        Assert.True(
            elapsed.Elapsed < TimeSpan.FromSeconds(2),
            $"broad search over an 8000-message room took {elapsed.ElapsedMilliseconds} ms; "
            + "the retraction probe has lost its index and is scanning the room again");
    }

    /// <summary>
    /// Scratch database mirroring the EF Core schema, plus the shipping FTS DDL. GUID columns are
    /// stored UPPER-case "D" text, as EF Core persists them.
    /// </summary>
    private sealed class TempChatDatabase : IDbConnectionFactory, IAsyncDisposable
    {
        private readonly string _path;

        private TempChatDatabase(string path) => _path = path;

        public static async Task<TempChatDatabase> CreateAsync(bool applyFts = true)
        {
            SQLitePCL.Batteries_V2.Init();
            var path = Path.Combine(Path.GetTempPath(), "solarwin-chatsearch-" + Guid.NewGuid().ToString("N") + ".db");
            var db = new TempChatDatabase(path);
            // Indexes mirror the ones EF Core's migrations create, minus the FTS ones. They are
            // not decoration: the query planner picks between them and the retraction index, so a
            // fixture without them would plan differently from production.
            await db.ExecuteAsync("""
                CREATE TABLE chat_messages (
                    RowId TEXT NOT NULL CONSTRAINT PK_chat_messages PRIMARY KEY,
                    MessageId TEXT NOT NULL, RoomId TEXT NOT NULL, RoomSequence INTEGER NOT NULL,
                    CreatedAt TEXT NULL, UpdatedAt TEXT NULL, DeletedAt TEXT NULL, Type TEXT NULL,
                    Content TEXT NULL, SenderId TEXT NOT NULL, ClientMessageId TEXT NULL,
                    IsEncrypted INTEGER NOT NULL, PayloadJson TEXT NULL, SyncedAt TEXT NOT NULL,
                    Source INTEGER NOT NULL);
                CREATE TABLE chat_rooms (
                    RoomId TEXT NOT NULL CONSTRAINT PK_chat_rooms PRIMARY KEY,
                    Type INTEGER NOT NULL, Pinned INTEGER NOT NULL, LastActivity TEXT NOT NULL,
                    UnreadCount INTEGER NOT NULL, LastReadSequence INTEGER NOT NULL, Name TEXT NULL,
                    PayloadJson TEXT NULL, DeletedAt TEXT NULL);
                CREATE INDEX IX_messages_room_seq_id ON chat_messages (RoomId, RoomSequence, MessageId);
                CREATE INDEX IX_messages_room_id ON chat_messages (RoomId, MessageId);
                CREATE INDEX IX_messages_room_created ON chat_messages (RoomId, CreatedAt);
                """);
            if (applyFts)
            {
                await db.ApplyFtsMigrationAsync();
            }

            return db;
        }

        /// <summary>
        /// The plan rows SQLite reports for <paramref name="sql"/>, bound exactly as the
        /// repository binds it. No <c>ANALYZE</c> is run — production never runs it either, and
        /// the point of asserting on the plan is to pin what production actually does.
        /// </summary>
        public async Task<IReadOnlyList<string>> ExplainPlanAsync(string sql, params (string Name, object? Value)[] parameters)
        {
            await using var connection = new SqliteConnection($"Data Source={_path};Mode=ReadOnly");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "EXPLAIN QUERY PLAN " + sql;
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }

            var details = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                // EXPLAIN QUERY PLAN yields (id, parent, notused, detail).
                details.Add(reader.GetString(3));
            }

            return details;
        }

        public ChatSearchRepository CreateRepository() => new(this);

        public Task<SqliteConnection> OpenReadOnlyConnectionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var connection = new SqliteConnection($"Data Source={_path};Mode=ReadOnly");
            return OpenAsync(connection, cancellationToken);
        }

        private static async Task<SqliteConnection> OpenAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        public async Task ApplyFtsMigrationAsync()
        {
            foreach (var statement in ChatMessagesFtsDdl.UpStatements)
            {
                await ExecuteAsync(statement);
            }
        }

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new SqliteConnection($"Data Source={_path}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public async Task AddRoomAsync(Guid roomId, string? name = null, string? payloadJson = null)
        {
            await using var connection = new SqliteConnection($"Data Source={_path}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO chat_rooms (RoomId, Type, Pinned, LastActivity, UnreadCount, LastReadSequence, Name, PayloadJson)
                VALUES (@room, 0, 0, @activity, 0, 0, @name, @payload);
                """;
            command.Parameters.AddWithValue("@room", Stored(roomId));
            command.Parameters.AddWithValue("@activity", Format(DateTimeOffset.UtcNow));
            command.Parameters.AddWithValue("@name", name is null ? DBNull.Value : name);
            command.Parameters.AddWithValue("@payload", payloadJson is null ? DBNull.Value : payloadJson);
            await command.ExecuteNonQueryAsync();
        }

        public async Task AddMessageAsync(
            string? content,
            Guid? roomId = null,
            Guid? messageId = null,
            Guid? senderId = null,
            string type = "text",
            long roomSequence = 1,
            DateTimeOffset? createdAt = null,
            DateTimeOffset? deletedAt = null,
            Guid? deletedTarget = null,
            string? senderNick = null)
        {
            var payload = new Dictionary<string, object>();
            if (deletedTarget is { } target)
            {
                payload["meta"] = new { message_id = target.ToString("D") };
            }

            if (senderNick is { } nick)
            {
                payload["sender"] = new { nick };
            }

            await using var connection = new SqliteConnection($"Data Source={_path}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO chat_messages
                    (RowId, MessageId, RoomId, RoomSequence, CreatedAt, DeletedAt, Type, Content,
                     SenderId, IsEncrypted, PayloadJson, SyncedAt, Source)
                VALUES
                    (@row, @message, @room, @sequence, @created, @deleted, @type, @content,
                     @sender, 0, @payload, @synced, 0);
                """;
            command.Parameters.AddWithValue("@row", Stored(Guid.NewGuid()));
            command.Parameters.AddWithValue("@message", Stored(messageId ?? Guid.NewGuid()));
            command.Parameters.AddWithValue("@room", Stored(roomId ?? Guid.NewGuid()));
            command.Parameters.AddWithValue("@sequence", roomSequence);
            command.Parameters.AddWithValue("@created", Format(createdAt ?? DateTimeOffset.UtcNow));
            command.Parameters.AddWithValue("@deleted", deletedAt is null ? DBNull.Value : Format(deletedAt.Value));
            command.Parameters.AddWithValue("@type", type);
            command.Parameters.AddWithValue("@content", content is null ? DBNull.Value : content);
            command.Parameters.AddWithValue("@sender", Stored(senderId ?? Guid.NewGuid()));
            command.Parameters.AddWithValue("@payload", System.Text.Json.JsonSerializer.Serialize(payload));
            command.Parameters.AddWithValue("@synced", Format(DateTimeOffset.UtcNow));
            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Bulk load over a single connection and transaction. <see cref="AddMessageAsync"/> opens
        /// a connection per row, which is right for two or three rows in a setup block and far too
        /// slow for the thousands the scaling test needs.
        /// </summary>
        public async Task AddMessagesBulkAsync(Guid roomId, int count, Func<int, string> contentFor)
        {
            await using var connection = new SqliteConnection($"Data Source={_path}");
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO chat_messages
                    (RowId, MessageId, RoomId, RoomSequence, CreatedAt, DeletedAt, Type, Content,
                     SenderId, IsEncrypted, PayloadJson, SyncedAt, Source)
                VALUES
                    (@row, @message, @room, @sequence, @created, NULL, 'text', @content,
                     @sender, 0, NULL, @synced, 0);
                """;
            var row = command.Parameters.Add("@row", SqliteType.Text);
            var message = command.Parameters.Add("@message", SqliteType.Text);
            var room = command.Parameters.Add("@room", SqliteType.Text);
            var sequence = command.Parameters.Add("@sequence", SqliteType.Integer);
            var created = command.Parameters.Add("@created", SqliteType.Text);
            var content = command.Parameters.Add("@content", SqliteType.Text);
            var sender = command.Parameters.Add("@sender", SqliteType.Text);
            var synced = command.Parameters.Add("@synced", SqliteType.Text);

            room.Value = Stored(roomId);
            synced.Value = Format(DateTimeOffset.UtcNow);
            sender.Value = Stored(Guid.NewGuid());
            for (var i = 0; i < count; i++)
            {
                row.Value = Stored(Guid.NewGuid());
                message.Value = Stored(Guid.NewGuid());
                sequence.Value = i + 1;
                created.Value = Format(DateTimeOffset.UtcNow.AddSeconds(i));
                content.Value = contentFor(i);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        public async Task UpdateContentAsync(Guid messageId, string content)
        {
            await using var connection = new SqliteConnection($"Data Source={_path}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE chat_messages SET Content = @content WHERE MessageId = @message;";
            command.Parameters.AddWithValue("@content", content);
            command.Parameters.AddWithValue("@message", Stored(messageId));
            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteMessageAsync(Guid messageId)
        {
            await using var connection = new SqliteConnection($"Data Source={_path}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM chat_messages WHERE MessageId = @message;";
            command.Parameters.AddWithValue("@message", Stored(messageId));
            await command.ExecuteNonQueryAsync();
        }

        public async Task<long> CountIndexedRowsAsync()
        {
            await using var connection = new SqliteConnection($"Data Source={_path};Mode=ReadOnly");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM chat_messages_fts;";
            return (long)(await command.ExecuteScalarAsync() ?? 0L);
        }

        public async Task<long> CountObjectsAsync(string name)
        {
            await using var connection = new SqliteConnection($"Data Source={_path};Mode=ReadOnly");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name = @name;";
            command.Parameters.AddWithValue("@name", name);
            return (long)(await command.ExecuteScalarAsync() ?? 0L);
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            try { File.Delete(_path); } catch { }
            return ValueTask.CompletedTask;
        }

        private static string Stored(Guid value) => value.ToString("D").ToUpperInvariant();

        private static string Format(DateTimeOffset value)
            => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture);
    }
}
