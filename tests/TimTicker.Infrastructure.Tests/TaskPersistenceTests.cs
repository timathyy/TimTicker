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
                Assert.Equal(TaskPriority.High, task.Priority);

                await tasks.CompleteAsync(task.Id);
                IReadOnlyList<TaskSummary> completed = await tasks.ListAsync(new TaskQuery(
                    DomainTaskStatus.Completed,
                    null,
                    TaskSortOrder.CreatedAtDescending));
                Assert.Equal(taskId, Assert.Single(completed).Id);
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
