namespace TimTicker.Domain.Tasks;

public sealed class TaskItem
{
    public const int MaximumTitleLength = 200;
    public const int MaximumDescriptionLength = 4_000;

    private TaskItem()
    {
    }

    public TaskItem(
        Guid id,
        string title,
        string? description,
        DateOnly? dueDate,
        TaskPriority priority,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The task identifier must not be empty.", nameof(id));
        }

        Id = id;
        ApplyDetails(title, description, dueDate, priority);
        CreatedAtUtc = RequireUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
        Status = TaskStatus.Pending;
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public TaskPriority Priority { get; private set; }

    public TaskStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public void Update(string title, string? description, DateOnly? dueDate, TaskPriority priority, DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        ApplyDetails(title, description, dueDate, priority);
        UpdatedAtUtc = RequireUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        EnsureActive();
        EnsureStatus(TaskStatus.Pending, "Only pending tasks can be completed.");
        CompletedAtUtc = RequireUtc(completedAtUtc, nameof(completedAtUtc));
        UpdatedAtUtc = CompletedAtUtc.Value;
        Status = TaskStatus.Completed;
    }

    public void Reopen(DateTimeOffset reopenedAtUtc)
    {
        EnsureActive();
        EnsureStatus(TaskStatus.Completed, "Only completed tasks can be reopened.");
        UpdatedAtUtc = RequireUtc(reopenedAtUtc, nameof(reopenedAtUtc));
        CompletedAtUtc = null;
        Status = TaskStatus.Pending;
    }

    public void Archive(DateTimeOffset archivedAtUtc)
    {
        EnsureActive();
        if (Status == TaskStatus.Archived)
        {
            throw new InvalidOperationException("The task is already archived.");
        }

        UpdatedAtUtc = RequireUtc(archivedAtUtc, nameof(archivedAtUtc));
        Status = TaskStatus.Archived;
    }

    public void Restore(DateTimeOffset restoredAtUtc)
    {
        EnsureActive();
        EnsureStatus(TaskStatus.Archived, "Only archived tasks can be restored.");
        UpdatedAtUtc = RequireUtc(restoredAtUtc, nameof(restoredAtUtc));
        CompletedAtUtc = null;
        Status = TaskStatus.Pending;
    }

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        EnsureActive();
        UpdatedAtUtc = RequireUtc(deletedAtUtc, nameof(deletedAtUtc));
        IsDeleted = true;
    }

    private void ApplyDetails(string title, string? description, DateOnly? dueDate, TaskPriority priority)
    {
        ArgumentNullException.ThrowIfNull(title);
        string normalizedTitle = title.Trim();
        if (normalizedTitle.Length == 0)
        {
            throw new ArgumentException("A task title is required.", nameof(title));
        }

        if (normalizedTitle.Length > MaximumTitleLength)
        {
            throw new ArgumentException($"A task title must be {MaximumTitleLength} characters or fewer.", nameof(title));
        }

        string? normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalizedDescription?.Length > MaximumDescriptionLength)
        {
            throw new ArgumentException($"A task description must be {MaximumDescriptionLength} characters or fewer.", nameof(description));
        }

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        Title = normalizedTitle;
        Description = normalizedDescription;
        DueDate = dueDate;
        Priority = priority;
    }

    private void EnsureActive()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted task cannot be changed.");
        }
    }

    private void EnsureStatus(TaskStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Task timestamps must use UTC.", parameterName);
        }

        return value;
    }
}
