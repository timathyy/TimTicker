using TimTicker.Domain.Timeline;

namespace TimTicker.Application.Timeline;

public interface ITimelineEntryRepository
{
    Task AddAsync(TimelineEntry entry, CancellationToken cancellationToken = default);

    Task<TimelineEntry?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TimelineEntry?> FindRunningAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimelineEntry>> ListIntersectingAsync(
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(TimelineEntry entry, CancellationToken cancellationToken = default);
}
