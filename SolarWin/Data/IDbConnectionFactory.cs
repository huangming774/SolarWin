using Microsoft.Data.Sqlite;

namespace SolarWin.Data;

/// <summary>Creates short-lived, read-only connections for local analytics queries.</summary>
public interface IDbConnectionFactory
{
    Task<SqliteConnection> OpenReadOnlyConnectionAsync(CancellationToken cancellationToken = default);
}
