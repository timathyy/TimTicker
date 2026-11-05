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

    public async Task<bool> NameExistsAsync(Guid? parentId, string name, CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TaskFolders.AnyAsync(
            folder => !folder.IsDeleted && folder.ParentId == parentId && folder.Name == name,
            cancellationToken).ConfigureAwait(false);
    }
}
