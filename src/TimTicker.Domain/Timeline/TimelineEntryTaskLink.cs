namespace TimTicker.Domain.Timeline;

public sealed class TimelineEntryTaskLink
{
    private TimelineEntryTaskLink()
    {
    }

    internal TimelineEntryTaskLink(
        Guid timelineEntryId,
        Guid taskItemId,
        DateTimeOffset addedAtUtc)
    {
        if (timelineEntryId == Guid.Empty)
        {
            throw new ArgumentException("The timeline entry identifier must not be empty.", nameof(timelineEntryId));
        }

        if (taskItemId == Guid.Empty)
        {
            throw new ArgumentException("The task identifier must not be empty.", nameof(taskItemId));
        }

        TimelineEntryId = timelineEntryId;
        TaskItemId = taskItemId;
        AddedAtUtc = RequireUtc(addedAtUtc, nameof(addedAtUtc));
    }

    public Guid TimelineEntryId { get; private set; }

    public Guid TaskItemId { get; private set; }

    public DateTimeOffset AddedAtUtc { get; private set; }

    public DateTimeOffset? RemovedAtUtc { get; private set; }

    public bool IsActive => RemovedAtUtc is null;

    internal void Restore(DateTimeOffset restoredAtUtc)
    {
        AddedAtUtc = RequireUtc(restoredAtUtc, nameof(restoredAtUtc));
        RemovedAtUtc = null;
    }

    internal void Remove(DateTimeOffset removedAtUtc)
    {
        RemovedAtUtc = RequireUtc(removedAtUtc, nameof(removedAtUtc));
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
