using TimTicker.Application.Tasks;
using TimTicker.Domain.Timeline;

namespace TimTicker.Application.Timeline;

public sealed class TimelineService(
    ITimelineEntryRepository repository,
    ITaskItemRepository taskRepository,
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
            summaries.Add(await ToSummaryAsync(
                entry,
                today,
                cancellationToken,
                clipToDay: entry.RecordDate is null).ConfigureAwait(false));
        }

        TimeSpan totalDuration = summaries.Aggregate(TimeSpan.Zero, (total, entry) => total + entry.DayDuration);
        return new TimelineDaySummary(today, summaries, totalDuration);
    }

    public async Task<TimelineEntrySummary> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TimelineEntry entry = await GetEntryAsync(id, cancellationToken).ConfigureAwait(false);
        DateOnly displayDate = DateOnly.FromDateTime(ConvertUtcToLocal(entry.StartAtUtc));
        return await ToSummaryAsync(entry, displayDate, cancellationToken, clipToDay: false).ConfigureAwait(false);
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
            DateOnly displayDate = DateOnly.FromDateTime(ConvertUtcToLocal(entry.StartAtUtc));
            summaries.Add(await ToSummaryAsync(entry, displayDate, cancellationToken, clipToDay: false).ConfigureAwait(false));
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
            LocalToday());
        await repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(entry, entry.RecordDate!.Value, cancellationToken, clipToDay: false).ConfigureAwait(false);
    }

    public async Task<TimelineEntrySummary> StopCurrentAsync(CancellationToken cancellationToken = default)
    {
        TimelineEntry entry = await repository.FindRunningAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("There is no running timeline entry.");
        DateTimeOffset now = UtcNow();
        await EnsureNoOverlapAsync(entry.StartAtUtc, now, entry.Id, cancellationToken).ConfigureAwait(false);
        entry.Stop(now);
        await repository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        DateOnly displayDate = entry.RecordDate ?? LocalToday();
        return await ToSummaryAsync(entry, displayDate, cancellationToken, clipToDay: entry.RecordDate is null).ConfigureAwait(false);
    }

    public async Task<TimelineEntrySummary> CreateAsync(
        CreateTimelineEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
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
        return await ToSummaryAsync(entry, request.RecordDate, cancellationToken, clipToDay: false).ConfigureAwait(false);
    }

    public async Task<TimelineEntrySummary> UpdateAsync(
        UpdateTimelineEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        TimelineEntry entry = await GetEntryAsync(request.Id, cancellationToken).ConfigureAwait(false);
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
        return await ToSummaryAsync(entry, request.RecordDate, cancellationToken, clipToDay: false).ConfigureAwait(false);
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
        entry.RemoveTask(taskId, UtcNow());
        await repository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        TimelineEntry entry = await GetEntryAsync(id, cancellationToken).ConfigureAwait(false);
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
        CancellationToken cancellationToken,
        bool clipToDay = true)
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

        return new TimelineEntrySummary(
            entry.Id,
            entry.Title,
            entry.Description,
            entry.Category,
            linkedTasks,
            entry.RecordDate ?? DateOnly.FromDateTime(ConvertUtcToLocal(entry.StartAtUtc)),
            entry.StartAtUtc,
            entry.EndAtUtc,
            ConvertUtcToLocal(entry.StartAtUtc),
            entry.EndAtUtc.HasValue ? ConvertUtcToLocal(entry.EndAtUtc.Value) : null,
            dayDuration,
            entry.IsRunning,
            entry.Source);
    }

    private (DateTimeOffset Start, DateTimeOffset End) GetUtcDayBounds(DateOnly localDate)
    {
        DateTime startLocal = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        DateTime endLocal = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (ConvertLocalToUtc(startLocal, nameof(localDate)), ConvertLocalToUtc(endLocal, nameof(localDate)));
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

    private DateOnly LocalToday() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
}
