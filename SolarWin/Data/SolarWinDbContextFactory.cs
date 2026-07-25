using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SolarWin.Helpers;

namespace SolarWin.Data;

/// <summary>
/// Design-time factory for <c>dotnet ef migrations add</c>.
/// </summary>
public sealed class SolarWinDbContextFactory : IDesignTimeDbContextFactory<SolarWinDbContext>
{
    public SolarWinDbContext CreateDbContext(string[] args)
    {
        AppPaths.EnsureDirectories();
        var path = Path.Combine(AppPaths.DbDirectory, "_design_time.db");
        var options = new DbContextOptionsBuilder<SolarWinDbContext>()
            .UseSqlite($"Data Source={path};Cache=Shared")
            .AddInterceptors(new SqliteWalConnectionInterceptor())
            .Options;
        return new SolarWinDbContext(options);
    }
}
