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
        if (await repository.NameExistsAsync(parentFolderId, normalizedName, cancellationToken: cancellationToken).ConfigureAwait(false))
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

    public async Task<TaskFolderSummary> RenameAsync(
        Guid id,
        string name,
        CancellationToken cancellationToken = default)
    {
        TaskFolder folder = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        string normalizedName = name?.Trim() ?? throw new ArgumentNullException(nameof(name));
        if (await repository.NameExistsAsync(folder.ParentId, normalizedName, folder.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("A folder with this name already exists in the current folder.");
        }

        folder.Rename(normalizedName, timeProvider.GetUtcNow());
        await repository.SaveAsync(folder, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ToSummary(folder);
    }

    public async Task<TaskFolderSummary> MoveAsync(
        Guid id,
        Guid? parentFolderId,
        CancellationToken cancellationToken = default)
    {
        TaskFolder folder = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        await EnsureParentExistsAsync(parentFolderId, cancellationToken).ConfigureAwait(false);
        if (parentFolderId == folder.Id)
        {
            throw new InvalidOperationException("A folder cannot be moved into itself.");
        }

        IReadOnlyList<TaskFolder> folders = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, TaskFolder> foldersById = folders.ToDictionary(item => item.Id);
        Guid? ancestorId = parentFolderId;
        HashSet<Guid> visited = [];
        while (ancestorId is Guid currentId && foldersById.TryGetValue(currentId, out TaskFolder? ancestor) && visited.Add(currentId))
        {
            if (currentId == folder.Id)
            {
                throw new InvalidOperationException("A folder cannot be moved into one of its descendants.");
            }

            ancestorId = ancestor.ParentId;
        }

        if (await repository.NameExistsAsync(parentFolderId, folder.Name, folder.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("A folder with this name already exists in the target folder.");
        }

        folder.MoveTo(parentFolderId, timeProvider.GetUtcNow());
        await repository.SaveAsync(folder, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ToSummary(folder);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TaskFolder folder = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (folder.Kind == TaskFolderKind.Template)
        {
            throw new InvalidOperationException("An automatic task folder cannot be deleted independently from its rule.");
        }

        if (await repository.HasChildrenAsync(id, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("A folder containing subfolders cannot be deleted.");
        }

        folder.Delete(timeProvider.GetUtcNow());
        await repository.SaveAsync(folder, moveTasksToRoot: true, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

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

    private async Task<TaskFolder> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The folder identifier must not be empty.", nameof(id));
        }

        return await repository.FindAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("The requested task folder was not found.");
    }
}
