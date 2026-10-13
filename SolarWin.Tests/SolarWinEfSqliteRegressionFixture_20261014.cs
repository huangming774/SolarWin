using Microsoft.EntityFrameworkCore;
using SolarWin.Data;

namespace SolarWin.Tests;

/// <summary>
/// Real EF Core SQLite fixture for the local chat store tests. Every account gets its own
/// temporary file; this fixture never uses AppPaths or the production database directory.
/// </summary>
internal sealed class SolarWinEfSqliteRegressionFixture : IAccountDbContextFactory, IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "solarwin-ef-regression-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<Guid, string> _paths = [];
    private Guid? _boundAccountId;
    private TaskCompletionSource _ready = CompletedReady();
    private TaskCompletionSource? _readyEntered;

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
                return _boundAccountId is { } id ? PathFor_NoLock(id) : null;
            }
        }
    }

    public async Task PrepareAsync(Guid accountId)
    {
        SQLitePCL.Batteries_V2.Init();
        Bind(accountId);
        await using var db = CreateDbContext(accountId);
        await db.Database.EnsureCreatedAsync();
    }

    public void BlockReady(TaskCompletionSource entered)
    {
        lock (_gate)
        {
            _readyEntered = entered;
            _ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Bind(Guid accountId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(accountId, Guid.Empty);
        lock (_gate)
        {
            CompleteSuperseded_NoLock();
            _boundAccountId = accountId;
            _ready = CompletedReady();
        }
    }

    public void Switch(Guid accountId) => Bind(accountId);

    public void Remove(Guid accountId)
    {
        lock (_gate)
        {
            if (_boundAccountId == accountId)
            {
                CompleteSuperseded_NoLock();
                _boundAccountId = null;
                _ready = FaultedReady();
            }
        }

        if (_paths.TryGetValue(accountId, out var path))
        {
            TryDelete(path);
        }
    }

    public void ResetBoundDatabase()
    {
        Guid accountId;
        lock (_gate)
        {
            accountId = _boundAccountId
                ?? throw new InvalidOperationException("No test account is bound.");
        }

        Remove(accountId);
        Bind(accountId);
    }

    public SolarWinDbContext CreateDbContext()
    {
        lock (_gate)
        {
            return CreateDbContext_NoLock(
                _boundAccountId
                ?? throw new InvalidOperationException("No test account is bound."));
        }
    }

    public SolarWinDbContext CreateDbContext(Guid accountId)
    {
        lock (_gate)
        {
            if (_boundAccountId != accountId)
            {
                throw new InvalidOperationException("The requested test account is no longer bound.");
            }

            return CreateDbContext_NoLock(accountId);
        }
    }

    public Task EnsureMigratedAsync(CancellationToken cancellationToken = default)
        => WaitUntilReadyAsync(cancellationToken);

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken = default)
    {
        Task ready;
        lock (_gate)
        {
            _readyEntered?.TrySetResult();
            ready = _ready.Task;
        }

        return cancellationToken.CanBeCanceled
            ? ready.WaitAsync(cancellationToken)
            : ready;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            CompleteSuperseded_NoLock();
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        await Task.Yield();
        TryDelete(_root);
    }

    private SolarWinDbContext CreateDbContext_NoLock(Guid accountId)
    {
        Directory.CreateDirectory(_root);
        var path = PathFor_NoLock(accountId);
        var options = new DbContextOptionsBuilder<SolarWinDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False")
            .Options;
        return new SolarWinDbContext(options);
    }

    private string PathFor_NoLock(Guid accountId)
    {
        if (!_paths.TryGetValue(accountId, out var path))
        {
            path = Path.Combine(_root, accountId.ToString("N") + ".db");
            _paths[accountId] = path;
        }

        return path;
    }

    private void CompleteSuperseded_NoLock()
    {
        if (!_ready.Task.IsCompleted)
        {
            _ready.TrySetException(new InvalidOperationException("Test account binding was superseded."));
        }

        _readyEntered = null;
    }

    private static TaskCompletionSource CompletedReady()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.TrySetResult();
        return source;
    }

    private static TaskCompletionSource FaultedReady()
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.TrySetException(new InvalidOperationException("No test account is bound."));
        return source;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Test cleanup is best effort after SQLite has released the file.
        }
    }
}
