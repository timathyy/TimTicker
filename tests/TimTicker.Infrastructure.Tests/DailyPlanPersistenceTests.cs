using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimTicker.Application;
using TimTicker.Application.Abstractions;
using TimTicker.Application.Planning;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;
using TimTicker.Infrastructure;
using TimTicker.Infrastructure.Persistence;

namespace TimTicker.Infrastructure.Tests;

public sealed class DailyPlanPersistenceTests
{
    [Fact]
    public async Task PlanOrderAndRemovalSurviveNewServiceProvider()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "timticker.db");
        Directory.CreateDirectory(testRoot);
        FixedTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));

        try
        {
            Guid firstTaskId;
            Guid secondTaskId;
            await using (ServiceProvider firstProvider = CreateProvider(databasePath, clock))
            {
                await firstProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
                ITaskService taskService = firstProvider.GetRequiredService<ITaskService>();
                firstTaskId = (await taskService.CreateAsync(CreateRequest("First"))).Id;
                secondTaskId = (await taskService.CreateAsync(CreateRequest("Second"))).Id;
                IDailyPlanService planService = firstProvider.GetRequiredService<IDailyPlanService>();
                await planService.AddTasksToTodayAsync([firstTaskId, secondTaskId]);
                await planService.MoveTodayTaskAsync(secondTaskId, PlanMoveDirection.Up);
                await planService.RemoveTaskFromTodayAsync(firstTaskId);
                await taskService.CompleteAsync(secondTaskId);
                await planService.CloseTodayAsync("Persisted review");
                await taskService.ReopenAsync(secondTaskId);
            }

            await using (ServiceProvider secondProvider = CreateProvider(databasePath, clock))
            {
                await secondProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
                DailyPlanSummary plan = await secondProvider.GetRequiredService<IDailyPlanService>().GetTodayAsync();

                DailyPlanItemSummary item = Assert.Single(plan.Items);
                Assert.Equal(secondTaskId, item.TaskId);
                Assert.Equal(0, item.SortOrder);
                Assert.Equal(TimTicker.Domain.Tasks.TaskStatus.Completed, item.Status);
                Assert.True(plan.IsClosed);
                Assert.Equal(1m, plan.CompletionRate);
                Assert.Equal("Persisted review", plan.ReviewText);
                IReadOnlyList<TaskSummary> pendingTasks = await secondProvider.GetRequiredService<ITaskService>().ListAsync(
                    new TaskQuery(TimTicker.Domain.Tasks.TaskStatus.Pending, null, TaskSortOrder.CreatedAtDescending));
                Assert.Contains(pendingTasks, task => task.Id == firstTaskId);
                Assert.Contains(pendingTasks, task => task.Id == secondTaskId);
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
    public async Task RepositoryReturnsSinglePlanForSameDate()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "timticker.db");
        Directory.CreateDirectory(testRoot);
        FixedTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));

        try
        {
            await using ServiceProvider provider = CreateProvider(databasePath, clock);
            await provider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
            IDailyPlanRepository repository = provider.GetRequiredService<IDailyPlanRepository>();

            TimTicker.Domain.Planning.DailyPlan first = await repository.GetOrCreateAsync(new DateOnly(2026, 11, 2), clock.GetUtcNow());
            TimTicker.Domain.Planning.DailyPlan second = await repository.GetOrCreateAsync(new DateOnly(2026, 11, 2), clock.GetUtcNow());

            Assert.Equal(first.Id, second.Id);
            IDbContextFactory<TimTickerDbContext> contextFactory = provider.GetRequiredService<IDbContextFactory<TimTickerDbContext>>();
            await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync();
            Assert.Equal(1, await context.DailyPlans.CountAsync());
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
    public async Task DeletedTaskIsPersistentlyRemovedFromOpenPlan()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "timticker.db");
        Directory.CreateDirectory(testRoot);
        FixedTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));

        try
        {
            Guid retainedTaskId;
            Guid deletedTaskId;
            await using (ServiceProvider firstProvider = CreateProvider(databasePath, clock))
            {
                await firstProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
                ITaskService tasks = firstProvider.GetRequiredService<ITaskService>();
                retainedTaskId = (await tasks.CreateAsync(CreateRequest("Retained"))).Id;
                deletedTaskId = (await tasks.CreateAsync(CreateRequest("Deleted"))).Id;
                IDailyPlanService plan = firstProvider.GetRequiredService<IDailyPlanService>();
                await plan.AddTasksToTodayAsync([retainedTaskId, deletedTaskId]);
                await tasks.CompleteAsync(retainedTaskId);
                await tasks.DeleteAsync(deletedTaskId);

                DailyPlanSummary refreshed = await plan.GetTodayAsync();

                Assert.Equal(1, refreshed.TotalCount);
                Assert.Equal(1m, refreshed.CompletionRate);
            }

            await using (ServiceProvider secondProvider = CreateProvider(databasePath, clock))
            {
                await secondProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
                DailyPlanSummary reloaded = await secondProvider.GetRequiredService<IDailyPlanService>().GetTodayAsync();

                Assert.Equal(retainedTaskId, Assert.Single(reloaded.Items).TaskId);
                Assert.Equal(1, reloaded.TotalCount);
                Assert.Equal(1, reloaded.CompletedCount);
                Assert.Equal(1m, reloaded.CompletionRate);

                IDbContextFactory<TimTickerDbContext> contextFactory =
                    secondProvider.GetRequiredService<IDbContextFactory<TimTickerDbContext>>();
                await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync();
                TimTicker.Domain.Planning.DailyPlanItem removedItem = await context.DailyPlanItems
                    .SingleAsync(item => item.TaskItemId == deletedTaskId);
                Assert.NotNull(removedItem.RemovedAtUtc);
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

    private static ServiceProvider CreateProvider(string databasePath, TimeProvider clock)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddTimTickerApplication();
        services.AddSingleton<TimeProvider>(clock);
        services.AddTimTickerInfrastructure(databasePath);
        return services.BuildServiceProvider();
    }

    private static CreateTaskRequest CreateRequest(string title) =>
        new(title, null, new DateOnly(2026, 11, 2), null, TaskPriority.None);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
