using TimTicker.Application.Tasks;

namespace TimTicker.Application.Timeline;

public sealed record StartTimelineEntryRequest(string Title, string? Description, string? Category, Guid? TaskItemId);

public sealed record CreateTimelineEntryRequest(
    string Title,
    string? Description,
    string? Category,
    Guid? TaskItemId,
    DateTime StartLocal,
    DateTime EndLocal);

public sealed record UpdateTimelineEntryRequest(
    Guid Id,
    string Title,
    string? Description,
    string? Category,
    Guid? TaskItemId,
    DateTime StartLocal,
    DateTime EndLocal);

public sealed record TimelineEntrySummary(
    Guid Id,
    string Title,
    string? Description,
    string? Category,
    Guid? TaskItemId,
    string? TaskTitle,
    DateTimeOffset StartAtUtc,
    DateTimeOffset? EndAtUtc,
    DateTime StartLocal,
    DateTime? EndLocal,
    TimeSpan DayDuration,
    bool IsRunning);

public sealed record TimelineDaySummary(DateOnly Date, IReadOnlyList<TimelineEntrySummary> Entries, TimeSpan TotalDuration);

public sealed record TimelineTaskOption(Guid Id, string Title);

public sealed class TimelineOverlapException : InvalidOperationException
{
    public TimelineOverlapException()
        : base("The timeline entry overlaps another active entry.")
    {
    }
}
