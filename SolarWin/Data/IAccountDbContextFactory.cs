namespace SolarWin.Data;

/// <summary>
/// Per-account SQLite factory: Bind / Switch / Remove + migrate gate (design v1.0 Phase 1).
/// Does not expose repositories or write pumps.
/// </summary>
public interface IAccountDbContextFactory
{
    /// <summary>Currently bound account; null if none.</summary>
    Guid? BoundAccountId { get; }

    /// <summary>Absolute path of the bound account database file, or null.</summary>
    string? BoundDatabasePath { get; }

    /// <summary>
    /// Bind to <paramref name="accountId"/> and start background <see cref="EnsureMigratedAsync"/>.
    /// Idempotent when already bound to the same id and ready.
    /// </summary>
    void Bind(Guid accountId);

    /// <summary>Switch active account (re-bind + migrate for the new id).</summary>
    void Switch(Guid accountId);

    /// <summary>
    /// Delete the account db file(s). If it is the bound account, unbind first.
    /// </summary>
    void Remove(Guid accountId);

    /// <summary>
    /// Phase 7: delete the currently bound account database, then re-bind + migrate an empty file.
    /// Caller should drain the write pump first.
    /// </summary>
    void ResetBoundDatabase();

    /// <summary>Create a short-lived context for the currently bound account.</summary>
    /// <exception cref="InvalidOperationException">No account bound.</exception>
    SolarWinDbContext CreateDbContext();

    /// <summary>
    /// Create a context only when <paramref name="accountId"/> is still the bound account.
    /// This prevents an operation that waited through a bind from writing to a newly bound account.
    /// </summary>
    SolarWinDbContext CreateDbContext(Guid accountId);

    /// <summary>Run migrations for the bound account (awaits readiness on success).</summary>
    Task EnsureMigratedAsync(CancellationToken cancellationToken = default);

    /// <summary>Wait until the bound account has finished MigrateAsync (or failed).</summary>
    Task WaitUntilReadyAsync(CancellationToken cancellationToken = default);
}
