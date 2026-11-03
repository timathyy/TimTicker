using TimTicker.Application.Planning;
using TimTicker.Application.Timeline;

namespace TimTicker.Application.Calendar;

public sealed record CalendarDaySummary(
    DateOnly Date,
    bool HasPlan,
    bool IsClosed,
    int CompletedCount,
    int TotalCount,
    decimal? CompletionRate,
    bool HasTimelineEntries)
{
    public bool HasData => HasPlan || HasTimelineEntries;
}

public sealed record CalendarMonthSummary(
    DateOnly MonthStart,
    DateOnly VisibleStart,
    DateOnly VisibleEnd,
    IReadOnlyList<CalendarDaySummary> Days);

public sealed record CalendarDayDetails(
    DateOnly Date,
    DailyPlanSummary? Plan,
    TimelineDaySummary Timeline)
{
    public bool HasData =>
        Plan?.TotalCount > 0 ||
        !string.IsNullOrWhiteSpace(Plan?.ReviewText) ||
        Timeline.Entries.Count > 0;
}

public sealed record CalendarPlanData(
    DateOnly Date,
    bool IsClosed,
    int CompletedCount,
    int TotalCount,
    decimal? CompletionRate);

public sealed record CalendarTimelineInterval(DateTimeOffset StartAtUtc, DateTimeOffset? EndAtUtc);

public sealed record CalendarRangeData(
    IReadOnlyList<CalendarPlanData> Plans,
    IReadOnlyList<CalendarTimelineInterval> TimelineIntervals);
