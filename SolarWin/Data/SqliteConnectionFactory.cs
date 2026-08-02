using Microsoft.Data.Sqlite;

namespace SolarWin.Data;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly IAccountDbContextFactory _accountFactory;

    public SqliteConnectionFactory(IAccountDbContextFactory accountFactory)
    {
        _accountFactory = accountFactory;
    }

    public async Task<SqliteConnection> OpenReadOnlyConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        if (_accountFactory.BoundAccountId is null)
        {
            throw new InvalidOperationException("尚未绑定登录账号的本地聊天数据库。");
        }

        await _accountFactory.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);

        var path = _accountFactory.BoundDatabasePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException("当前账号的本地聊天数据库不存在，请先打开聊天并完成同步。", path);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = true,
            DefaultTimeout = 5,
        };

        var connection = new SqliteConnection(builder.ToString());
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA busy_timeout=5000;";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
