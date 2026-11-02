using TimTicker.Application.Tasks;
using TimTicker.Application.Settings;
using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.Application.Tests;

public sealed class TaskServiceTests
{
    [Fact]
    public async Task CreateAndCompletePersistsExpectedState()
    {
        InMemoryTaskRepository repository = new();
        TaskService service = CreateService(repository, new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero));

        TaskSummary created = await service.CreateAsync(CreateRequest("Review tasks", TaskPriority.Medium));
        await service.CompleteAsync(created.Id);

        TaskItem stored = Assert.Single(repository.Tasks);
        Assert.Equal(DomainTaskStatus.Completed, stored.Status);
        Assert.Equal(created.Id, stored.Id);
        Assert.Equal(new DateOnly(2026, 10, 29), stored.CompletedDate);
    }

    [Fact]
    public async Task CompletedDateCanBeEditedAndIsClearedWhenTaskReopens()
    {
        InMemoryTaskRepository repository = new();
        TaskService service = CreateService(repository, new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero));
        TaskSummary created = await service.CreateAsync(CreateRequest("Editable completion"));
        await service.CompleteAsync(created.Id);

        await service.UpdateAsync(new UpdateTaskRequest(
            created.Id,
            created.Title,
            created.Description,
            created.CreatedDate,
            created.DueDate,
            created.Priority,
            new DateOnly(2026, 10, 28)));

        TaskItem stored = Assert.Single(repository.Tasks);
        Assert.Equal(new DateOnly(2026, 10, 28), stored.CompletedDate);

        await service.ReopenAsync(created.Id);

        Assert.Equal(DomainTaskStatus.Pending, stored.Status);
        Assert.Null(stored.CompletedDate);
        Assert.Null(stored.CompletedAtUtc);
    }

    [Fact]
    public async Task ListUsesRequestedStatusAndExcludesDeletedTasks()
    {
        InMemoryTaskRepository repository = new();
        TaskService service = CreateService(repository, new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero));
        TaskSummary completed = await service.CreateAsync(CreateRequest("Done"));
        TaskSummary deleted = await service.CreateAsync(CreateRequest("Remove"));
        await service.CompleteAsync(completed.Id);
        await service.DeleteAsync(deleted.Id);

        IReadOnlyList<TaskSummary> tasks = await service.ListAsync(new TaskQuery(DomainTaskStatus.Pending, null, TaskSortOrder.CreatedAtDescending));

        Assert.Empty(tasks);
    }

    [Fact]
    public async Task ListWithNoStatusReturnsTasksFromEveryState()
    {
        InMemoryTaskRepository repository = new();
        TaskService service = CreateService(repository, new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero));
        TaskSummary pending = await service.CreateAsync(CreateRequest("Pending"));
        TaskSummary completed = await service.CreateAsync(CreateRequest("Completed"));
        await service.CompleteAsync(completed.Id);

        IReadOnlyList<TaskSummary> tasks = await service.ListAsync(new TaskQuery(null, null, TaskSortOrder.CreatedAtDescending));

        Assert.Equal(2, tasks.Count);
        Assert.Contains(tasks, task => task.Id == pending.Id);
        Assert.Contains(tasks, task => task.Id == completed.Id);
    }

    [Fact]
    public async Task CompletionBeforeConfiguredDayEndUsesPreviousBusinessDate()
    {
        InMemoryTaskRepository repository = new();
        TaskService service = CreateService(
            repository,
            new DateTimeOffset(2026, 11, 3, 3, 0, 0, TimeSpan.Zero),
            new TestDayBoundarySettings(DayEndTime.SixAm));
        TaskSummary task = await service.CreateAsync(CreateRequest("Late completion"));

        await service.CompleteAsync(task.Id);

        Assert.Equal(new DateOnly(2026, 11, 2), Assert.Single(repository.Tasks).CompletedDate);
    }

    private static TaskService CreateService(
        InMemoryTaskRepository repository,
        DateTimeOffset utcNow,
        IDayBoundarySettings? dayBoundarySettings = null) =>
        new(repository, dayBoundarySettings ?? new TestDayBoundarySettings(), new FixedTimeProvider(utcNow));

    private sealed class InMemoryTaskRepository : ITaskItemRepository
    {
        public List<TaskItem> Tasks { get; } = [];

        public Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
        {
            Tasks.Add(task);
            return Task.CompletedTask;
        }

        public Task<TaskItem?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Tasks.SingleOrDefault(task => task.Id == id && !task.IsDeleted));

        public Task<IReadOnlyList<TaskItem>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default)
        {
            IEnumerable<TaskItem> tasks = Tasks.Where(task => !task.IsDeleted &&
                (!query.Status.HasValue || task.Status == query.Status.Value));
            return Task.FromResult<IReadOnlyList<TaskItem>>(tasks.ToArray());
        }

        public Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static CreateTaskRequest CreateRequest(string title, TaskPriority priority = TaskPriority.None) =>
        new(title, null, new DateOnly(2026, 10, 29), null, priority);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
