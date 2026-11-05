using TimTicker.Domain.Tasks;

namespace TimTicker.Application.Tasks;

public interface ITaskFolderRepository
{
    Task AddAsync(TaskFolder folder, CancellationToken cancellationToken = default);

    Task SaveAsync(TaskFolder folder, CancellationToken cancellationToken = default);

    Task DeleteTreeAsync(
        IReadOnlyCollection<Guid> folderIds,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken = default);

    Task<TaskFolder?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskFolder>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(
        Guid? parentId,
        string name,
        Guid? excludedFolderId = null,
        CancellationToken cancellationToken = default);

}
