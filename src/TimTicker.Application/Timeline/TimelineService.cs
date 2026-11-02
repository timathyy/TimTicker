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
            .ListIntersectingAsync(dayStartUtc, dayEndUtc, cancellationToken)
            .ConfigureAwait(false);
        List<TimelineEntrySummary> summaries = [];
        foreach (TimelineEntry entry in entries.OrderBy(entry => entry.StartAtUtc))
        {
            summaries.Add(await ToSummaryAsync(entry, today, cancellationToken).ConfigureAwait(false));
        }

        TimeSpan totalDuration = summaries.Aggregate(TimeSpan.Zero, (total, entry) => total + entry.DayDuration);
        return new TimelineDaySummary(today, summaries, totalDuration);
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
        await EnsureTaskExistsAsync(request.TaskItemId, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = UtcNow();
        await EnsureNoOverlapAsync(now, DateTimeOffset.MaxValue, null, cancellationToken).ConfigureAwait(false);
        TimelineEntry entry = new(
            Guid.NewGuid(),
            request.Title,
            request.Description,
            request.Category,
            request.TaskItemId,
            now,
            null,
            now);
        await repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(entry, LocalToday(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<TimelineEntrySummary> StopCurrentAsync(CancellationToken cancellationToken = default)
    {
        TimelineEntry entry = await repository.FindRunningAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("There is no running timeline entry.");
        DateTimeOffset now = UtcNow();
        await EnsureNoOverlapAsync(entry.StartAtUtc, now, entry.Id, cancellationToken).ConfigureAwait(false);
        entry.Stop(now);
        await repository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(entry, LocalToday(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<TimelineEntrySummary> CreateAsync(
        CreateTimelineEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTaskExistsAsync(request.TaskItemId, cancellationToken).ConfigureAwait(false);
        DateTimeOffset startAtUtc = ConvertLocalToUtc(request.StartLocal, nameof(request.StartLocal));
        DateTimeOffset endAtUtc = ConvertLocalToUtc(request.EndLocal, nameof(request.EndLocal));
        await EnsureNoOverlapAsync(startAtUtc, endAtUtc, null, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = UtcNow();
        TimelineEntry entry = new(
            Guid.NewGuid(),
            request.Title,
            request.Description,
            request.Category,
            request.TaskItemId,
            startAtUtc,
            endAtUtc,
            now);
        await repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(entry, LocalToday(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<TimelineEntrySummary> UpdateAsync(
        UpdateTimelineEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        TimelineEntry entry = await GetEntryAsync(request.Id, cancellationToken).ConfigureAwait(false);
        await EnsureTaskExistsAsync(request.TaskItemId, cancellationToken).ConfigureAwait(false);
        DateTimeOffset startAtUtc = ConvertLocalToUtc(request.StartLocal, nameof(request.StartLocal));
        DateTimeOffset endAtUtc = ConvertLocalToUtc(request.EndLocal, nameof(request.EndLocal));
        await EnsureNoOverlapAsync(startAtUtc, endAtUtc, entry.Id, cancellationToken).ConfigureAwait(false);
        entry.Update(
            request.Title,
            request.Description,
            request.Category,
            request.TaskItemId,
            startAtUtc,
            endAtUtc,
            UtcNow());
        await repository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(entry, LocalToday(), cancellationToken).ConfigureAwait(false);
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

    private async Task EnsureTaskExistsAsync(Guid? taskItemId, CancellationToken cancellationToken)
    {
        if (!taskItemId.HasValue)
        {
            return;
        }

        if (taskItemId == Guid.Empty || await taskRepository.FindAsync(taskItemId.Value, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new KeyNotFoundException("The linked task was not found.");
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
        CancellationToken cancellationToken)
    {
        string? taskTitle = null;
        if (entry.TaskItemId.HasValue)
        {
            taskTitle = (await taskRepository.FindAsync(entry.TaskItemId.Value, cancellationToken).ConfigureAwait(false))?.Title;
        }

        (DateTimeOffset dayStartUtc, DateTimeOffset dayEndUtc) = GetUtcDayBounds(displayDate);
        DateTimeOffset effectiveEnd = entry.EndAtUtc ?? UtcNow();
        DateTimeOffset segmentStart = entry.StartAtUtc > dayStartUtc ? entry.StartAtUtc : dayStartUtc;
        DateTimeOffset segmentEnd = effectiveEnd < dayEndUtc ? effectiveEnd : dayEndUtc;
        TimeSpan dayDuration = segmentEnd > segmentStart ? segmentEnd - segmentStart : TimeSpan.Zero;

        return new TimelineEntrySummary(
            entry.Id,
            entry.Title,
            entry.Description,
            entry.Category,
            entry.TaskItemId,
            taskTitle,
            entry.StartAtUtc,
            entry.EndAtUtc,
            ConvertUtcToLocal(entry.StartAtUtc),
            entry.EndAtUtc.HasValue ? ConvertUtcToLocal(entry.EndAtUtc.Value) : null,
            dayDuration,
            entry.IsRunning);
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
