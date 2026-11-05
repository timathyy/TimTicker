using TimTicker.Domain.Tasks;

namespace TimTicker.Application.Tasks;

public sealed class TaskFolderService(ITaskFolderRepository repository, TimeProvider timeProvider) : ITaskFolderService
{
    public async Task<TaskFolderSummary> CreateAsync(
        string name,
        Guid? parentFolderId = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = name?.Trim() ?? throw new ArgumentNullException(nameof(name));
        await EnsureParentExistsAsync(parentFolderId, cancellationToken).ConfigureAwait(false);
        if (await repository.NameExistsAsync(parentFolderId, normalizedName, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("A folder with this name already exists in the current folder.");
        }

        TaskFolder folder = new(
            Guid.NewGuid(),
            normalizedName,
            TaskFolderKind.User,
            timeProvider.GetUtcNow(),
            parentFolderId);
        await repository.AddAsync(folder, cancellationToken).ConfigureAwait(false);
        return ToSummary(folder);
    }

    public async Task<IReadOnlyList<TaskFolderSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        (await repository.ListAsync(cancellationToken).ConfigureAwait(false)).Select(ToSummary).ToArray();

    internal static TaskFolderSummary ToSummary(TaskFolder folder) => new(
        folder.Id,
        folder.Name,
        folder.ParentId,
        folder.Kind,
        folder.CreatedAtUtc,
        folder.UpdatedAtUtc);

    private async Task EnsureParentExistsAsync(Guid? parentFolderId, CancellationToken cancellationToken)
    {
        if (parentFolderId.HasValue &&
            !await repository.ExistsAsync(parentFolderId.Value, cancellationToken).ConfigureAwait(false))
        {
            throw new KeyNotFoundException("The parent task folder was not found.");
        }
    }
}
