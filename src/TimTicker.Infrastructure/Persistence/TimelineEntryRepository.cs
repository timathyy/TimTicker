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
            .SingleOrDefaultAsync(entry => entry.Id == id && !entry.IsDeleted, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TimelineEntry?> FindRunningAsync(CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.TimelineEntries.AsNoTracking()
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
            .Where(entry => !entry.IsDeleted &&
                            entry.StartAtUtc < endAtUtc &&
                            (entry.EndAtUtc == null || entry.EndAtUtc > startAtUtc))
            .OrderBy(entry => entry.StartAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.TimelineEntries.Update(entry);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
