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
                    Assert.Equal(12, appliedMigrations.Length);
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
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_TaskFoldersAndTemplates", StringComparison.Ordinal));
                    Assert.Contains(appliedMigrations, migration => migration.EndsWith("_HierarchicalTaskFolders", StringComparison.Ordinal));
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
                await oldContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO Tasks
                        (Id, Title, Description, DueDate, Priority, Status, CreatedAtUtc, UpdatedAtUtc,
                         CompletedAtUtc, IsDeleted, CompletedDate, CreatedDate)
                    VALUES
                        ({taskId}, {"Existing linked task"}, {null}, {null}, {(int)TaskPriority.None}, {0},
                         {startTicks}, {startTicks}, {null}, {false}, {null}, {new DateOnly(2026, 11, 2)})
                    """);
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
    public async Task InitializeAsyncUpgradesPreviouslyAppliedFlatFolderMigration()
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
            Guid folderId = Guid.NewGuid();
            long createdAtTicks = new DateTimeOffset(2026, 11, 4, 8, 0, 0, TimeSpan.Zero).UtcTicks;
            await using (TimTickerDbContext oldContext = await contextFactory.CreateDbContextAsync())
            {
                await oldContext.Database.MigrateAsync("20261104045326_TaskFoldersAndTemplates");
                await oldContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO TaskFolders
                        (Id, Name, Kind, CreatedAtUtc, UpdatedAtUtc, IsDeleted)
                    VALUES
                        ({folderId}, {"旧版文件夹"}, {(int)TaskFolderKind.User}, {createdAtTicks}, {createdAtTicks}, {false})
                    """);
            }

            await serviceProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();

            await using TimTickerDbContext upgradedContext = await contextFactory.CreateDbContextAsync();
            TaskFolder folder = Assert.Single(await upgradedContext.TaskFolders.AsNoTracking().ToListAsync());
            Assert.Equal(folderId, folder.Id);
            Assert.Null(folder.ParentId);
            string[] appliedMigrations = (await upgradedContext.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.Contains("20261104045326_TaskFoldersAndTemplates", appliedMigrations);
            Assert.Contains(appliedMigrations, migration => migration.EndsWith("_HierarchicalTaskFolders", StringComparison.Ordinal));
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
            long createdAtTicks = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero).UtcTicks;
            await using (TimTickerDbContext oldContext = await contextFactory.CreateDbContextAsync())
            {
                await oldContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO Tasks
                        (Id, Title, Description, DueDate, Priority, Status, CreatedAtUtc, UpdatedAtUtc,
                         CompletedAtUtc, IsDeleted, CompletedDate, CreatedDate)
                    VALUES
                        ({taskId}, {"Keep during upgrade"}, {null}, {null}, {(int)TaskPriority.None}, {0},
                         {createdAtTicks}, {createdAtTicks}, {null}, {false}, {null}, {new DateOnly(2026, 11, 1)})
                    """);
            }

            await serviceProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();

            TaskItem upgradedTask = Assert.IsType<TaskItem>(
                await serviceProvider.GetRequiredService<ITaskItemRepository>().FindAsync(taskId));
            Assert.Null(upgradedTask.FolderId);
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
