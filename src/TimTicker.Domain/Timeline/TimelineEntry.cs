namespace TimTicker.Domain.Timeline;

public sealed class TimelineEntry
{
    public const int MaximumTitleLength = 200;
    public const int MaximumDescriptionLength = 4_000;
    public const int MaximumCategoryLength = 100;

    private readonly List<TimelineEntryTaskLink> _taskLinks = [];

    private TimelineEntry()
    {
    }

    public TimelineEntry(
        Guid id,
        string title,
        string? description,
        string? category,
        IEnumerable<Guid> taskItemIds,
        DateTimeOffset startAtUtc,
        DateTimeOffset? endAtUtc,
        DateTimeOffset createdAtUtc,
        TimelineEntrySource source = TimelineEntrySource.Manual)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The timeline entry identifier must not be empty.", nameof(id));
        }

        Id = id;
        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        DateTimeOffset normalizedCreatedAt = RequireUtc(createdAtUtc, nameof(createdAtUtc));
        ApplyDetails(title, description, category, startAtUtc, endAtUtc);
        SynchronizeTasks(taskItemIds, normalizedCreatedAt);
        Source = source;
        CreatedAtUtc = normalizedCreatedAt;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Category { get; private set; }

    public IReadOnlyList<TimelineEntryTaskLink> TaskLinks => _taskLinks;

    public IReadOnlyList<TimelineEntryTaskLink> ActiveTaskLinks => _taskLinks.Where(link => link.IsActive).ToArray();

    public DateTimeOffset StartAtUtc { get; private set; }

    public DateTimeOffset? EndAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public TimelineEntrySource Source { get; private set; }

    public bool IsRunning => EndAtUtc is null;

    public void Update(
        string title,
        string? description,
        string? category,
        IEnumerable<Guid> taskItemIds,
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        if (IsRunning)
        {
            throw new InvalidOperationException("A running timeline entry must be stopped before it can be edited.");
        }

        DateTimeOffset normalizedUpdatedAt = RequireUtc(updatedAtUtc, nameof(updatedAtUtc));
        ApplyDetails(title, description, category, startAtUtc, endAtUtc);
        SynchronizeTasks(taskItemIds, normalizedUpdatedAt);
        UpdatedAtUtc = normalizedUpdatedAt;
    }

    public void RemoveTask(Guid taskItemId, DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        DateTimeOffset normalizedUpdatedAt = RequireUtc(updatedAtUtc, nameof(updatedAtUtc));
        TimelineEntryTaskLink link = _taskLinks.SingleOrDefault(link => link.TaskItemId == taskItemId && link.IsActive)
            ?? throw new KeyNotFoundException("The task is not linked to this timeline entry.");
        link.Remove(normalizedUpdatedAt);
        UpdatedAtUtc = normalizedUpdatedAt;
    }

    public void Stop(DateTimeOffset endAtUtc)
    {
        EnsureActive();
        if (!IsRunning)
        {
            throw new InvalidOperationException("Only a running timeline entry can be stopped.");
        }

        DateTimeOffset normalizedEnd = RequireUtc(endAtUtc, nameof(endAtUtc));
        if (normalizedEnd <= StartAtUtc)
        {
            throw new ArgumentException("The end time must be later than the start time.", nameof(endAtUtc));
        }

        EndAtUtc = normalizedEnd;
        UpdatedAtUtc = normalizedEnd;
    }

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        EnsureActive();
        UpdatedAtUtc = RequireUtc(deletedAtUtc, nameof(deletedAtUtc));
        IsDeleted = true;
    }

    private void ApplyDetails(
        string title,
        string? description,
        string? category,
        DateTimeOffset startAtUtc,
        DateTimeOffset? endAtUtc)
    {
        ArgumentNullException.ThrowIfNull(title);
        string normalizedTitle = title.Trim();
        if (normalizedTitle.Length == 0 || normalizedTitle.Length > MaximumTitleLength)
        {
            throw new ArgumentException($"A title between 1 and {MaximumTitleLength} characters is required.", nameof(title));
        }

        string? normalizedDescription = NormalizeOptional(description, MaximumDescriptionLength, nameof(description));
        string? normalizedCategory = NormalizeOptional(category, MaximumCategoryLength, nameof(category));
        DateTimeOffset normalizedStart = RequireUtc(startAtUtc, nameof(startAtUtc));
        DateTimeOffset? normalizedEnd = endAtUtc.HasValue ? RequireUtc(endAtUtc.Value, nameof(endAtUtc)) : null;
        if (normalizedEnd <= normalizedStart)
        {
            throw new ArgumentException("The end time must be later than the start time.", nameof(endAtUtc));
        }

        Title = normalizedTitle;
        Description = normalizedDescription;
        Category = normalizedCategory;
        StartAtUtc = normalizedStart;
        EndAtUtc = normalizedEnd;
    }

    private void SynchronizeTasks(IEnumerable<Guid> taskItemIds, DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(taskItemIds);
        Guid[] normalizedIds = taskItemIds.Distinct().ToArray();
        if (normalizedIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Task identifiers must not be empty.", nameof(taskItemIds));
        }

        foreach (TimelineEntryTaskLink link in _taskLinks.Where(link => link.IsActive && !normalizedIds.Contains(link.TaskItemId)))
        {
            link.Remove(changedAtUtc);
        }

        foreach (Guid taskItemId in normalizedIds)
        {
            TimelineEntryTaskLink? existing = _taskLinks.SingleOrDefault(link => link.TaskItemId == taskItemId);
            if (existing is null)
            {
                _taskLinks.Add(new TimelineEntryTaskLink(Id, taskItemId, changedAtUtc));
            }
            else if (!existing.IsActive)
            {
                existing.Restore(changedAtUtc);
            }
        }
    }

    private void EnsureActive()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted timeline entry cannot be changed.");
        }
    }

    private static string? NormalizeOptional(string? value, int maximumLength, string parameterName)
    {
        string? normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maximumLength)
        {
            throw new ArgumentException($"The value must be {maximumLength} characters or fewer.", parameterName);
        }

        return normalized;
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timeline timestamps must use UTC.", parameterName);
        }

        return value;
    }
}
