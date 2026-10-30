using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.Application.Tests;

public sealed class TaskServiceTests
{
    [Fact]
    public async Task CreateAndCompletePersistsExpectedState()
    {
        InMemoryTaskRepository repository = new();
        TaskService service = new(repository, new FixedTimeProvider(new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero)));

        TaskSummary created = await service.CreateAsync(new CreateTaskRequest("Review tasks", null, null, TaskPriority.Medium));
        await service.CompleteAsync(created.Id);

        TaskItem stored = Assert.Single(repository.Tasks);
        Assert.Equal(DomainTaskStatus.Completed, stored.Status);
        Assert.Equal(created.Id, stored.Id);
    }

    [Fact]
    public async Task ListUsesRequestedStatusAndExcludesDeletedTasks()
    {
        InMemoryTaskRepository repository = new();
        TaskService service = new(repository, new FixedTimeProvider(new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero)));
        TaskSummary completed = await service.CreateAsync(new CreateTaskRequest("Done", null, null, TaskPriority.None));
        TaskSummary deleted = await service.CreateAsync(new CreateTaskRequest("Remove", null, null, TaskPriority.None));
        await service.CompleteAsync(completed.Id);
        await service.DeleteAsync(deleted.Id);

        IReadOnlyList<TaskSummary> tasks = await service.ListAsync(new TaskQuery(DomainTaskStatus.Pending, null, TaskSortOrder.CreatedAtDescending));

        Assert.Empty(tasks);
    }

    [Fact]
    public async Task ListWithNoStatusReturnsTasksFromEveryState()
    {
        InMemoryTaskRepository repository = new();
        TaskService service = new(repository, new FixedTimeProvider(new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero)));
        TaskSummary pending = await service.CreateAsync(new CreateTaskRequest("Pending", null, null, TaskPriority.None));
        TaskSummary completed = await service.CreateAsync(new CreateTaskRequest("Completed", null, null, TaskPriority.None));
        await service.CompleteAsync(completed.Id);

        IReadOnlyList<TaskSummary> tasks = await service.ListAsync(new TaskQuery(null, null, TaskSortOrder.CreatedAtDescending));

        Assert.Equal(2, tasks.Count);
        Assert.Contains(tasks, task => task.Id == pending.Id);
        Assert.Contains(tasks, task => task.Id == completed.Id);
    }

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

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
