using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TimTicker.Application;
using TimTicker.Application.Abstractions;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;
using TimTicker.Infrastructure;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.Infrastructure.Tests;

public sealed class TaskPersistenceTests
{
    [Fact]
    public async Task RepositorySupportsAscendingAndDescendingTaskSortOrders()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "timticker.db");
        Directory.CreateDirectory(testRoot);

        Guid oldestId = Guid.NewGuid();
        Guid middleId = Guid.NewGuid();
        Guid newestId = Guid.NewGuid();

        try
        {
            await using ServiceProvider provider = CreateProvider(databasePath);
            await provider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
            ITaskItemRepository repository = provider.GetRequiredService<ITaskItemRepository>();

            await repository.AddAsync(new TaskItem(
                oldestId,
                "Oldest",
                null,
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 11, 3),
                TaskPriority.High,
                new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)));
            await repository.AddAsync(new TaskItem(
                middleId,
                "Middle",
                null,
                new DateOnly(2026, 10, 2),
                null,
                TaskPriority.Medium,
                new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero)));
            await repository.AddAsync(new TaskItem(
                newestId,
                "Newest",
                null,
                new DateOnly(2026, 10, 3),
                new DateOnly(2026, 11, 1),
                TaskPriority.Low,
                new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero)));

            IReadOnlyList<TaskItem> createdAscending = await repository.ListAsync(
                new TaskQuery(null, null, TaskSortOrder.CreatedAtAscending));
            IReadOnlyList<TaskItem> dueDescending = await repository.ListAsync(
                new TaskQuery(null, null, TaskSortOrder.DueDateDescending));
            IReadOnlyList<TaskItem> priorityAscending = await repository.ListAsync(
                new TaskQuery(null, null, TaskSortOrder.PriorityAscending));

            Assert.Equal(new[] { oldestId, middleId, newestId }, createdAscending.Select(task => task.Id));
            Assert.Equal(new[] { oldestId, newestId, middleId }, dueDescending.Select(task => task.Id));
            Assert.Equal(new[] { newestId, middleId, oldestId }, priorityAscending.Select(task => task.Id));
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
    public async Task CreatedTaskSurvivesNewServiceProviderAndCanBeUpdated()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "timticker.db");
        Directory.CreateDirectory(testRoot);

        try
        {
            Guid taskId;
            await using (ServiceProvider firstProvider = CreateProvider(databasePath))
            {
                await firstProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
                ITaskService tasks = firstProvider.GetRequiredService<ITaskService>();
                TaskSummary created = await tasks.CreateAsync(new CreateTaskRequest(
                    "Persist me",
                    "SQLite integration test",
                    new DateOnly(2026, 10, 29),
                    new DateOnly(2026, 11, 1),
                    TaskPriority.High));
                taskId = created.Id;
            }

            await using (ServiceProvider secondProvider = CreateProvider(databasePath))
            {
                await secondProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
                ITaskService tasks = secondProvider.GetRequiredService<ITaskService>();
                IReadOnlyList<TaskSummary> pending = await tasks.ListAsync(new TaskQuery(
                    DomainTaskStatus.Pending,
                    "persist",
                    TaskSortOrder.CreatedAtDescending));

                TaskSummary task = Assert.Single(pending);
                Assert.Equal(taskId, task.Id);
                Assert.Equal(new DateOnly(2026, 11, 1), task.DueDate);
                Assert.Equal(new DateOnly(2026, 10, 29), task.CreatedDate);
                Assert.Equal(TaskPriority.High, task.Priority);

                await tasks.CompleteAsync(task.Id);
                IReadOnlyList<TaskSummary> completed = await tasks.ListAsync(new TaskQuery(
                    DomainTaskStatus.Completed,
                    null,
                    TaskSortOrder.CreatedAtDescending));
                Assert.Equal(taskId, Assert.Single(completed).Id);
                Assert.Equal(DateOnly.FromDateTime(DateTime.Today), Assert.Single(completed).CompletedDate);

                IReadOnlyList<TaskSummary> allTasks = await tasks.ListAsync(new TaskQuery(
                    null,
                    null,
                    TaskSortOrder.CreatedAtDescending));
                Assert.Equal(taskId, Assert.Single(allTasks).Id);
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

    private static ServiceProvider CreateProvider(string databasePath)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddTimTickerApplication();
        services.AddTimTickerInfrastructure(databasePath);
        return services.BuildServiceProvider();
    }
}
