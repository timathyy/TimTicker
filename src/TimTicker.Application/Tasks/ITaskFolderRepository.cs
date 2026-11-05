using TimTicker.Domain.Tasks;

namespace TimTicker.Application.Tasks;

public interface ITaskFolderRepository
{
    Task AddAsync(TaskFolder folder, CancellationToken cancellationToken = default);

    Task SaveAsync(TaskFolder folder, bool moveTasksToRoot = false, CancellationToken cancellationToken = default);

    Task<TaskFolder?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskFolder>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(
        Guid? parentId,
        string name,
        Guid? excludedFolderId = null,
        CancellationToken cancellationToken = default);

    Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default);
}
