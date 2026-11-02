using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TimTicker.Application.Abstractions;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;
using TimTicker.Domain.Timeline;
using TimTicker.Infrastructure;
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
                    Assert.Equal(10, appliedMigrations.Length);
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_TaskPool", StringComparison.Ordinal));
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_TaskDates", StringComparison.Ordinal));
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_DailyPlanning", StringComparison.Ordinal));
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_TimelineEntries", StringComparison.Ordinal));
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_TimelineEntrySource", StringComparison.Ordinal));
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_TimelineEntryMultipleTasks", StringComparison.Ordinal));
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_TimelineEntryRecordDate", StringComparison.Ordinal));
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_DailyPlanCompletionSnapshot", StringComparison.Ordinal));
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_RemoveTimelineEntryCategory", StringComparison.Ordinal));
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

    [Fact]
    public async Task InitializeAsyncMigratesExistingTimelineTaskAssociation()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "Data", "timticker.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        try
        {
            ServiceCollection services = new();
            services.AddLogging();
            services.AddTimTickerInfrastructure(databasePath);

            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IDbContextFactory<TimTickerDbContext> contextFactory =
                serviceProvider.GetRequiredService<IDbContextFactory<TimTickerDbContext>>();
            Guid entryId = Guid.NewGuid();
            Guid taskId = Guid.NewGuid();
            long startTicks = new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero).UtcTicks;
            long endTicks = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero).UtcTicks;
            await using (TimTickerDbContext oldContext = await contextFactory.CreateDbContextAsync())
            {
                await oldContext.Database.MigrateAsync("20261102114842_TimelineEntries");
                TaskItem task = new(
                    taskId,
                    "Existing linked task",
                    null,
                    new DateOnly(2026, 11, 2),
                    null,
                    TaskPriority.None,
                    new DateTimeOffset(startTicks, TimeSpan.Zero));
                oldContext.Tasks.Add(task);
                await oldContext.SaveChangesAsync();
                await oldContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO TimelineEntries
                        (Id, Title, Description, Category, TaskItemId, StartAtUtc, EndAtUtc, CreatedAtUtc, UpdatedAtUtc, IsDeleted)
                    VALUES
                        ({entryId}, {"Existing manual record"}, {null}, {null}, {taskId}, {startTicks}, {endTicks}, {startTicks}, {startTicks}, {false})
                    """);
            }

            await serviceProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();

            await using TimTickerDbContext upgradedContext = await contextFactory.CreateDbContextAsync();
            TimelineEntry entry = Assert.Single(await upgradedContext.TimelineEntries.Include(entry => entry.TaskLinks).ToListAsync());
            Assert.Equal(entryId, entry.Id);
            Assert.Equal(TimelineEntrySource.Manual, entry.Source);
            Assert.Equal(taskId, Assert.Single(entry.ActiveTaskLinks).TaskItemId);
            Assert.Empty(await upgradedContext.Database.GetPendingMigrationsAsync());
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

    [Fact]
    public async Task InitializeAsyncUpgradesTaskPoolDatabaseWithoutLosingTasks()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "Data", "timticker.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        try
        {
            ServiceCollection services = new();
            services.AddLogging();
            services.AddTimTickerInfrastructure(databasePath);

            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IDbContextFactory<TimTickerDbContext> contextFactory =
                serviceProvider.GetRequiredService<IDbContextFactory<TimTickerDbContext>>();
            await using (TimTickerDbContext oldContext = await contextFactory.CreateDbContextAsync())
            {
                await oldContext.Database.MigrateAsync("20261030085221_TaskDates");
            }

            Guid taskId = Guid.NewGuid();
            await serviceProvider.GetRequiredService<ITaskItemRepository>().AddAsync(new TaskItem(
                taskId,
                "Keep during upgrade",
                null,
                new DateOnly(2026, 11, 1),
                null,
                TaskPriority.None,
                new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero)));

            await serviceProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();

            Assert.NotNull(await serviceProvider.GetRequiredService<ITaskItemRepository>().FindAsync(taskId));
            await using TimTickerDbContext upgradedContext = await contextFactory.CreateDbContextAsync();
            Assert.Empty(await upgradedContext.DailyPlans.ToListAsync());
            Assert.Empty(await upgradedContext.TimelineEntries.ToListAsync());
            Assert.Empty(await upgradedContext.Database.GetPendingMigrationsAsync());
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
