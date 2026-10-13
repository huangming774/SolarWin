using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SolarWin.Data;

/// <summary>
/// After each connection open: enable WAL + NORMAL sync (design v1.0 §7), and memory-map the
/// database file.
///
/// <para>
/// <c>mmap_size</c> is the single highest-leverage pragma for this workload. Chat search reads
/// the same few pages repeatedly (the FTS index plus <c>PayloadJson</c>, which averages ~11 KB
/// per row), and without mapping every one of those reads is a pread through the page cache.
/// Measured on a 96 MB database, interleaved and repeated: mapping alone cut a broad FTS query
/// from 92.89 ms to 25.15 ms (3.69x) and a 2-character LIKE scan by 8.1x. For contrast,
/// <c>cache_size</c> and <c>temp_store=MEMORY</c> measured 0.99x and 1.00x on the same queries —
/// they are deliberately not set, since they would only add memory use and lose the FILE-backed
/// temp semantics without buying anything.
/// </para>
///
/// <para>
/// Windows cannot truncate or delete a file while it is mapped, so this interacts with
/// "clear chat cache". <c>AccountDbContextFactory.DeleteDatabaseFiles</c> already calls
/// <c>SqliteConnection.ClearAllPools()</c> before deleting, which unmaps before the delete, so the
/// ordering is safe. Note that path swallows failures: a residual mapping would leave a stale
/// database behind silently rather than raising.
/// </para>
/// </summary>
public sealed class SqliteWalConnectionInterceptor : DbConnectionInterceptor
{
    private const string PragmaSql =
        "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA mmap_size=268435456;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragmas(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ApplyPragmasAsync(connection, cancellationToken).ConfigureAwait(false);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyPragmas(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = PragmaSql;
        cmd.ExecuteNonQuery();
    }

    private static async Task ApplyPragmasAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = PragmaSql;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
