namespace TimTicker.Application.Tasks;

public interface ITaskFolderService
{
    Task<TaskFolderSummary> CreateAsync(string name, Guid? parentFolderId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskFolderSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<TaskFolderSummary> RenameAsync(Guid id, string name, CancellationToken cancellationToken = default);

    Task<TaskFolderSummary> MoveAsync(Guid id, Guid? parentFolderId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
