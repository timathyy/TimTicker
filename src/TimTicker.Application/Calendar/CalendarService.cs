using TimTicker.Application.Planning;
using TimTicker.Application.Settings;
using TimTicker.Application.Timeline;

namespace TimTicker.Application.Calendar;

public sealed class CalendarService(
    ICalendarReadRepository repository,
    IDailyPlanService planService,
    ITimelineService timelineService,
    IDayBoundarySettings dayBoundarySettings,
    TimeProvider timeProvider) : ICalendarService
{
    private const int VisibleDayCount = 42;

    public async Task<CalendarMonthSummary> GetMonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        DateOnly monthStart = new(year, month, 1);
        int daysSinceMonday = ((int)monthStart.DayOfWeek + 6) % 7;
        DateOnly visibleStart = monthStart.AddDays(-daysSinceMonday);
        DateOnly visibleEndExclusive = visibleStart.AddDays(VisibleDayCount);
        DateTimeOffset startAtUtc = ConvertBoundaryLocalToUtc(
            DayBoundary.GetStartLocal(visibleStart, dayBoundarySettings.CurrentDayEndTime));
        DateTimeOffset endAtUtc = ConvertBoundaryLocalToUtc(
            DayBoundary.GetStartLocal(visibleEndExclusive, dayBoundarySettings.CurrentDayEndTime));

        CalendarRangeData data = await repository.ReadRangeAsync(
            visibleStart,
            visibleEndExclusive,
            startAtUtc,
            endAtUtc,
            cancellationToken).ConfigureAwait(false);
        Dictionary<DateOnly, CalendarPlanData> plans = data.Plans.ToDictionary(plan => plan.Date);
        DateTimeOffset now = timeProvider.GetUtcNow();
        List<CalendarDaySummary> days = new(VisibleDayCount);

        for (int offset = 0; offset < VisibleDayCount; offset++)
        {
            DateOnly date = visibleStart.AddDays(offset);
            (DateTimeOffset dayStart, DateTimeOffset dayEnd) = GetUtcDayBounds(date);
            bool hasTimelineEntries = data.TimelineIntervals.Any(interval =>
                interval.StartAtUtc < dayEnd &&
                (interval.EndAtUtc ?? now) > dayStart);
            plans.TryGetValue(date, out CalendarPlanData? plan);
            days.Add(new CalendarDaySummary(
                date,
                plan is not null,
                plan?.IsClosed == true,
                plan?.CompletedCount ?? 0,
                plan?.TotalCount ?? 0,
                plan?.CompletionRate,
                hasTimelineEntries));
        }

        return new CalendarMonthSummary(
            monthStart,
            visibleStart,
            visibleEndExclusive.AddDays(-1),
            days);
    }

    public async Task<CalendarDayDetails> GetDayAsync(
        DateOnly calendarDate,
        CancellationToken cancellationToken = default)
    {
        if (calendarDate == default)
        {
            throw new ArgumentException("The calendar date must not be empty.", nameof(calendarDate));
        }

        DailyPlanSummary? plan = await planService.GetForDateAsync(calendarDate, cancellationToken).ConfigureAwait(false);
        TimelineDaySummary timeline = await timelineService.GetForDateAsync(calendarDate, cancellationToken).ConfigureAwait(false);
        return new CalendarDayDetails(calendarDate, plan, timeline);
    }

    private (DateTimeOffset Start, DateTimeOffset End) GetUtcDayBounds(DateOnly date)
    {
        DayEndTime dayEndTime = dayBoundarySettings.CurrentDayEndTime;
        return (
            ConvertBoundaryLocalToUtc(DayBoundary.GetStartLocal(date, dayEndTime)),
            ConvertBoundaryLocalToUtc(DayBoundary.GetEndLocal(date, dayEndTime)));
    }

    private DateTimeOffset ConvertBoundaryLocalToUtc(DateTime localValue)
    {
        DateTime normalized = DateTime.SpecifyKind(localValue, DateTimeKind.Unspecified);
        while (timeProvider.LocalTimeZone.IsInvalidTime(normalized))
        {
            normalized = normalized.AddMinutes(1);
        }

        DateTime utc = TimeZoneInfo.ConvertTimeToUtc(normalized, timeProvider.LocalTimeZone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
