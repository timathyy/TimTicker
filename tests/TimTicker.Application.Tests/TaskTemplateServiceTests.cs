using TimTicker.Application.Settings;
using TimTicker.Application.Tasks;
using TimTicker.Application.Templates;
using TimTicker.Domain.Tasks;
using TimTicker.Domain.Templates;

namespace TimTicker.Application.Tests;

public sealed class TaskTemplateServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAndManualGenerateCreatesDedicatedFolderAndFormattedTask()
    {
        InMemoryTemplateRepository repository = new();
        TaskTemplateService service = CreateService(repository);
        TaskTemplateSummary created = await service.CreateAsync(new CreateTaskTemplateRequest(
            "每日复盘",
            "复盘-%y%m%d-%n",
            "记录一天",
            TaskPriority.High,
            TaskTemplateSchedule.Manual,
            1,
            new DateOnly(2026, 11, 4),
            false));

        TemplateGenerationResult result = await service.GenerateManualAsync(created.Id);

        TaskItem task = Assert.Single(repository.Tasks);
        Assert.Equal("复盘-20261104-1", task.Title);
        Assert.Equal(created.FolderId, task.FolderId);
        Assert.Equal(created.Id, task.TemplateId);
        Assert.Single(result.TaskIds);
        Assert.Equal(TaskFolderKind.Template, Assert.Single(repository.Folders).Kind);
    }

    [Fact]
    public async Task AutomaticGenerationCatchesUpOnCreateAndRemainsIdempotent()
    {
        InMemoryTemplateRepository repository = new();
        TaskTemplateService service = CreateService(repository);
        await service.CreateAsync(new CreateTaskTemplateRequest(
            "晨间计划",
            "计划-%m%d-%n",
            null,
            TaskPriority.None,
            TaskTemplateSchedule.Daily,
            1,
            new DateOnly(2026, 11, 2),
            true));

        TemplateGenerationResult first = await service.GenerateDueAsync();
        TemplateGenerationResult second = await service.GenerateDueAsync();

        Assert.Equal(0, first.GeneratedCount);
        Assert.Equal(0, second.GeneratedCount);
        Assert.Equal(3, repository.Tasks.Count);
        Assert.Equal(
            new[] { new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 4) },
            repository.Tasks.Select(task => task.TemplateOccurrenceDate!.Value));
    }

    [Fact]
    public async Task DuplicateTemplateNameIsRejected()
    {
        InMemoryTemplateRepository repository = new();
        TaskTemplateService service = CreateService(repository);
        CreateTaskTemplateRequest request = new(
            "周报",
            "周报-%n",
            null,
            TaskPriority.None,
            TaskTemplateSchedule.Manual,
            1,
            new DateOnly(2026, 11, 4),
            false);
        await service.CreateAsync(request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request));
    }

    [Fact]
    public async Task TemplateFolderIsCreatedInsideSelectedParent()
    {
        InMemoryTemplateRepository repository = new();
        Guid parentId = Guid.NewGuid();
        repository.Folders.Add(new TaskFolder(
            parentId,
            "项目",
            TaskFolderKind.User,
            Now));
        TaskTemplateService service = CreateService(repository);

        TaskTemplateSummary created = await service.CreateAsync(new CreateTaskTemplateRequest(
            "每日构建",
            "构建-%n",
            null,
            TaskPriority.None,
            TaskTemplateSchedule.Manual,
            1,
            new DateOnly(2026, 11, 4),
            false,
            parentId));

        TaskFolder createdFolder = Assert.Single(repository.Folders, folder => folder.Id == created.FolderId);
        Assert.Equal(parentId, createdFolder.ParentId);
    }

    private static TaskTemplateService CreateService(InMemoryTemplateRepository repository) =>
        new(repository, new TestDayBoundarySettings(DayEndTime.Midnight), new FixedTimeProvider(Now));

    private sealed class InMemoryTemplateRepository : ITaskTemplateRepository
    {
        public List<TaskTemplate> Templates { get; } = [];

        public List<TaskFolder> Folders { get; } = [];

        public List<TaskItem> Tasks { get; } = [];

        public Task AddAsync(TaskTemplate taskTemplate, TaskFolder folder, CancellationToken cancellationToken = default)
        {
            Templates.Add(taskTemplate);
            Folders.Add(folder);
            return Task.CompletedTask;
        }

        public Task<TaskTemplate?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Templates.SingleOrDefault(item => item.Id == id));

        public Task<IReadOnlyList<TaskTemplate>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TaskTemplate>>(Templates.ToArray());

        public Task SaveGenerationAsync(
            TaskTemplate taskTemplate,
            IReadOnlyCollection<TaskItem> tasks,
            CancellationToken cancellationToken = default)
        {
            Tasks.AddRange(tasks);
            return Task.CompletedTask;
        }

        public Task<bool> NameExistsAsync(Guid? parentFolderId, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(Folders.Any(folder =>
                folder.ParentId == parentFolderId &&
                string.Equals(folder.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> FolderExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Folders.Any(folder => folder.Id == id && !folder.IsDeleted));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
