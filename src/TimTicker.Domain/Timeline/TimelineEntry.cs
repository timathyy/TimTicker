namespace TimTicker.Domain.Timeline;

public sealed class TimelineEntry
{
    public const int MaximumTitleLength = 200;
    public const int MaximumDescriptionLength = 4_000;
    public const int MaximumCategoryLength = 100;

    private TimelineEntry()
    {
    }

    public TimelineEntry(
        Guid id,
        string title,
        string? description,
        string? category,
        Guid? taskItemId,
        DateTimeOffset startAtUtc,
        DateTimeOffset? endAtUtc,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The timeline entry identifier must not be empty.", nameof(id));
        }

        Id = id;
        ApplyDetails(title, description, category, taskItemId, startAtUtc, endAtUtc);
        CreatedAtUtc = RequireUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Category { get; private set; }

    public Guid? TaskItemId { get; private set; }

    public DateTimeOffset StartAtUtc { get; private set; }

    public DateTimeOffset? EndAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public bool IsRunning => EndAtUtc is null;

    public void Update(
        string title,
        string? description,
        string? category,
        Guid? taskItemId,
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        if (IsRunning)
        {
            throw new InvalidOperationException("A running timeline entry must be stopped before it can be edited.");
        }

        ApplyDetails(title, description, category, taskItemId, startAtUtc, endAtUtc);
        UpdatedAtUtc = RequireUtc(updatedAtUtc, nameof(updatedAtUtc));
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
        Guid? taskItemId,
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
        if (taskItemId == Guid.Empty)
        {
            throw new ArgumentException("The task identifier must not be empty.", nameof(taskItemId));
        }

        DateTimeOffset normalizedStart = RequireUtc(startAtUtc, nameof(startAtUtc));
        DateTimeOffset? normalizedEnd = endAtUtc.HasValue ? RequireUtc(endAtUtc.Value, nameof(endAtUtc)) : null;
        if (normalizedEnd <= normalizedStart)
        {
            throw new ArgumentException("The end time must be later than the start time.", nameof(endAtUtc));
        }

        Title = normalizedTitle;
        Description = normalizedDescription;
        Category = normalizedCategory;
        TaskItemId = taskItemId;
        StartAtUtc = normalizedStart;
        EndAtUtc = normalizedEnd;
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
