using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TimTicker.Application.Abstractions;
using TimTicker.Infrastructure.Persistence;

namespace TimTicker.Infrastructure.Tests;

public sealed class DatabaseInitializationTests
{
    [Fact]
    public async Task InitializeAsyncCreatesDatabaseAndIsIdempotent()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "Data", "timticker.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        try
        {
            ServiceCollection services = new();
            services.AddLogging();
            services.AddTimTickerInfrastructure(databasePath);

            await using (ServiceProvider serviceProvider = services.BuildServiceProvider())
            {
                IAppDatabaseInitializer initializer =
                    serviceProvider.GetRequiredService<IAppDatabaseInitializer>();

                await initializer.InitializeAsync();
                await initializer.InitializeAsync();

                IDbContextFactory<TimTickerDbContext> contextFactory =
                    serviceProvider.GetRequiredService<IDbContextFactory<TimTickerDbContext>>();
                await using (TimTickerDbContext context = await contextFactory.CreateDbContextAsync())
                {
                    string[] appliedMigrations =
                        (await context.Database.GetAppliedMigrationsAsync()).ToArray();
                    string[] pendingMigrations =
                        (await context.Database.GetPendingMigrationsAsync()).ToArray();

                    Assert.True(File.Exists(databasePath));
                    Assert.Equal(3, appliedMigrations.Length);
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_TaskPool", StringComparison.Ordinal));
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_TaskDates", StringComparison.Ordinal));
                    Assert.Empty(pendingMigrations);
                }
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
