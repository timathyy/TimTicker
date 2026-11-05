using Microsoft.EntityFrameworkCore;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.Infrastructure.Persistence;

public sealed class TaskItemRepository(IDbContextFactory<TimTickerDbContext> contextFactory) : ITaskItemRepository
{
    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.Tasks.AddAsync(task, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TaskItem?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Tasks.AsNoTracking().SingleOrDefaultAsync(task => task.Id == id && !task.IsDeleted, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaskItem>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        IQueryable<TaskItem> tasks = context.Tasks.AsNoTracking()
            .Where(task => !task.IsDeleted);

        if (query.Status is DomainTaskStatus status)
        {
            tasks = tasks.Where(task => task.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            string searchText = query.SearchText.Trim();
            tasks = tasks.Where(task => EF.Functions.Like(task.Title, $"%{searchText}%") ||
                                        (task.Description != null && EF.Functions.Like(task.Description, $"%{searchText}%")));
        }

        if (query.RootOnly)
        {
            tasks = tasks.Where(task => task.FolderId == null);
        }
        else if (query.FolderId is Guid folderId)
        {
            tasks = tasks.Where(task => task.FolderId == folderId);
        }

        tasks = query.SortOrder switch
        {
            TaskSortOrder.CreatedAtDescending => tasks.OrderByDescending(task => task.CreatedAtUtc),
            TaskSortOrder.CreatedAtAscending => tasks.OrderBy(task => task.CreatedAtUtc),
            TaskSortOrder.DueDateAscending => tasks.OrderBy(task => task.DueDate == null).ThenBy(task => task.DueDate).ThenByDescending(task => task.CreatedAtUtc),
            TaskSortOrder.DueDateDescending => tasks.OrderBy(task => task.DueDate == null).ThenByDescending(task => task.DueDate).ThenByDescending(task => task.CreatedAtUtc),
            TaskSortOrder.PriorityDescending => tasks.OrderByDescending(task => task.Priority).ThenByDescending(task => task.CreatedAtUtc),
            TaskSortOrder.PriorityAscending => tasks.OrderBy(task => task.Priority).ThenByDescending(task => task.CreatedAtUtc),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.SortOrder, "Unknown task sort order."),
        };

        return await tasks.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.Tasks.Update(task);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
