namespace SolarWin.Data;

/// <summary>
/// Raw DDL for the chat search feature: the <c>chat_messages_fts</c> FTS5 virtual table, its
/// three sync triggers, and the partial expression index that keeps the retraction probe cheap.
///
/// This is the single source of truth: the EF migration
/// <c>20260913140000_AddChatMessagesFts</c> feeds these exact strings to
/// <c>MigrationBuilder.Sql</c>, and the repository-layer tests replay them onto a scratch
/// database. One copy means the tests cannot silently drift from what ships.
///
/// <para>
/// <b>Everything keys on <c>_rowid_</c>, never on <c>rowid</c>.</b> The chat message entity has
/// a <c>Guid RowId</c> primary key, which EF Core materialises as a column named
/// <c>"RowId"</c>. SQLite resolves identifiers case-insensitively, so a column named
/// <c>RowId</c> <i>shadows</i> the implicit integer rowid: <c>SELECT rowid FROM chat_messages</c>
/// returns the Guid text, not the row number (<c>_rowid_</c> and <c>oid</c> still reach the real
/// one). FTS5 interpolates <c>content_rowid</c> straight into its internal content query, so
/// declaring <c>content_rowid='rowid'</c> made it read Guid text and coerce it to an integer by
/// numeric-prefix rules — GUIDs beginning with a letter all collapsed to <c>0</c>, and every
/// <c>JOIN … ON fts.rowid = m.rowid</c> failed, so search returned nothing at all. The same
/// shadowing hits trigger bodies: <c>new.rowid</c> is the Guid, and handing that to FTS5 raises
/// <c>datatype mismatch</c>, which would have failed every subsequent message insert.
/// </para>
///
/// <para>
/// <b>The index is a faithful mirror of <c>chat_messages.Content</c>; searchability is decided
/// only at query time.</b> The triggers carry no <c>WHEN</c> clause on purpose. FTS5's
/// <c>'rebuild'</c> re-reads the content table directly and never fires triggers, so any
/// condition applied to the incremental paths would make the index disagree with itself
/// depending on whether a row predates the migration — rows would be silently unsearchable, and
/// the UPDATE path would emit <c>'delete'</c> for text that was never indexed. Mirroring
/// unconditionally removes that whole class of bug; the cost is negligible because the excluded
/// rows (non-text messages, 🔒 placeholders, blanks) are a tiny fraction of the corpus and the
/// query-time predicate in <c>ChatSearchRepository</c> filters them out anyway — along with
/// retracted messages, which no trigger condition could express.
/// </para>
///
/// <para>
/// <see cref="UpStatements"/> is a full reset: it drops any pre-existing trigger and table before
/// recreating them, so a database that ran an earlier, broken revision of this migration
/// converges to the correct shape instead of keeping the bad artifacts. The FTS index is derived
/// data rebuilt from <c>chat_messages</c>, so nothing is lost. Order matters: triggers must go
/// before the table they reference, and the backfill must run last.
/// </para>
/// </summary>
public static class ChatMessagesFtsDdl
{
    /// <summary>
    /// External-content FTS5 table. <c>content='chat_messages'</c> makes the virtual table a
    /// pure index over the real table (no duplicated storage).
    ///
    /// <c>content_rowid='_rowid_'</c> is load-bearing — see the type-level remarks; <c>'rowid'</c>
    /// resolves to the Guid <c>RowId</c> column and breaks the join.
    ///
    /// <c>tokenize='trigram'</c> is what makes CJK searchable without a custom analyzer: every
    /// trigram of the text is indexed, so MATCH behaves as substring search. The trade-off is
    /// that trigram MATCH requires 3+ characters — which is why the service routes 1–2 character
    /// queries (very common in Chinese, e.g. 「露营」) to the LIKE scan instead.
    /// </summary>
    public const string CreateTable = @"
        CREATE VIRTUAL TABLE IF NOT EXISTS chat_messages_fts USING fts5(
            Content,
            content='chat_messages',
            content_rowid='_rowid_',
            tokenize='trigram'
        );
    ";

    /// <summary>AFTER INSERT — mirror the row verbatim. NULL/blank content simply yields no tokens.</summary>
    public const string CreateInsertTrigger = @"
        CREATE TRIGGER IF NOT EXISTS chat_messages_ai AFTER INSERT ON chat_messages
        BEGIN
            INSERT INTO chat_messages_fts(rowid, Content) VALUES (new._rowid_, new.Content);
        END;
    ";

    /// <summary>
    /// AFTER DELETE — mirror the removal. External-content <c>'delete'</c> must be handed the
    /// values that were indexed, which is exactly what <c>old.*</c> gives us; passing them
    /// explicitly rather than re-reading the row also avoids racing a concurrent UPDATE.
    /// </summary>
    public const string CreateDeleteTrigger = @"
        CREATE TRIGGER IF NOT EXISTS chat_messages_ad AFTER DELETE ON chat_messages
        BEGIN
            INSERT INTO chat_messages_fts(chat_messages_fts, rowid, Content)
            VALUES('delete', old._rowid_, old.Content);
        END;
    ";

    /// <summary>
    /// AFTER UPDATE OF Content, Type, DeletedAt — every column whose change could imply a
    /// different mirror state. EF Core only emits SET clauses for properties that actually
    /// changed, so the OF-list is a reliable narrowing. Old and new are both mirrored so the net
    /// index state always tracks the current row.
    /// </summary>
    public const string CreateUpdateTrigger = @"
        CREATE TRIGGER IF NOT EXISTS chat_messages_au
            AFTER UPDATE OF Content, Type, DeletedAt ON chat_messages
        BEGIN
            INSERT INTO chat_messages_fts(chat_messages_fts, rowid, Content)
            VALUES('delete', old._rowid_, old.Content);

            INSERT INTO chat_messages_fts(rowid, Content)
            VALUES (new._rowid_, new.Content);
        END;
    ";

    /// <summary>
    /// Backfill / re-index. 'rebuild' walks the content table and re-tokenizes every row; it is
    /// also the repair path to run if the index ever drifts.
    /// </summary>
    public const string Rebuild = @"
        INSERT INTO chat_messages_fts(chat_messages_fts) VALUES('rebuild');
    ";

    /// <summary>
    /// Partial expression index backing the retraction probe in
    /// <c>ChatSearchRepository.SearchablePredicate</c>.
    ///
    /// <para>
    /// <b>The indexed expression must appear verbatim in the query, including the absence of any
    /// <c>COALESCE</c>.</b> SQLite only considers an expression index when the query text matches
    /// the indexed expression exactly; wrapping the column in <c>COALESCE(…, '')</c> — which the
    /// predicate used to do — silently disqualifies it and the probe degrades to an unbounded
    /// scan of every message in the room. That is a 1000x-class regression that produces no
    /// error, which is why <c>ChatSearchRepositoryTests</c> asserts on the query plan.
    /// </para>
    ///
    /// <para>
    /// Without this index the probe correlates only on <c>RoomId</c> and then parses the ~11 KB
    /// <c>PayloadJson</c> of every row in the room, once per candidate hit — i.e. cost grows with
    /// the square of room size. Measured on a single 8000-message room: 16,337 ms to 12.35 ms.
    /// With the index the extracted value is computed once at write time and the probe becomes a
    /// bounded seek.
    /// </para>
    ///
    /// <para>
    /// It is partial (<c>WHERE Type = 'messages.delete'</c>) so it only carries retraction rows —
    /// about 0.2% of writes — and measured write throughput is unchanged. The <c>WHERE</c> clause
    /// is also why the predicate must keep testing <c>Type</c> explicitly for the planner to be
    /// allowed to use it.
    /// </para>
    /// </summary>
    public const string CreateRetractionIndex = @"
        CREATE INDEX IF NOT EXISTS ix_chat_messages_retraction
        ON chat_messages (RoomId, lower(json_extract(PayloadJson, '$.meta.message_id')))
        WHERE Type = 'messages.delete';
    ";

    /// <summary>
    /// Full ordered reset. The drops are what make this idempotent and self-healing; a fresh
    /// database pays nothing for them.
    /// </summary>
    public static readonly string[] UpStatements =
    [
        "DROP TRIGGER IF EXISTS chat_messages_au;",
        "DROP TRIGGER IF EXISTS chat_messages_ad;",
        "DROP TRIGGER IF EXISTS chat_messages_ai;",
        "DROP TABLE IF EXISTS chat_messages_fts;",
        CreateTable,
        CreateInsertTrigger,
        CreateDeleteTrigger,
        CreateUpdateTrigger,
        CreateRetractionIndex,
        Rebuild,
    ];

    /// <summary>
    /// Teardown in an order that cannot leave a trigger pointing at a dropped table.
    /// </summary>
    public static readonly string[] DownStatements =
    [
        "DROP TRIGGER IF EXISTS chat_messages_au;",
        "DROP TRIGGER IF EXISTS chat_messages_ad;",
        "DROP TRIGGER IF EXISTS chat_messages_ai;",
        "DROP TABLE IF EXISTS chat_messages_fts;",
        "DROP INDEX IF EXISTS ix_chat_messages_retraction;",
    ];
}
