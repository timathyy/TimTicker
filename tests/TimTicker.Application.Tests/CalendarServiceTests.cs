using TimTicker.Application.Calendar;
using TimTicker.Application.Planning;
using TimTicker.Application.Settings;
using TimTicker.Application.Tasks;
using TimTicker.Application.Timeline;

namespace TimTicker.Application.Tests;

public sealed class CalendarServiceTests
{
    [Fact]
    public async Task LeapYearMonthUsesMondayFirstSixWeekRange()
    {
        RecordingCalendarRepository repository = new()
        {
            Data = new CalendarRangeData(
                [new CalendarPlanData(new DateOnly(2028, 2, 29), true, 1, 1, 1m)],
                []),
        };
        CalendarService service = CreateService(repository);

        CalendarMonthSummary month = await service.GetMonthAsync(2028, 2);

        Assert.Equal(new DateOnly(2028, 1, 31), month.VisibleStart);
        Assert.Equal(new DateOnly(2028, 3, 12), month.VisibleEnd);
        Assert.Equal(42, month.Days.Count);
        CalendarDaySummary leapDay = Assert.Single(month.Days, day => day.Date == new DateOnly(2028, 2, 29));
        Assert.True(leapDay.HasPlan);
        Assert.True(leapDay.IsClosed);
        Assert.Equal(1m, leapDay.CompletionRate);
        Assert.Equal(month.VisibleStart, repository.StartDate);
        Assert.Equal(month.VisibleEnd.AddDays(1), repository.EndDateExclusive);
    }

    [Fact]
    public async Task MonthRangeAndTimelineDaysRespectLocalSixAmBoundary()
    {
        TimeZoneInfo utcPlusEight = TimeZoneInfo.CreateCustomTimeZone(
            "Calendar UTC+8",
            TimeSpan.FromHours(8),
            "Calendar UTC+8",
            "Calendar UTC+8");
        FixedTimeProvider clock = new(new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero), utcPlusEight);
        RecordingCalendarRepository repository = new()
        {
            Data = new CalendarRangeData(
                [],
                [new CalendarTimelineInterval(
                    new DateTimeOffset(2026, 2, 23, 21, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 2, 23, 23, 0, 0, TimeSpan.Zero))]),
        };
        CalendarService service = CreateService(repository, new TestDayBoundarySettings(DayEndTime.SixAm), clock);

        CalendarMonthSummary month = await service.GetMonthAsync(2026, 3);

        Assert.Equal(new DateOnly(2026, 2, 23), month.VisibleStart);
        Assert.Equal(new DateTimeOffset(2026, 2, 22, 22, 0, 0, TimeSpan.Zero), repository.StartAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 4, 5, 22, 0, 0, TimeSpan.Zero), repository.EndAtUtc);
        CalendarDaySummary timelineOnlyDay = Assert.Single(month.Days, day => day.Date == new DateOnly(2026, 2, 23));
        Assert.True(timelineOnlyDay.HasTimelineEntries);
        Assert.True(timelineOnlyDay.HasData);
        Assert.False(timelineOnlyDay.HasPlan);
        Assert.Equal(0, timelineOnlyDay.TotalCount);
        Assert.True(Assert.Single(month.Days, day => day.Date == new DateOnly(2026, 2, 24)).HasTimelineEntries);
        Assert.False(Assert.Single(month.Days, day => day.Date == new DateOnly(2026, 2, 25)).HasTimelineEntries);
    }

    [Fact]
    public async Task DayDetailsCombinePlanAndClippedTimelineSummary()
    {
        DateOnly selectedDate = new(2026, 11, 3);
        DailyPlanSummary plan = new(
            Guid.NewGuid(),
            selectedDate,
            [],
            0,
            0,
            null,
            true,
            new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero),
            "Good day");
        TimelineDaySummary timeline = new(selectedDate, [], TimeSpan.FromMinutes(90));
        StubPlanService plans = new(plan);
        StubTimelineService timelines = new(timeline);
        CalendarService service = new(
            new RecordingCalendarRepository(),
            plans,
            timelines,
            new TestDayBoundarySettings(),
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc));

        CalendarDayDetails details = await service.GetDayAsync(selectedDate);

        Assert.Same(plan, details.Plan);
        Assert.Same(timeline, details.Timeline);
        Assert.True(details.HasData);
        Assert.Equal(selectedDate, plans.RequestedDate);
        Assert.Equal(selectedDate, timelines.RequestedDate);
    }

    [Fact]
    public async Task DayDetailsTreatPersistedEmptyPlanAsNoData()
    {
        DateOnly selectedDate = new(2026, 11, 3);
        DailyPlanSummary emptyPlan = new(
            Guid.NewGuid(),
            selectedDate,
            [],
            0,
            0,
            null,
            false,
            null,
            null);
        CalendarService service = new(
            new RecordingCalendarRepository(),
            new StubPlanService(emptyPlan),
            new StubTimelineService(new TimelineDaySummary(selectedDate, [], TimeSpan.Zero)),
            new TestDayBoundarySettings(),
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc));

        CalendarDayDetails details = await service.GetDayAsync(selectedDate);

        Assert.Same(emptyPlan, details.Plan);
        Assert.False(details.HasData);
    }

    private static CalendarService CreateService(
        RecordingCalendarRepository repository,
        IDayBoundarySettings? settings = null,
        TimeProvider? timeProvider = null) =>
        new(
            repository,
            new StubPlanService(),
            new StubTimelineService(),
            settings ?? new TestDayBoundarySettings(),
            timeProvider ?? new FixedTimeProvider(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc));

    private sealed class RecordingCalendarRepository : ICalendarReadRepository
    {
        public CalendarRangeData Data { get; init; } = new([], []);

        public DateOnly StartDate { get; private set; }

        public DateOnly EndDateExclusive { get; private set; }

        public DateTimeOffset StartAtUtc { get; private set; }

        public DateTimeOffset EndAtUtc { get; private set; }

        public Task<CalendarRangeData> ReadRangeAsync(
            DateOnly startDate,
            DateOnly endDateExclusive,
            DateTimeOffset startAtUtc,
            DateTimeOffset endAtUtc,
            CancellationToken cancellationToken = default)
        {
            StartDate = startDate;
            EndDateExclusive = endDateExclusive;
            StartAtUtc = startAtUtc;
            EndAtUtc = endAtUtc;
            return Task.FromResult(Data);
        }
    }

    private sealed class StubPlanService(DailyPlanSummary? result = null) : IDailyPlanService
    {
        public DateOnly RequestedDate { get; private set; }

        public Task<DailyPlanSummary?> GetForDateAsync(DateOnly planDate, CancellationToken cancellationToken = default)
        {
            RequestedDate = planDate;
            return Task.FromResult(result?.Date == planDate ? result : null);
        }

        public Task<DailyPlanSummary> GetTodayAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DailyPlanSummary> GetOrCreateForDateAsync(DateOnly planDate, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<DailyPlanTaskCandidate>> ListTodayCandidatesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<DailyPlanTaskCandidate>> ListCandidatesAsync(DateOnly planDate, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DailyPlanSummary> AddTasksToTodayAsync(IEnumerable<Guid> taskIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DailyPlanSummary> AddTasksAsync(DateOnly planDate, IEnumerable<Guid> taskIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DailyPlanSummary> CreateTaskAndAddToTodayAsync(CreateTaskRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DailyPlanSummary> CreateTaskAndAddAsync(DateOnly planDate, CreateTaskRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DailyPlanSummary> RemoveTaskFromTodayAsync(Guid taskId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DailyPlanSummary> RemoveTaskAsync(DateOnly planDate, Guid taskId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DailyPlanSummary> MoveTodayTaskAsync(Guid taskId, PlanMoveDirection direction, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DailyPlanSummary> MoveTaskAsync(DateOnly planDate, Guid taskId, PlanMoveDirection direction, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DailyPlanSummary> CloseTodayAsync(string? reviewText, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DailyPlanSummary> CloseAsync(DateOnly planDate, string? reviewText, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<DailyPlanSummary> ReopenAsync(DateOnly planDate, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubTimelineService(TimelineDaySummary? result = null) : ITimelineService
    {
        public DateOnly RequestedDate { get; private set; }

        public Task<TimelineDaySummary> GetForDateAsync(DateOnly timelineDate, CancellationToken cancellationToken = default)
        {
            RequestedDate = timelineDate;
            return Task.FromResult(result ?? new TimelineDaySummary(timelineDate, [], TimeSpan.Zero));
        }

        public Task<TimelineDaySummary> GetTodayAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TimelineEntrySummary> GetAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<TimelineEntrySummary>> ListForTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<TimelineTaskOption>> ListTaskOptionsAsync(DateOnly planDate, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TimelineEntrySummary> StartAsync(StartTimelineEntryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TimelineEntrySummary> StopCurrentAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TimelineEntrySummary> CreateAsync(CreateTimelineEntryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<TimelineEntrySummary> UpdateAsync(UpdateTimelineEntryRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RemoveTaskAsync(Guid entryId, Guid taskId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow, TimeZoneInfo localTimeZone) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone { get; } = localTimeZone;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
