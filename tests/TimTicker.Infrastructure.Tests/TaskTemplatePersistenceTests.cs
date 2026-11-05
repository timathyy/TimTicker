using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TimTicker.Application;
using TimTicker.Application.Abstractions;
using TimTicker.Application.Tasks;
using TimTicker.Application.Templates;
using TimTicker.Domain.Tasks;
using TimTicker.Domain.Templates;
using TimTicker.Infrastructure;

namespace TimTicker.Infrastructure.Tests;

public sealed class TaskTemplatePersistenceTests
{
    [Fact]
    public async Task FoldersTemplatesAndGeneratedTasksSurviveRestart()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "timticker.db");
        Directory.CreateDirectory(testRoot);

        try
        {
            Guid userFolderId;
            Guid templateFolderId;
            await using (ServiceProvider firstProvider = CreateProvider(databasePath))
            {
                await firstProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
                ITaskFolderService folders = firstProvider.GetRequiredService<ITaskFolderService>();
                ITaskService tasks = firstProvider.GetRequiredService<ITaskService>();
                ITaskTemplateService templates = firstProvider.GetRequiredService<ITaskTemplateService>();

                TaskFolderSummary projectsFolder = await folders.CreateAsync("项目");
                TaskFolderSummary userFolder = await folders.CreateAsync("工作", projectsFolder.Id);
                userFolderId = userFolder.Id;
                await tasks.CreateAsync(new CreateTaskRequest(
                    "整理需求",
                    null,
                    new DateOnly(2026, 11, 4),
                    null,
                    TaskPriority.Medium,
                    userFolder.Id));
                TaskTemplateSummary taskTemplate = await templates.CreateAsync(new CreateTaskTemplateRequest(
                    "每日记录",
                    "记录-%y%m%d-%n",
                    null,
                    TaskPriority.None,
                    TaskTemplateSchedule.Manual,
                    1,
                    new DateOnly(2026, 11, 4),
                    false,
                    userFolder.Id));
                templateFolderId = taskTemplate.FolderId;
                await templates.GenerateManualAsync(taskTemplate.Id);
                await folders.CreateAsync("归档", taskTemplate.FolderId);
                await tasks.CreateAsync(new CreateTaskRequest(
                    "手工放入自动文件夹",
                    null,
                    new DateOnly(2026, 11, 4),
                    null,
                    TaskPriority.None,
                    taskTemplate.FolderId));
            }

            await using (ServiceProvider secondProvider = CreateProvider(databasePath))
            {
                await secondProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
                ITaskFolderService folders = secondProvider.GetRequiredService<ITaskFolderService>();
                ITaskService tasks = secondProvider.GetRequiredService<ITaskService>();
                ITaskTemplateService templates = secondProvider.GetRequiredService<ITaskTemplateService>();

                IReadOnlyList<TaskFolderSummary> storedFolders = await folders.ListAsync();
                Assert.Equal(4, storedFolders.Count);
                Assert.Contains(storedFolders, folder => folder.Id == userFolderId && folder.Kind == TaskFolderKind.User);
                Assert.Contains(storedFolders, folder =>
                    folder.Id == templateFolderId &&
                    folder.ParentId == userFolderId &&
                    folder.Kind == TaskFolderKind.Template);
                Assert.Single(await templates.ListAsync());

                TaskSummary ordinary = Assert.Single(await tasks.ListAsync(new TaskQuery(
                    null,
                    null,
                    TaskSortOrder.CreatedAtDescending,
                    userFolderId)));
                IReadOnlyList<TaskSummary> templateFolderTasks = await tasks.ListAsync(new TaskQuery(
                    null,
                    null,
                    TaskSortOrder.CreatedAtDescending,
                    templateFolderId));
                TaskSummary generated = Assert.Single(templateFolderTasks, task => task.TemplateId.HasValue);
                TaskSummary manuallyFiled = Assert.Single(templateFolderTasks, task => !task.TemplateId.HasValue);
                Assert.Null(ordinary.TemplateId);
                Assert.NotNull(generated.TemplateId);
                Assert.Equal(1, generated.TemplateSequenceNumber);
                Assert.Equal("手工放入自动文件夹", manuallyFiled.Title);
                Assert.Contains(storedFolders, folder => folder.ParentId == templateFolderId && folder.Name == "归档");
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
    public async Task FolderNameUniquenessIsCaseInsensitive()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "timticker.db");
        Directory.CreateDirectory(testRoot);

        try
        {
            await using ServiceProvider provider = CreateProvider(databasePath);
            await provider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
            ITaskFolderService folders = provider.GetRequiredService<ITaskFolderService>();
            await folders.CreateAsync("Work");

            await Assert.ThrowsAsync<InvalidOperationException>(() => folders.CreateAsync("work"));
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
    public async Task SameFolderNameIsAllowedUnderDifferentParents()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "timticker.db");
        Directory.CreateDirectory(testRoot);

        try
        {
            await using ServiceProvider provider = CreateProvider(databasePath);
            await provider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
            ITaskFolderService folders = provider.GetRequiredService<ITaskFolderService>();
            TaskFolderSummary firstParent = await folders.CreateAsync("甲");
            TaskFolderSummary secondParent = await folders.CreateAsync("乙");

            TaskFolderSummary firstChild = await folders.CreateAsync("资料", firstParent.Id);
            TaskFolderSummary secondChild = await folders.CreateAsync("资料", secondParent.Id);

            Assert.Equal(firstParent.Id, firstChild.ParentId);
            Assert.Equal(secondParent.Id, secondChild.ParentId);
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
