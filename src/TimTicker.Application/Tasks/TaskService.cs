using TimTicker.Domain.Tasks;

namespace TimTicker.Application.Tasks;

public sealed class TaskService(ITaskItemRepository repository, TimeProvider timeProvider) : ITaskService
{
    public async Task<TaskSummary> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        TaskItem task = new(Guid.NewGuid(), request.Title, request.Description, request.DueDate, request.Priority, UtcNow());
        await repository.AddAsync(task, cancellationToken).ConfigureAwait(false);
        return ToSummary(task);
    }

    public async Task<TaskSummary> UpdateAsync(UpdateTaskRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        TaskItem task = await GetTaskAsync(request.Id, cancellationToken).ConfigureAwait(false);
        task.Update(request.Title, request.Description, request.DueDate, request.Priority, UtcNow());
        await repository.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        return ToSummary(task);
    }

    public async Task<IReadOnlyList<TaskSummary>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        IReadOnlyList<TaskItem> tasks = await repository.ListAsync(query, cancellationToken).ConfigureAwait(false);
        return tasks.Select(ToSummary).ToArray();
    }

    public Task CompleteAsync(Guid id, CancellationToken cancellationToken = default) => ChangeAsync(id, task => task.Complete(UtcNow()), cancellationToken);

    public Task ReopenAsync(Guid id, CancellationToken cancellationToken = default) => ChangeAsync(id, task => task.Reopen(UtcNow()), cancellationToken);

    public Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => ChangeAsync(id, task => task.Archive(UtcNow()), cancellationToken);

    public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default) => ChangeAsync(id, task => task.Restore(UtcNow()), cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => ChangeAsync(id, task => task.Delete(UtcNow()), cancellationToken);

    private async Task ChangeAsync(Guid id, Action<TaskItem> change, CancellationToken cancellationToken)
    {
        TaskItem task = await GetTaskAsync(id, cancellationToken).ConfigureAwait(false);
        change(task);
        await repository.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TaskItem> GetTaskAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The task identifier must not be empty.", nameof(id));
        }

        return await repository.FindAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("The requested task was not found.");
    }

    private DateTimeOffset UtcNow() => timeProvider.GetUtcNow();

    private static TaskSummary ToSummary(TaskItem task) => new(
        task.Id,
        task.Title,
        task.Description,
        task.DueDate,
        task.Priority,
        task.Status,
        task.CreatedAtUtc,
        task.UpdatedAtUtc,
        task.CompletedAtUtc);
}
