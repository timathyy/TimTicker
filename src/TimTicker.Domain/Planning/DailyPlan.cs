namespace TimTicker.Domain.Planning;

public sealed class DailyPlan
{
    private readonly List<DailyPlanItem> _items = [];

    private DailyPlan()
    {
    }

    public DailyPlan(Guid id, DateOnly date, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The daily plan identifier must not be empty.", nameof(id));
        }

        if (date == default)
        {
            throw new ArgumentException("The plan date must not be empty.", nameof(date));
        }

        Id = id;
        Date = date;
        CreatedAtUtc = RequireUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public DateOnly Date { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public string? ReviewText { get; private set; }

    public decimal? FinalCompletionRate { get; private set; }

    public IReadOnlyList<DailyPlanItem> Items => _items;

    public IReadOnlyList<DailyPlanItem> ActiveItems => _items
        .Where(item => item.IsActive)
        .OrderBy(item => item.SortOrder)
        .ToArray();

    public void AddTask(Guid taskItemId, DateTimeOffset addedAtUtc)
    {
        if (taskItemId == Guid.Empty)
        {
            throw new ArgumentException("The task identifier must not be empty.", nameof(taskItemId));
        }

        DailyPlanItem? existing = _items.SingleOrDefault(item => item.TaskItemId == taskItemId);
        if (existing?.IsActive == true)
        {
            throw new InvalidOperationException("The task is already in this daily plan.");
        }

        int sortOrder = ActiveItems.Count;
        if (existing is null)
        {
            _items.Add(new DailyPlanItem(Guid.NewGuid(), Id, taskItemId, sortOrder, addedAtUtc));
        }
        else
        {
            existing.Restore(sortOrder, addedAtUtc);
        }

        Touch(addedAtUtc);
    }

    public void RemoveTask(Guid taskItemId, DateTimeOffset removedAtUtc)
    {
        DailyPlanItem item = GetActiveItem(taskItemId);
        item.Remove(removedAtUtc);
        NormalizeSortOrder();
        Touch(removedAtUtc);
    }

    public void MoveTask(Guid taskItemId, int newIndex, DateTimeOffset updatedAtUtc)
    {
        List<DailyPlanItem> activeItems = ActiveItems.ToList();
        DailyPlanItem item = GetActiveItem(taskItemId);
        int oldIndex = activeItems.IndexOf(item);
        if (newIndex < 0 || newIndex >= activeItems.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(newIndex));
        }

        if (oldIndex == newIndex)
        {
            return;
        }

        activeItems.RemoveAt(oldIndex);
        activeItems.Insert(newIndex, item);
        for (int index = 0; index < activeItems.Count; index++)
        {
            activeItems[index].SetSortOrder(index);
        }

        Touch(updatedAtUtc);
    }

    private DailyPlanItem GetActiveItem(Guid taskItemId) => _items.SingleOrDefault(item => item.TaskItemId == taskItemId && item.IsActive)
        ?? throw new KeyNotFoundException("The task is not in this daily plan.");

    private void NormalizeSortOrder()
    {
        IReadOnlyList<DailyPlanItem> activeItems = ActiveItems;
        for (int index = 0; index < activeItems.Count; index++)
        {
            activeItems[index].SetSortOrder(index);
        }
    }

    private void Touch(DateTimeOffset updatedAtUtc) => UpdatedAtUtc = RequireUtc(updatedAtUtc, nameof(updatedAtUtc));

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Plan timestamps must use UTC.", parameterName);
        }

        return value;
    }
}
