using Microsoft.EntityFrameworkCore;
using TimTicker.Application.Timeline;
using TimTicker.Domain.Timeline;

namespace TimTicker.Infrastructure.Persistence;

public sealed class TimelineEntryRepository(IDbContextFactory<TimTickerDbContext> contextFactory) : ITimelineEntryRepository
{
    public async Task AddAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.TimelineEntries.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TimelineEntry?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TimelineEntries.AsNoTracking()
            .Include(entry => entry.TaskLinks)
            .SingleOrDefaultAsync(entry => entry.Id == id && !entry.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TimelineEntry?> FindRunningAsync(CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TimelineEntries.AsNoTracking()
            .Include(entry => entry.TaskLinks)
            .SingleOrDefaultAsync(entry => !entry.IsDeleted && entry.EndAtUtc == null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TimelineEntry>> ListIntersectingAsync(
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TimelineEntries.AsNoTracking()
            .Include(entry => entry.TaskLinks)
            .Where(entry => !entry.IsDeleted &&
                            entry.StartAtUtc < endAtUtc &&
                            (entry.EndAtUtc == null || entry.EndAtUtc > startAtUtc))
            .OrderBy(entry => entry.StartAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TimelineEntry>> ListForDateAsync(
        DateOnly recordDate,
        DateTimeOffset dayStartUtc,
        DateTimeOffset dayEndUtc,
        CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TimelineEntries.AsNoTracking()
            .Include(entry => entry.TaskLinks)
            .Where(entry => !entry.IsDeleted &&
                            entry.StartAtUtc < dayEndUtc &&
                            (entry.EndAtUtc == null || entry.EndAtUtc > dayStartUtc))
            .OrderBy(entry => entry.StartAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TimelineEntry>> ListByTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TimelineEntries.AsNoTracking()
            .Include(entry => entry.TaskLinks)
            .Where(entry => !entry.IsDeleted && entry.TaskLinks.Any(link => link.TaskItemId == taskId && link.RemovedAtUtc == null))
            .OrderByDescending(entry => entry.StartAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        HashSet<Guid> persistedTaskIds = (await context.TimelineEntryTaskLinks
            .AsNoTracking()
            .Where(link => link.TimelineEntryId == entry.Id)
            .Select(link => link.TaskItemId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .ToHashSet();
        context.TimelineEntries.Update(entry);
        foreach (TimelineEntryTaskLink link in entry.TaskLinks.Where(link => !persistedTaskIds.Contains(link.TaskItemId)))
        {
            context.Entry(link).State = EntityState.Added;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
