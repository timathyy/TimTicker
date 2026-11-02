namespace TimTicker.Domain.Planning;

public sealed class DailyPlanItem
{
    private DailyPlanItem()
    {
    }

    internal DailyPlanItem(Guid id, Guid dailyPlanId, Guid taskItemId, int sortOrder, DateTimeOffset addedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The plan item identifier must not be empty.", nameof(id));
        }

        if (dailyPlanId == Guid.Empty)
        {
            throw new ArgumentException("The daily plan identifier must not be empty.", nameof(dailyPlanId));
        }

        if (taskItemId == Guid.Empty)
        {
            throw new ArgumentException("The task identifier must not be empty.", nameof(taskItemId));
        }

        Id = id;
        DailyPlanId = dailyPlanId;
        TaskItemId = taskItemId;
        SortOrder = sortOrder;
        AddedAtUtc = RequireUtc(addedAtUtc, nameof(addedAtUtc));
    }

    public Guid Id { get; private set; }

    public Guid DailyPlanId { get; private set; }

    public Guid TaskItemId { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset AddedAtUtc { get; private set; }

    public DateTimeOffset? RemovedAtUtc { get; private set; }

    public bool? WasCompletedAtClose { get; private set; }

    public bool IsActive => RemovedAtUtc is null;

    internal void Remove(DateTimeOffset removedAtUtc)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("The task is already outside this plan.");
        }

        RemovedAtUtc = RequireUtc(removedAtUtc, nameof(removedAtUtc));
    }

    internal void Restore(int sortOrder, DateTimeOffset addedAtUtc)
    {
        if (IsActive)
        {
            throw new InvalidOperationException("The task is already in this plan.");
        }

        SortOrder = sortOrder;
        AddedAtUtc = RequireUtc(addedAtUtc, nameof(addedAtUtc));
        RemovedAtUtc = null;
    }

    internal void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    internal void CaptureCompletion(bool wasCompleted) => WasCompletedAtClose = wasCompleted;

    internal void ClearCompletionSnapshot() => WasCompletedAtClose = null;

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Plan timestamps must use UTC.", parameterName);
        }

        return value;
    }
}
