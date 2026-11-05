using Microsoft.EntityFrameworkCore;
using TimTicker.Application.Templates;
using TimTicker.Domain.Tasks;
using TimTicker.Domain.Templates;

namespace TimTicker.Infrastructure.Persistence;

public sealed class TaskTemplateRepository(IDbContextFactory<TimTickerDbContext> contextFactory) : ITaskTemplateRepository
{
    public async Task AddAsync(TaskTemplate taskTemplate, TaskFolder folder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskTemplate);
        ArgumentNullException.ThrowIfNull(folder);
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await context.TaskFolders.AddAsync(folder, cancellationToken).ConfigureAwait(false);
        await context.TaskTemplates.AddAsync(taskTemplate, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TaskTemplate?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TaskTemplates.AsNoTracking()
            .SingleOrDefaultAsync(template => template.Id == id && !template.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaskTemplate>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TaskTemplates.AsNoTracking()
            .Where(template => !template.IsDeleted)
            .OrderBy(template => template.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveGenerationAsync(
        TaskTemplate taskTemplate,
        IReadOnlyCollection<TaskItem> tasks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskTemplate);
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Count == 0)
        {
            return;
        }

        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        context.TaskTemplates.Update(taskTemplate);
        await context.Tasks.AddRangeAsync(tasks, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> NameExistsAsync(Guid? parentFolderId, string name, CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TaskFolders.AnyAsync(
            folder => !folder.IsDeleted && folder.ParentId == parentFolderId && folder.Name == name,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> FolderExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TaskFolders.AnyAsync(
            folder => folder.Id == id && !folder.IsDeleted,
            cancellationToken).ConfigureAwait(false);
    }
}
