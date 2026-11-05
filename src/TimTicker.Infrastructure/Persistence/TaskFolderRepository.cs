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
        bool moveTasksToRoot = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (moveTasksToRoot)
        {
            await context.Tasks
                .Where(task => !task.IsDeleted && task.FolderId == folder.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(task => task.FolderId, (Guid?)null)
                        .SetProperty(task => task.UpdatedAtUtc, folder.UpdatedAtUtc),
                    cancellationToken)
                .ConfigureAwait(false);
        }

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

    public async Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TaskFolders.AnyAsync(
            folder => !folder.IsDeleted && folder.ParentId == id,
            cancellationToken).ConfigureAwait(false);
    }
}
