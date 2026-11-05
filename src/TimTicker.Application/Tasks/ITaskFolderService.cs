namespace TimTicker.Application.Tasks;

public interface ITaskFolderService
{
    Task<TaskFolderSummary> CreateAsync(string name, Guid? parentFolderId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskFolderSummary>> ListAsync(CancellationToken cancellationToken = default);
}
