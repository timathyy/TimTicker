using TimTicker.Application.Tasks;
using TimTicker.Application.Planning;
using TimTicker.Application.Settings;
using TimTicker.Domain.Timeline;

namespace TimTicker.Application.Timeline;

public sealed class TimelineService(
    ITimelineEntryRepository repository,
    ITaskItemRepository taskRepository,
    IDailyPlanRepository planRepository,
    IDayBoundarySettings dayBoundarySettings,
    TimeProvider timeProvider) : ITimelineService
{
    public async Task<TimelineDaySummary> GetTodayAsync(CancellationToken cancellationToken = default)
    {
        DateOnly today = LocalToday();
        (DateTimeOffset dayStartUtc, DateTimeOffset dayEndUtc) = GetUtcDayBounds(today);
        IReadOnlyList<TimelineEntry> entries = await repository
            .ListForDateAsync(today, dayStartUtc, dayEndUtc, cancellationToken)
            .ConfigureAwait(false);
        List<TimelineEntrySummary> summaries = [];
        foreach (TimelineEntry entry in entries.OrderBy(entry => entry.StartAtUtc))
        {
            summaries.Add(await ToSummaryAsync(entry, today, clipToDay: true, cancellationToken).ConfigureAwait(false));
        }

        TimeSpan totalDuration = summaries.Aggregate(TimeSpan.Zero, (total, entry) => total + entry.DayDuration);
        return new TimelineDaySummary(today, summaries, totalDuration);
    }

    public async Task<TimelineEntrySummary> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TimelineEntry entry = await GetEntryAsync(id, cancellationToken).ConfigureAwait(false);
        DateOnly displayDate = GetRecordDate(entry);
        return await ToSummaryAsync(entry, displayDate, clipToDay: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TimelineEntrySummary>> ListForTaskAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("The task identifier must not be empty.", nameof(taskId));
        }

        IReadOnlyList<TimelineEntry> entries = await repository.ListByTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        List<TimelineEntrySummary> summaries = [];
        foreach (TimelineEntry entry in entries)
        {
            DateOnly displayDate = GetRecordDate(entry);
            summaries.Add(await ToSummaryAsync(entry, displayDate, clipToDay: false, cancellationToken).ConfigureAwait(false));
        }

        return summaries;
    }

    public async Task<IReadOnlyList<TimelineTaskOption>> ListTaskOptionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Domain.Tasks.TaskItem> tasks = await taskRepository.ListAsync(
            new TaskQuery(null, null, TaskSortOrder.CreatedAtDescending),
            cancellationToken).ConfigureAwait(false);
        return tasks.Select(task => new TimelineTaskOption(task.Id, task.Title)).ToArray();
    }

    public async Task<TimelineEntrySummary> StartAsync(
        StartTimelineEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        DateOnly recordDate = LocalToday();
        await EnsureDayOpenAsync(recordDate, cancellationToken).ConfigureAwait(false);
        await EnsureTasksExistAsync(request.TaskItemIds, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = UtcNow();
        await EnsureNoOverlapAsync(now, DateTimeOffset.MaxValue, null, cancellationToken).ConfigureAwait(false);
        TimelineEntry entry = new(
            Guid.NewGuid(),
            request.Title,
            request.Description,
            request.Category,
            request.TaskItemIds,
            now,
            null,
            now,
            TimelineEntrySource.Timer,
            recordDate);
        await repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(entry, recordDate, clipToDay: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TimelineEntrySummary> StopCurrentAsync(CancellationToken cancellationToken = default)
    {
        TimelineEntry entry = await repository.FindRunningAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("There is no running timeline entry.");
        await EnsureDayOpenAsync(GetRecordDate(entry), cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = UtcNow();
        await EnsureNoOverlapAsync(entry.StartAtUtc, now, entry.Id, cancellationToken).ConfigureAwait(false);
        entry.Stop(now);
        await repository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        DateOnly displayDate = GetRecordDate(entry);
        return await ToSummaryAsync(entry, displayDate, clipToDay: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TimelineEntrySummary> CreateAsync(
        CreateTimelineEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureRecordDateMatchesBoundary(request.RecordDate, request.StartLocal);
        await EnsureDayOpenAsync(request.RecordDate, cancellationToken).ConfigureAwait(false);
        await EnsureTasksExistAsync(request.TaskItemIds, cancellationToken).ConfigureAwait(false);
        DateTimeOffset startAtUtc = ConvertLocalToUtc(request.StartLocal, nameof(request.StartLocal));
        DateTimeOffset endAtUtc = ConvertLocalToUtc(request.EndLocal, nameof(request.EndLocal));
        await EnsureNoOverlapAsync(startAtUtc, endAtUtc, null, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = UtcNow();
        TimelineEntry entry = new(
            Guid.NewGuid(),
            request.Title,
            request.Description,
            request.Category,
            request.TaskItemIds,
            startAtUtc,
            endAtUtc,
            now,
            TimelineEntrySource.Manual,
            request.RecordDate);
        await repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(entry, request.RecordDate, clipToDay: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TimelineEntrySummary> UpdateAsync(
        UpdateTimelineEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        TimelineEntry entry = await GetEntryAsync(request.Id, cancellationToken).ConfigureAwait(false);
        await EnsureDayOpenAsync(GetRecordDate(entry), cancellationToken).ConfigureAwait(false);
        EnsureRecordDateMatchesBoundary(request.RecordDate, request.StartLocal);
        await EnsureDayOpenAsync(request.RecordDate, cancellationToken).ConfigureAwait(false);
        await EnsureTasksExistAsync(request.TaskItemIds, cancellationToken).ConfigureAwait(false);
        DateTimeOffset startAtUtc = ConvertLocalToUtc(request.StartLocal, nameof(request.StartLocal));
        DateTimeOffset endAtUtc = ConvertLocalToUtc(request.EndLocal, nameof(request.EndLocal));
        await EnsureNoOverlapAsync(startAtUtc, endAtUtc, entry.Id, cancellationToken).ConfigureAwait(false);
        entry.Update(
            request.Title,
            request.Description,
            request.Category,
            request.TaskItemIds,
            request.RecordDate,
            startAtUtc,
            endAtUtc,
            UtcNow());
        await repository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(entry, request.RecordDate, clipToDay: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveTaskAsync(
        Guid entryId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("The task identifier must not be empty.", nameof(taskId));
        }

        TimelineEntry entry = await GetEntryAsync(entryId, cancellationToken).ConfigureAwait(false);
        await EnsureDayOpenAsync(GetRecordDate(entry), cancellationToken).ConfigureAwait(false);
        entry.RemoveTask(taskId, UtcNow());
        await repository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TimelineEntry entry = await GetEntryAsync(id, cancellationToken).ConfigureAwait(false);
        await EnsureDayOpenAsync(GetRecordDate(entry), cancellationToken).ConfigureAwait(false);
        entry.Delete(UtcNow());
        await repository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    private async Task<TimelineEntry> GetEntryAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The timeline entry identifier must not be empty.", nameof(id));
        }

        return await repository.FindAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("The requested timeline entry was not found.");
    }

    private async Task EnsureTasksExistAsync(IEnumerable<Guid> taskItemIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(taskItemIds);
        foreach (Guid taskItemId in taskItemIds.Distinct())
        {
            if (taskItemId == Guid.Empty || await taskRepository.FindAsync(taskItemId, cancellationToken).ConfigureAwait(false) is null)
            {
                throw new KeyNotFoundException("A linked task was not found.");
            }
        }
    }

    private async Task EnsureNoOverlapAsync(
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        if (endAtUtc <= startAtUtc)
        {
            throw new ArgumentException("The end time must be later than the start time.", nameof(endAtUtc));
        }

        IReadOnlyList<TimelineEntry> overlaps = await repository
            .ListIntersectingAsync(startAtUtc, endAtUtc, cancellationToken)
            .ConfigureAwait(false);
        if (overlaps.Any(entry => entry.Id != excludedId))
        {
            throw new TimelineOverlapException();
        }
    }

    private async Task<TimelineEntrySummary> ToSummaryAsync(
        TimelineEntry entry,
        DateOnly displayDate,
        bool clipToDay,
        CancellationToken cancellationToken)
    {
        List<TaskSummary> linkedTasks = [];
        foreach (TimelineEntryTaskLink link in entry.ActiveTaskLinks)
        {
            Domain.Tasks.TaskItem? task = await taskRepository.FindAsync(link.TaskItemId, cancellationToken).ConfigureAwait(false);
            if (task is not null)
            {
                linkedTasks.Add(TaskService.ToSummary(task));
            }
        }

        DateTimeOffset effectiveEnd = entry.EndAtUtc ?? UtcNow();
        DateTimeOffset segmentStart = entry.StartAtUtc;
        DateTimeOffset segmentEnd = effectiveEnd;
        if (clipToDay)
        {
            (DateTimeOffset dayStartUtc, DateTimeOffset dayEndUtc) = GetUtcDayBounds(displayDate);
            segmentStart = entry.StartAtUtc > dayStartUtc ? entry.StartAtUtc : dayStartUtc;
            segmentEnd = effectiveEnd < dayEndUtc ? effectiveEnd : dayEndUtc;
        }

        TimeSpan dayDuration = segmentEnd > segmentStart ? segmentEnd - segmentStart : TimeSpan.Zero;

        DateOnly recordDate = GetRecordDate(entry);
        bool isReadOnly = (await planRepository.FindAsync(recordDate, cancellationToken).ConfigureAwait(false))?.IsClosed == true;
        return new TimelineEntrySummary(
            entry.Id,
            entry.Title,
            entry.Description,
            entry.Category,
            linkedTasks,
            recordDate,
            entry.StartAtUtc,
            entry.EndAtUtc,
            ConvertUtcToLocal(entry.StartAtUtc),
            entry.EndAtUtc.HasValue ? ConvertUtcToLocal(entry.EndAtUtc.Value) : null,
            dayDuration,
            entry.IsRunning,
            entry.Source,
            isReadOnly);
    }

    private (DateTimeOffset Start, DateTimeOffset End) GetUtcDayBounds(DateOnly localDate)
    {
        DayEndTime dayEndTime = dayBoundarySettings.CurrentDayEndTime;
        DateTime startLocal = DayBoundary.GetStartLocal(localDate, dayEndTime);
        DateTime endLocal = DayBoundary.GetEndLocal(localDate, dayEndTime);
        return (ConvertBoundaryLocalToUtc(startLocal), ConvertBoundaryLocalToUtc(endLocal));
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

    private DateTimeOffset ConvertLocalToUtc(DateTime localValue, string parameterName)
    {
        DateTime unspecified = DateTime.SpecifyKind(localValue, DateTimeKind.Unspecified);
        if (timeProvider.LocalTimeZone.IsInvalidTime(unspecified))
        {
            throw new ArgumentException("The local time does not exist in the current time zone.", parameterName);
        }

        DateTime utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, timeProvider.LocalTimeZone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private DateTime ConvertUtcToLocal(DateTimeOffset utcValue) =>
        TimeZoneInfo.ConvertTime(utcValue, timeProvider.LocalTimeZone).DateTime;

    private DateTimeOffset UtcNow() => timeProvider.GetUtcNow();

    private DateOnly LocalToday() => DayBoundary.GetBusinessDate(
        timeProvider.GetLocalNow().DateTime,
        dayBoundarySettings.CurrentDayEndTime);

    private DateOnly GetRecordDate(TimelineEntry entry) =>
        DayBoundary.GetBusinessDate(
            ConvertUtcToLocal(entry.StartAtUtc),
            dayBoundarySettings.CurrentDayEndTime);

    private async Task EnsureDayOpenAsync(DateOnly date, CancellationToken cancellationToken)
    {
        if ((await planRepository.FindAsync(date, cancellationToken).ConfigureAwait(false))?.IsClosed == true)
        {
            throw new InvalidOperationException("The selected day is closed and read-only.");
        }
    }

    private void EnsureRecordDateMatchesBoundary(DateOnly recordDate, DateTime startLocal)
    {
        DateOnly expectedDate = DayBoundary.GetBusinessDate(startLocal, dayBoundarySettings.CurrentDayEndTime);
        if (recordDate != expectedDate)
        {
            throw new ArgumentException("The record date does not match the configured day boundary.", nameof(recordDate));
        }
    }
}
