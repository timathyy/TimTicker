using Microsoft.EntityFrameworkCore;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;

namespace TimTicker.Infrastructure.Persistence;

public sealed class TaskFolderRepository(IDbContextFactory<TimTickerDbContext> contextFactory) : ITaskFolderRepository
{
    public async Task AddAsync(TaskFolder folder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.TaskFolders.AddAsync(folder, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(
        TaskFolder folder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (folder.Kind == TaskFolderKind.Template)
        {
            await context.TaskTemplates
                .Where(template => template.FolderId == folder.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(template => template.Name, folder.Name)
                        .SetProperty(template => template.UpdatedAtUtc, folder.UpdatedAtUtc),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        context.TaskFolders.Update(folder);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteTreeAsync(
        IReadOnlyCollection<Guid> folderIds,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folderIds);
        if (folderIds.Count == 0)
        {
            return;
        }

        Guid[] ids = folderIds.Distinct().ToArray();
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await context.Tasks
            .Where(task => !task.IsDeleted && task.FolderId.HasValue && ids.Contains(task.FolderId.Value))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(task => task.IsDeleted, true)
                    .SetProperty(task => task.UpdatedAtUtc, deletedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
        await context.TaskTemplates
            .Where(template => !template.IsDeleted && ids.Contains(template.FolderId))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(template => template.IsDeleted, true)
                    .SetProperty(template => template.IsEnabled, false)
                    .SetProperty(template => template.UpdatedAtUtc, deletedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
        await context.TaskFolders
            .Where(folder => !folder.IsDeleted && ids.Contains(folder.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(folder => folder.IsDeleted, true)
                    .SetProperty(folder => folder.UpdatedAtUtc, deletedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TaskFolder?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TaskFolders.AsNoTracking()
            .SingleOrDefaultAsync(folder => folder.Id == id && !folder.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaskFolder>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TaskFolders.AsNoTracking()
            .Where(folder => !folder.IsDeleted)
            .OrderBy(folder => folder.ParentId)
            .ThenBy(folder => folder.Kind)
            .ThenBy(folder => folder.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TaskFolders.AnyAsync(folder => folder.Id == id && !folder.IsDeleted, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> NameExistsAsync(
        Guid? parentId,
        string name,
        Guid? excludedFolderId = null,
        CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TaskFolders.AnyAsync(
            folder => !folder.IsDeleted &&
                      folder.ParentId == parentId &&
                      folder.Name == name &&
                      (!excludedFolderId.HasValue || folder.Id != excludedFolderId.Value),
            cancellationToken).ConfigureAwait(false);
    }

}
