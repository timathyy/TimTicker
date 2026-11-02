using TimTicker.Application.Tasks;
using TimTicker.Domain.Timeline;

namespace TimTicker.Application.Timeline;

public sealed record StartTimelineEntryRequest(string Title, string? Description, string? Category, IReadOnlyList<Guid> TaskItemIds);

public sealed record CreateTimelineEntryRequest(
    string Title,
    string? Description,
    string? Category,
    IReadOnlyList<Guid> TaskItemIds,
    DateOnly RecordDate,
    DateTime StartLocal,
    DateTime EndLocal);

public sealed record UpdateTimelineEntryRequest(
    Guid Id,
    string Title,
    string? Description,
    string? Category,
    IReadOnlyList<Guid> TaskItemIds,
    DateOnly RecordDate,
    DateTime StartLocal,
    DateTime EndLocal);

public sealed record TimelineEntrySummary(
    Guid Id,
    string Title,
    string? Description,
    string? Category,
    IReadOnlyList<TaskSummary> LinkedTasks,
    DateOnly RecordDate,
    DateTimeOffset StartAtUtc,
    DateTimeOffset? EndAtUtc,
    DateTime StartLocal,
    DateTime? EndLocal,
    TimeSpan DayDuration,
    bool IsRunning,
    TimelineEntrySource Source);

public sealed record TimelineDaySummary(DateOnly Date, IReadOnlyList<TimelineEntrySummary> Entries, TimeSpan TotalDuration);

public sealed record TimelineTaskOption(Guid Id, string Title);

public sealed class TimelineOverlapException : InvalidOperationException
{
    public TimelineOverlapException()
        : base("The timeline entry overlaps another active entry.")
    {
    }
}
