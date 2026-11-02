using TimTicker.Application.Tasks;
using TimTicker.Application.Timeline;
using TimTicker.Application.Planning;
using TimTicker.Application.Settings;
using TimTicker.Domain.Planning;
using TimTicker.Domain.Tasks;
using TimTicker.Domain.Timeline;

namespace TimTicker.Application.Tests;

public sealed class TimelineServiceTests
{
    [Fact]
    public async Task AdjacentEntriesAreAllowedButOverlappingEntryIsRejected()
    {
        InMemoryTimelineRepository timelines = new();
        TimelineService service = CreateService(timelines, new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
        await service.CreateAsync(CreateRequest(9, 0, 10, 0));
        await service.CreateAsync(CreateRequest(10, 0, 11, 0));

        await Assert.ThrowsAsync<TimelineOverlapException>(() => service.CreateAsync(CreateRequest(9, 30, 10, 30)));

        Assert.Equal(2, timelines.Entries.Count);
    }

    [Fact]
    public async Task CrossMidnightEntryIsSplitAtLocalDayBoundary()
    {
        InMemoryTimelineRepository timelines = new();
        TimelineService service = CreateService(timelines, new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero));
        timelines.Entries.Add(new TimelineEntry(
            Guid.NewGuid(),
            "Overnight",
            null,
            [],
            new DateTimeOffset(2026, 11, 2, 23, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 3, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 2, 23, 0, 0, TimeSpan.Zero)));

        TimelineDaySummary today = await service.GetTodayAsync();

        TimelineEntrySummary entry = Assert.Single(today.Entries);
        Assert.Equal(TimeSpan.FromHours(1), entry.DayDuration);
        Assert.Equal(TimeSpan.FromHours(1), today.TotalDuration);
        Assert.Equal(new DateTime(2026, 11, 2, 23, 0, 0), entry.StartLocal);
    }

    [Fact]
    public async Task LocalDayBoundariesUseConfiguredTimeZone()
    {
        InMemoryTimelineRepository timelines = new();
        TimeZoneInfo utcPlusEight = TimeZoneInfo.CreateCustomTimeZone("Test UTC+8", TimeSpan.FromHours(8), "Test UTC+8", "Test UTC+8");
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 3, 1, 0, 0, TimeSpan.Zero), utcPlusEight);
        TimelineService service = CreateService(timelines, clock);
        timelines.Entries.Add(new TimelineEntry(
            Guid.NewGuid(),
            "Local midnight",
            null,
            [],
            new DateTimeOffset(2026, 11, 2, 16, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 2, 18, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 2, 16, 0, 0, TimeSpan.Zero)));

        TimelineDaySummary today = await service.GetTodayAsync();

        TimelineEntrySummary entry = Assert.Single(today.Entries);
        Assert.Equal(new DateTime(2026, 11, 3, 0, 0, 0), entry.StartLocal);
        Assert.Equal(TimeSpan.FromHours(2), today.TotalDuration);
    }

    [Fact]
    public async Task RunningEntryCanBeRecoveredAndStoppedByNewServiceInstance()
    {
        InMemoryTimelineRepository timelines = new();
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));
        TimelineService firstService = CreateService(timelines, clock);
        TimelineEntrySummary started = await firstService.StartAsync(new StartTimelineEntryRequest("Focus", null, []));

        clock.UtcNow = clock.UtcNow.AddHours(2);
        TimelineService restartedService = CreateService(timelines, clock);
        TimelineDaySummary recovered = await restartedService.GetTodayAsync();
        TimelineEntrySummary stopped = await restartedService.StopCurrentAsync();

        Assert.True(Assert.Single(recovered.Entries).IsRunning);
        Assert.Equal(started.Id, stopped.Id);
        Assert.Equal(TimelineEntrySource.Timer, stopped.Source);
        Assert.False(stopped.IsRunning);
        Assert.Equal(TimeSpan.FromHours(2), stopped.DayDuration);
    }

    [Fact]
    public async Task ResultsAreSortedByStartTime()
    {
        InMemoryTimelineRepository timelines = new();
        TimelineService service = CreateService(timelines, new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
        TimelineEntrySummary later = await service.CreateAsync(CreateRequest(10, 0, 11, 0));
        TimelineEntrySummary earlier = await service.CreateAsync(CreateRequest(8, 0, 9, 0));

        TimelineDaySummary today = await service.GetTodayAsync();

        Assert.Equal(new[] { earlier.Id, later.Id }, today.Entries.Select(entry => entry.Id));
    }

    [Fact]
    public async Task ManualEntryCanLinkTaskBeEditedAndSoftDeleted()
    {
        InMemoryTimelineRepository timelines = new();
        InMemoryTaskRepository tasks = new();
        TaskItem task = new(
            Guid.NewGuid(),
            "Linked task",
            null,
            new DateOnly(2026, 11, 2),
            null,
            TaskPriority.None,
            new DateTimeOffset(2026, 11, 2, 7, 0, 0, TimeSpan.Zero));
        tasks.Tasks.Add(task);
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
        TimelineService service = new(timelines, tasks, new EmptyPlanRepository(), new TestDayBoundarySettings(), clock);
        TimelineEntrySummary created = await service.CreateAsync(new CreateTimelineEntryRequest(
            "Original",
            null,
            [task.Id],
            new DateOnly(2026, 11, 2),
            new DateTime(2026, 11, 2, 8, 0, 0),
            new DateTime(2026, 11, 2, 9, 0, 0)));

        TimelineEntrySummary updated = await service.UpdateAsync(new UpdateTimelineEntryRequest(
            created.Id,
            "Updated",
            "Description",
            [task.Id],
            new DateOnly(2026, 11, 2),
            new DateTime(2026, 11, 2, 9, 0, 0),
            new DateTime(2026, 11, 2, 10, 0, 0)));
        await service.DeleteAsync(created.Id);

        Assert.Equal("Updated", updated.Title);
        Assert.Equal("Linked task", Assert.Single(updated.LinkedTasks).Title);
        Assert.Equal(TimelineEntrySource.Manual, updated.Source);
        Assert.True(Assert.Single(timelines.Entries).IsDeleted);
        Assert.Empty((await service.GetTodayAsync()).Entries);
    }

    [Fact]
    public async Task EntryCanLinkMultipleTasksAndRemoveOneAssociation()
    {
        InMemoryTimelineRepository timelines = new();
        InMemoryTaskRepository tasks = new();
        DateTimeOffset now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        TaskItem first = new(Guid.NewGuid(), "First", null, new DateOnly(2026, 11, 2), null, TaskPriority.None, now);
        TaskItem second = new(Guid.NewGuid(), "Second", null, new DateOnly(2026, 11, 2), null, TaskPriority.None, now);
        tasks.Tasks.AddRange([first, second]);
        TimelineService service = new(
            timelines,
            tasks,
            new EmptyPlanRepository(),
            new TestDayBoundarySettings(),
            new MutableTimeProvider(now));

        TimelineEntrySummary created = await service.CreateAsync(new CreateTimelineEntryRequest(
            "Shared work",
            null,
            [first.Id, second.Id],
            new DateOnly(2026, 11, 2),
            new DateTime(2026, 11, 2, 9, 0, 0),
            new DateTime(2026, 11, 2, 10, 0, 0)));
        await service.RemoveTaskAsync(created.Id, first.Id);

        TimelineEntrySummary updated = await service.GetAsync(created.Id);
        Assert.Equal([second.Id], updated.LinkedTasks.Select(task => task.Id));
        Assert.Empty(await service.ListForTaskAsync(first.Id));
        Assert.Equal(created.Id, Assert.Single(await service.ListForTaskAsync(second.Id)).Id);
    }

    [Fact]
    public async Task NextDayTimesRemainOwnedByTheirRecordDate()
    {
        InMemoryTimelineRepository timelines = new();
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));
        TimelineService service = CreateService(
            timelines,
            clock,
            new TestDayBoundarySettings(DayEndTime.SixAm));
        TimelineEntrySummary created = await service.CreateAsync(new CreateTimelineEntryRequest(
            "After midnight",
            null,
            [],
            new DateOnly(2026, 11, 2),
            new DateTime(2026, 11, 3, 1, 1, 0),
            new DateTime(2026, 11, 3, 2, 30, 0)));

        TimelineEntrySummary ownedByToday = Assert.Single((await service.GetTodayAsync()).Entries);
        clock.UtcNow = clock.UtcNow.AddDays(1);

        Assert.Equal(new DateOnly(2026, 11, 2), ownedByToday.RecordDate);
        Assert.Equal(TimeSpan.FromMinutes(89), created.DayDuration);
        Assert.Equal(new DateTime(2026, 11, 3, 1, 1, 0), ownedByToday.StartLocal);
        Assert.Equal(TimeSpan.FromMinutes(89), ownedByToday.DayDuration);
        Assert.Empty((await service.GetTodayAsync()).Entries);
    }

    [Fact]
    public async Task TimerStartedBeforeSixAmBelongsToPreviousBusinessDay()
    {
        InMemoryTimelineRepository timelines = new();
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 3, 3, 0, 0, TimeSpan.Zero));
        TimelineService service = CreateService(
            timelines,
            clock,
            new TestDayBoundarySettings(DayEndTime.SixAm));

        TimelineEntrySummary started = await service.StartAsync(new StartTimelineEntryRequest("Late work", null, []));
        TimelineDaySummary today = await service.GetTodayAsync();

        Assert.Equal(new DateOnly(2026, 11, 2), today.Date);
        Assert.Equal(new DateOnly(2026, 11, 2), started.RecordDate);
        Assert.Equal(started.Id, Assert.Single(today.Entries).Id);
    }

    [Fact]
    public async Task ChangingBoundaryReassignsEarlyMorningRecordWithoutEditingIt()
    {
        InMemoryTimelineRepository timelines = new();
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero));
        TestDayBoundarySettings settings = new(DayEndTime.Midnight);
        TimelineService service = CreateService(timelines, clock, settings);
        TimelineEntry entry = new(
            Guid.NewGuid(),
            "Early morning",
            null,
            [],
            new DateTimeOffset(2026, 11, 3, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 3, 2, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 3, 1, 0, 0, TimeSpan.Zero),
            TimelineEntrySource.Manual,
            new DateOnly(2026, 11, 3));
        timelines.Entries.Add(entry);

        Assert.Equal(entry.Id, Assert.Single((await service.GetTodayAsync()).Entries).Id);

        settings.SetDayEndTime(DayEndTime.SixAm);

        Assert.Empty((await service.GetTodayAsync()).Entries);
        Assert.Equal(new DateOnly(2026, 11, 2), (await service.GetAsync(entry.Id)).RecordDate);
    }

    [Fact]
    public async Task ClosedDayMakesTimelineEntriesReadOnly()
    {
        DateOnly planDate = new(2026, 11, 2);
        DateTimeOffset now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        DailyPlan plan = new(Guid.NewGuid(), planDate, now);
        plan.Close(null, new HashSet<Guid>(), now);
        InMemoryTimelineRepository timelines = new();
        TimelineEntry entry = new(
            Guid.NewGuid(),
            "Read only",
            null,
            [],
            now.AddHours(-2),
            now.AddHours(-1),
            now,
            TimelineEntrySource.Manual,
            planDate);
        timelines.Entries.Add(entry);
        TimelineService service = new(
            timelines,
            new EmptyTaskRepository(),
            new EmptyPlanRepository(plan),
            new TestDayBoundarySettings(),
            new MutableTimeProvider(now));

        TimelineEntrySummary summary = await service.GetAsync(entry.Id);

        Assert.True(summary.IsReadOnly);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(entry.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(CreateRequest(13, 0, 14, 0)));
    }

    private static CreateTimelineEntryRequest CreateRequest(int startHour, int startMinute, int endHour, int endMinute) => new(
        "Manual entry",
        null,
        [],
        new DateOnly(2026, 11, 2),
        new DateTime(2026, 11, 2, startHour, startMinute, 0),
        new DateTime(2026, 11, 2, endHour, endMinute, 0));

    private static TimelineService CreateService(InMemoryTimelineRepository timelines, DateTimeOffset utcNow) =>
        CreateService(timelines, new MutableTimeProvider(utcNow));

    private static TimelineService CreateService(InMemoryTimelineRepository timelines, MutableTimeProvider clock) =>
        CreateService(timelines, clock, new TestDayBoundarySettings());

    private static TimelineService CreateService(
        InMemoryTimelineRepository timelines,
        MutableTimeProvider clock,
        IDayBoundarySettings dayBoundarySettings) =>
        new(timelines, new EmptyTaskRepository(), new EmptyPlanRepository(), dayBoundarySettings, clock);

    private sealed class EmptyPlanRepository(DailyPlan? plan = null) : IDailyPlanRepository
    {
        public Task<DailyPlan?> FindAsync(DateOnly planDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(plan?.Date == planDate ? plan : null);

        public Task<DailyPlan> GetOrCreateAsync(
            DateOnly planDate,
            DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(plan?.Date == planDate ? plan : new DailyPlan(Guid.NewGuid(), planDate, createdAtUtc));

        public Task UpdateAsync(DailyPlan plan, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryTimelineRepository : ITimelineEntryRepository
    {
        public List<TimelineEntry> Entries { get; } = [];

        public Task AddAsync(TimelineEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<TimelineEntry?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Entries.SingleOrDefault(entry => entry.Id == id && !entry.IsDeleted));

        public Task<TimelineEntry?> FindRunningAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Entries.SingleOrDefault(entry => !entry.IsDeleted && entry.IsRunning));

        public Task<IReadOnlyList<TimelineEntry>> ListIntersectingAsync(
            DateTimeOffset startAtUtc,
            DateTimeOffset endAtUtc,
            CancellationToken cancellationToken = default)
        {
            TimelineEntry[] result = Entries
                .Where(entry => !entry.IsDeleted && entry.StartAtUtc < endAtUtc &&
                                (!entry.EndAtUtc.HasValue || entry.EndAtUtc > startAtUtc))
                .OrderBy(entry => entry.StartAtUtc)
                .ToArray();
            return Task.FromResult<IReadOnlyList<TimelineEntry>>(result);
        }

        public Task<IReadOnlyList<TimelineEntry>> ListForDateAsync(
            DateOnly recordDate,
            DateTimeOffset dayStartUtc,
            DateTimeOffset dayEndUtc,
            CancellationToken cancellationToken = default)
        {
            TimelineEntry[] result = Entries
                .Where(entry => !entry.IsDeleted &&
                                entry.StartAtUtc < dayEndUtc &&
                                (!entry.EndAtUtc.HasValue || entry.EndAtUtc > dayStartUtc))
                .OrderBy(entry => entry.StartAtUtc)
                .ToArray();
            return Task.FromResult<IReadOnlyList<TimelineEntry>>(result);
        }

        public Task<IReadOnlyList<TimelineEntry>> ListByTaskAsync(
            Guid taskId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TimelineEntry>>(
                Entries.Where(entry => !entry.IsDeleted && entry.ActiveTaskLinks.Any(link => link.TaskItemId == taskId)).ToArray());

        public Task UpdateAsync(TimelineEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class EmptyTaskRepository : ITaskItemRepository
    {
        public Task AddAsync(TaskItem task, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TaskItem?> FindAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<TaskItem?>(null);

        public Task<IReadOnlyList<TaskItem>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TaskItem>>([]);

        public Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryTaskRepository : ITaskItemRepository
    {
        public List<TaskItem> Tasks { get; } = [];

        public Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
        {
            Tasks.Add(task);
            return Task.CompletedTask;
        }

        public Task<TaskItem?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Tasks.SingleOrDefault(task => task.Id == id && !task.IsDeleted));

        public Task<IReadOnlyList<TaskItem>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TaskItem>>(Tasks.Where(task => !task.IsDeleted).ToArray());

        public Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow, TimeZoneInfo? localTimeZone = null) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override TimeZoneInfo LocalTimeZone { get; } = localTimeZone ?? TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
