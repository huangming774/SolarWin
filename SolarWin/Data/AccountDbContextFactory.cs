using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SolarWin.Helpers;

namespace SolarWin.Data;

/// <summary>
/// Builds per-account SQLite options, migrates on Bind/Switch, opens with WAL (design v1.0).
/// </summary>
public sealed class AccountDbContextFactory : IAccountDbContextFactory
{
    private readonly object _gate = new();
    private readonly SqliteWalConnectionInterceptor _walInterceptor = new();

    private Guid? _boundAccountId;
    private int _bindGeneration;
    private TaskCompletionSource _ready = CreateReadyTcs(alreadyFailed: true);

    public Guid? BoundAccountId
    {
        get
        {
            lock (_gate)
            {
                return _boundAccountId;
            }
        }
    }

    public string? BoundDatabasePath
    {
        get
        {
            lock (_gate)
            {
                return _boundAccountId is { } id ? GetDatabasePath(id) : null;
            }
        }
    }

    public void Bind(Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("accountId must be non-empty.", nameof(accountId));
        }

        AppPaths.EnsureDirectories();

        int generation;
        lock (_gate)
        {
            // Same account already ready or migrate in flight — do not reset the gate.
            if (_boundAccountId == accountId)
            {
                if (_ready.Task.IsCompletedSuccessfully
                    || (!_ready.Task.IsCompleted && !_ready.Task.IsFaulted && !_ready.Task.IsCanceled))
                {
                    return;
                }
                // Faulted/canceled: fall through and retry migrate.
            }

            _boundAccountId = accountId;
            generation = ++_bindGeneration;
            _ready = CreateReadyTcs(alreadyFailed: false);
        }

        // Background migrate — login path must not block UI (design v1.0 Phase 1).
        _ = EnsureMigratedCoreAsync(accountId, generation, CancellationToken.None);
    }

    public void Switch(Guid accountId)
    {
        // Always re-open migrate gate for the target account (even if same id after Remove retry).
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("accountId must be non-empty.", nameof(accountId));
        }

        AppPaths.EnsureDirectories();

        int generation;
        lock (_gate)
        {
            // Switching to the same account that is already ready/in-flight: no-op.
            if (_boundAccountId == accountId)
            {
                if (_ready.Task.IsCompletedSuccessfully
                    || (!_ready.Task.IsCompleted && !_ready.Task.IsFaulted && !_ready.Task.IsCanceled))
                {
                    return;
                }
            }

            _boundAccountId = accountId;
            generation = ++_bindGeneration;
            _ready = CreateReadyTcs(alreadyFailed: false);
        }

        _ = EnsureMigratedCoreAsync(accountId, generation, CancellationToken.None);
    }

    public void Remove(Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            return;
        }

        lock (_gate)
        {
            if (_boundAccountId == accountId)
            {
                _boundAccountId = null;
                _bindGeneration++;
                _ready = CreateReadyTcs(alreadyFailed: true);
            }
        }

        DeleteDatabaseFiles(accountId);
    }

    public void ResetBoundDatabase()
    {
        Guid accountId;
        lock (_gate)
        {
            if (_boundAccountId is not { } id || id == Guid.Empty)
            {
                throw new InvalidOperationException("No account is bound to the local database factory.");
            }

            accountId = id;
            _boundAccountId = null;
            _bindGeneration++;
            _ready = CreateReadyTcs(alreadyFailed: true);
        }

        DeleteDatabaseFiles(accountId);
        // Recreate empty schema for the same account (background migrate).
        Bind(accountId);
    }

    public SolarWinDbContext CreateDbContext()
    {
        Guid accountId;
        lock (_gate)
        {
            if (_boundAccountId is not { } id || id == Guid.Empty)
            {
                throw new InvalidOperationException("No account is bound to the local database factory.");
            }

            accountId = id;
        }

        return new SolarWinDbContext(BuildOptions(accountId));
    }

    public async Task EnsureMigratedAsync(CancellationToken cancellationToken = default)
    {
        Guid accountId;
        int generation;
        lock (_gate)
        {
            if (_boundAccountId is not { } id || id == Guid.Empty)
            {
                throw new InvalidOperationException("No account is bound to the local database factory.");
            }

            accountId = id;
            generation = _bindGeneration;
        }

        await EnsureMigratedCoreAsync(accountId, generation, cancellationToken).ConfigureAwait(false);
        // Propagate migrate success/failure via the ready gate.
        await WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken = default)
    {
        Task ready;
        lock (_gate)
        {
            ready = _ready.Task;
        }

        if (cancellationToken.CanBeCanceled)
        {
            return ready.WaitAsync(cancellationToken);
        }

        return ready;
    }

    private async Task EnsureMigratedCoreAsync(
        Guid accountId,
        int generation,
        CancellationToken cancellationToken)
    {
        try
        {
            AppPaths.EnsureDirectories();
            var path = GetDatabasePath(accountId);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await using (var db = new SolarWinDbContext(BuildOptions(accountId)))
            {
                await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

                // Force a connection so WAL pragmas run and -wal appears after first use.
                await db.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await using var cmd = db.Database.GetDbConnection().CreateCommand();
                    cmd.CommandText = "PRAGMA journal_mode;";
                    var mode = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(Convert.ToString(mode), "wal", StringComparison.OrdinalIgnoreCase))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[SolarWin.Data] WAL not active for {path}; journal_mode={mode}");
                    }
                }
                finally
                {
                    await db.Database.CloseConnectionAsync().ConfigureAwait(false);
                }
            }

            lock (_gate)
            {
                if (generation == _bindGeneration && _boundAccountId == accountId)
                {
                    _ready.TrySetResult();
                }
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                if (generation == _bindGeneration && _boundAccountId == accountId)
                {
                    _ready.TrySetException(ex);
                }
            }

            System.Diagnostics.Debug.WriteLine($"[SolarWin.Data] Migrate failed for {accountId:N}: {ex}");
        }
    }

    private DbContextOptions<SolarWinDbContext> BuildOptions(Guid accountId)
    {
        var path = GetDatabasePath(accountId);
        // WAL already provides concurrent readers. Microsoft.Data.Sqlite explicitly
        // discourages combining WAL with SQLite's legacy shared-cache mode.
        var connectionString = $"Data Source={path};Pooling=True;Default Timeout=5";

        var builder = new DbContextOptionsBuilder<SolarWinDbContext>();
        builder.UseSqlite(connectionString);
        builder.AddInterceptors(_walInterceptor);
        return builder.Options;
    }

    public static string GetDatabasePath(Guid accountId)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("accountId must be non-empty.", nameof(accountId));
        }

        return Path.Combine(AppPaths.DbDirectory, $"{accountId:N}.db");
    }

    private static void DeleteDatabaseFiles(Guid accountId)
    {
        try
        {
            // Release pooled connections so the file is not locked (Windows).
            SqliteConnection.ClearAllPools();

            var path = GetDatabasePath(accountId);
            TryDelete(path);
            TryDelete(path + "-wal");
            TryDelete(path + "-shm");
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore locked files
        }
    }

    private static TaskCompletionSource CreateReadyTcs(bool alreadyFailed)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (alreadyFailed)
        {
            tcs.TrySetException(
                new InvalidOperationException("Local database is not bound or not ready."));
        }

        return tcs;
    }
}
