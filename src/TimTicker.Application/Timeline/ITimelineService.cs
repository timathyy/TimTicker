namespace TimTicker.Application.Timeline;

public interface ITimelineService
{
    Task<TimelineDaySummary> GetTodayAsync(CancellationToken cancellationToken = default);

    Task<TimelineDaySummary> GetForDateAsync(DateOnly timelineDate, CancellationToken cancellationToken = default);

    Task<TimelineEntrySummary> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimelineEntrySummary>> ListForTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimelineTaskOption>> ListTaskOptionsAsync(
        DateOnly planDate,
        CancellationToken cancellationToken = default);

    Task<TimelineEntrySummary> StartAsync(StartTimelineEntryRequest request, CancellationToken cancellationToken = default);

    Task<TimelineEntrySummary> StopCurrentAsync(CancellationToken cancellationToken = default);

    Task<TimelineEntrySummary> CreateAsync(CreateTimelineEntryRequest request, CancellationToken cancellationToken = default);

    Task<TimelineEntrySummary> UpdateAsync(UpdateTimelineEntryRequest request, CancellationToken cancellationToken = default);

    Task RemoveTaskAsync(Guid entryId, Guid taskId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
