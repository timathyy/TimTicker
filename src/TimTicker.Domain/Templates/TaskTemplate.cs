using TimTicker.Domain.Tasks;

namespace TimTicker.Domain.Templates;

public sealed class TaskTemplate
{
    public const int MaximumNameLength = 80;
    public const int MaximumTitlePatternLength = 200;

    private TaskTemplate()
    {
    }

    public TaskTemplate(
        Guid id,
        Guid folderId,
        string name,
        string titlePattern,
        string? description,
        TaskPriority priority,
        TaskTemplateSchedule schedule,
        int repeatInterval,
        DateOnly startDate,
        bool isEnabled,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The template identifier must not be empty.", nameof(id));
        }

        if (folderId == Guid.Empty)
        {
            throw new ArgumentException("The template folder identifier must not be empty.", nameof(folderId));
        }

        Id = id;
        FolderId = folderId;
        ApplyDetails(name, titlePattern, description, priority, schedule, repeatInterval, startDate, isEnabled);
        CreatedAtUtc = RequireUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
        NextSequenceNumber = 1;
    }

    public Guid Id { get; private set; }

    public Guid FolderId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string TitlePattern { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public TaskPriority Priority { get; private set; }

    public TaskTemplateSchedule Schedule { get; private set; }

    public int RepeatInterval { get; private set; }

    public DateOnly StartDate { get; private set; }

    public bool IsEnabled { get; private set; }

    public int NextSequenceNumber { get; private set; }

    public DateOnly? LastGeneratedDate { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted template cannot be changed.");
        }

        IsDeleted = true;
        IsEnabled = false;
        UpdatedAtUtc = RequireUtc(deletedAtUtc, nameof(deletedAtUtc));
    }

    public TaskItem GenerateManual(DateOnly generationDate, DateTimeOffset generatedAtUtc) =>
        Generate(generationDate, null, generatedAtUtc);

    public TaskItem GenerateScheduled(DateOnly occurrenceDate, DateTimeOffset generatedAtUtc)
    {
        if (Schedule == TaskTemplateSchedule.Manual || !IsEnabled)
        {
            throw new InvalidOperationException("This template is not enabled for automatic generation.");
        }

        DateOnly expectedDate = GetNextOccurrenceDate();
        if (occurrenceDate != expectedDate)
        {
            throw new InvalidOperationException("Scheduled tasks must be generated in occurrence order.");
        }

        TaskItem task = Generate(occurrenceDate, occurrenceDate, generatedAtUtc);
        LastGeneratedDate = occurrenceDate;
        return task;
    }

    public DateOnly GetNextOccurrenceDate()
    {
        if (Schedule == TaskTemplateSchedule.Manual)
        {
            throw new InvalidOperationException("Manual templates do not have scheduled occurrences.");
        }

        if (!LastGeneratedDate.HasValue)
        {
            return StartDate;
        }

        return Schedule switch
        {
            TaskTemplateSchedule.Daily => LastGeneratedDate.Value.AddDays(RepeatInterval),
            TaskTemplateSchedule.Weekly => LastGeneratedDate.Value.AddDays(checked(7 * RepeatInterval)),
            TaskTemplateSchedule.Monthly => GetNextMonthlyOccurrenceDate(LastGeneratedDate.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(Schedule)),
        };
    }

    private DateOnly GetNextMonthlyOccurrenceDate(DateOnly lastOccurrenceDate)
    {
        int elapsedMonths = checked(((lastOccurrenceDate.Year - StartDate.Year) * 12) + lastOccurrenceDate.Month - StartDate.Month);
        int nextInterval = checked((elapsedMonths / RepeatInterval) + 1);
        return StartDate.AddMonths(checked(nextInterval * RepeatInterval));
    }

    private TaskItem Generate(DateOnly titleDate, DateOnly? occurrenceDate, DateTimeOffset generatedAtUtc)
    {
        DateTimeOffset utcNow = RequireUtc(generatedAtUtc, nameof(generatedAtUtc));
        int sequence = NextSequenceNumber;
        string title = TaskTitlePatternFormatter.Format(TitlePattern, titleDate, sequence);
        TaskItem task = new(
            Guid.NewGuid(),
            title,
            Description,
            titleDate,
            null,
            Priority,
            utcNow,
            FolderId,
            Id,
            sequence,
            occurrenceDate);
        NextSequenceNumber = checked(sequence + 1);
        UpdatedAtUtc = utcNow;
        return task;
    }

    private void ApplyDetails(
        string name,
        string titlePattern,
        string? description,
        TaskPriority priority,
        TaskTemplateSchedule schedule,
        int repeatInterval,
        DateOnly startDate,
        bool isEnabled)
    {
        Name = NormalizeRequired(name, MaximumNameLength, "template name", nameof(name));
        TitlePattern = NormalizeRequired(titlePattern, MaximumTitlePatternLength, "title pattern", nameof(titlePattern));
        string longestPossibleTitle = TaskTitlePatternFormatter.Format(
            TitlePattern,
            new DateOnly(9999, 12, 31),
            int.MaxValue);
        if (longestPossibleTitle.Length > TaskItem.MaximumTitleLength)
        {
            throw new ArgumentException(
                $"The expanded task title must be {TaskItem.MaximumTitleLength} characters or fewer.",
                nameof(titlePattern));
        }

        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (Description?.Length > TaskItem.MaximumDescriptionLength)
        {
            throw new ArgumentException($"A task description must be {TaskItem.MaximumDescriptionLength} characters or fewer.", nameof(description));
        }

        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        if (!Enum.IsDefined(schedule))
        {
            throw new ArgumentOutOfRangeException(nameof(schedule));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repeatInterval);

        if (startDate == default)
        {
            throw new ArgumentException("The start date must not be empty.", nameof(startDate));
        }

        Priority = priority;
        Schedule = schedule;
        RepeatInterval = repeatInterval;
        StartDate = startDate;
        IsEnabled = schedule != TaskTemplateSchedule.Manual && isEnabled;
    }

    private static string NormalizeRequired(string value, int maximumLength, string label, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value);
        string normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > maximumLength)
        {
            throw new ArgumentException($"The {label} must contain 1 to {maximumLength} characters.", parameterName);
        }

        return normalized;
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Template timestamps must use UTC.", parameterName);
        }

        return value;
    }
}
