using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimTicker.Application.Abstractions;
using TimTicker.Infrastructure.Persistence;

namespace TimTicker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTimTickerInfrastructure(
        this IServiceCollection services,
        string databasePath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        string fullDatabasePath = Path.GetFullPath(databasePath);
        SqliteConnectionStringBuilder connectionString = new()
        {
            DataSource = fullDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
        };

        services.AddDbContextFactory<TimTickerDbContext>(options =>
            options.UseSqlite(connectionString.ConnectionString));
        services.AddSingleton<IAppDatabaseInitializer, AppDatabaseInitializer>();

        return services;
    }
}
