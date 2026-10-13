using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SolarWin.Data;

#nullable disable

namespace SolarWin.Data.Migrations
{
    /// <summary>
    /// Adds the FTS5 virtual table <c>chat_messages_fts</c> for full-text search over local
    /// chat history, plus three sync triggers that mirror <c>chat_messages.Content</c> changes.
    ///
    /// Design notes:
    /// <list type="bullet">
    /// <item>External-content FTS5 with <c>tokenize='trigram'</c>: substrings of any length are
    /// matched; CJK content is naturally searchable without a custom analyzer (every UTF-8 codepoint
    /// is a trigram character). 1-2 character queries fall back to a <c>LIKE</c> scan in the
    /// repository because trigram MATCH requires 3 or more chars.</item>
    /// <item><c>content_rowid='rowid'</c> maps to the implicit rowid of <c>chat_messages</c>
    /// (TEXT primary key, not <c>WITHOUT ROWID</c>).</item>
    /// <item>Triggers intentionally stay "dumb" (no WHEN clauses for INSERT/UPDATE) and let the
    /// query-time filter decide what counts as searchable text. The 'delete' command is always
    /// emitted on UPDATE/DELETE; re-tokenizing an empty or never-indexed row is a harmless no-op.</item>
    /// <item>Backfill uses <c>INSERT INTO fts(fts) VALUES('rebuild')</c> to populate from any
    /// existing rows in <c>chat_messages</c>.</item>
    /// <item>Stays in the per-account database; the FTS table is dropped together with the account
    /// library on logout (no cross-account leakage).</item>
    /// </list>
    /// </summary>
    [DbContext(typeof(SolarWinDbContext))]
    [Migration("20260913140000_AddChatMessagesFts")]
    public partial class AddChatMessagesFts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The DDL lives in ChatMessagesFtsDdl so the repository-layer tests can replay the
            // exact same statements onto a scratch database instead of a hand-copied duplicate.
            foreach (var statement in ChatMessagesFtsDdl.UpStatements)
            {
                migrationBuilder.Sql(statement);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var statement in ChatMessagesFtsDdl.DownStatements)
            {
                migrationBuilder.Sql(statement);
            }
        }
    }
}