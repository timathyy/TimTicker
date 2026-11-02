namespace TimTicker.Domain.Planning;

public sealed class DailyPlan
{
    public const int MaximumReviewLength = 4_000;

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

    public bool IsClosed => ClosedAtUtc.HasValue;

    public IReadOnlyList<DailyPlanItem> Items => _items;

    public IReadOnlyList<DailyPlanItem> ActiveItems => _items
        .Where(item => item.IsActive)
        .OrderBy(item => item.SortOrder)
        .ToArray();

    public void AddTask(Guid taskItemId, DateTimeOffset addedAtUtc)
    {
        EnsureOpen();
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
        EnsureOpen();
        DailyPlanItem item = GetActiveItem(taskItemId);
        item.Remove(removedAtUtc);
        NormalizeSortOrder();
        Touch(removedAtUtc);
    }

    public void MoveTask(Guid taskItemId, int newIndex, DateTimeOffset updatedAtUtc)
    {
        EnsureOpen();
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

    public void Close(
        string? reviewText,
        IReadOnlySet<Guid> completedTaskIds,
        DateTimeOffset closedAtUtc)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(completedTaskIds);
        string? normalizedReview = string.IsNullOrWhiteSpace(reviewText) ? null : reviewText.Trim();
        if (normalizedReview?.Length > MaximumReviewLength)
        {
            throw new ArgumentException($"Review text must be {MaximumReviewLength} characters or fewer.", nameof(reviewText));
        }

        IReadOnlyList<DailyPlanItem> activeItems = ActiveItems;
        if (completedTaskIds.Any(taskId => activeItems.All(item => item.TaskItemId != taskId)))
        {
            throw new ArgumentException("Completion snapshots can only reference active plan tasks.", nameof(completedTaskIds));
        }

        foreach (DailyPlanItem item in activeItems)
        {
            item.CaptureCompletion(completedTaskIds.Contains(item.TaskItemId));
        }

        DateTimeOffset normalizedClosedAt = RequireUtc(closedAtUtc, nameof(closedAtUtc));
        ReviewText = normalizedReview;
        FinalCompletionRate = activeItems.Count == 0
            ? null
            : completedTaskIds.Count / (decimal)activeItems.Count;
        ClosedAtUtc = normalizedClosedAt;
        UpdatedAtUtc = normalizedClosedAt;
    }

    public void Reopen(DateTimeOffset reopenedAtUtc)
    {
        if (!IsClosed)
        {
            throw new InvalidOperationException("Only a closed daily plan can be reopened.");
        }

        foreach (DailyPlanItem item in ActiveItems)
        {
            item.ClearCompletionSnapshot();
        }

        ClosedAtUtc = null;
        FinalCompletionRate = null;
        Touch(reopenedAtUtc);
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

    private void EnsureOpen()
    {
        if (IsClosed)
        {
            throw new InvalidOperationException("A closed daily plan is read-only.");
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
