using TimTicker.Domain.Tasks;

namespace TimTicker.Application.Tasks;

public interface ITaskItemRepository
{
    Task AddAsync(TaskItem task, CancellationToken cancellationToken = default);

    Task<TaskItem?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskItem>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default);

    Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default);
}
