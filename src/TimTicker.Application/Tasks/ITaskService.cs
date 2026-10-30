namespace TimTicker.Application.Tasks;

public interface ITaskService
{
    Task<TaskSummary> CreateAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);

    Task<TaskSummary> UpdateAsync(UpdateTaskRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskSummary>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default);

    Task CompleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task ReopenAsync(Guid id, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
