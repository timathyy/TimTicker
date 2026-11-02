using TimTicker.Application.Tasks;
using TimTicker.Application.Settings;
using TimTicker.Domain.Planning;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.Application.Planning;

public sealed class DailyPlanService(
    IDailyPlanRepository planRepository,
    ITaskItemRepository taskRepository,
    ITaskService taskService,
    IDayBoundarySettings dayBoundarySettings,
    TimeProvider timeProvider) : IDailyPlanService
{
    public async Task<DailyPlanSummary> GetTodayAsync(CancellationToken cancellationToken = default)
    {
        DateOnly today = LocalToday();
        DailyPlan plan = await planRepository.GetOrCreateAsync(today, UtcNow(), cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(plan, today, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DailyPlanSummary?> GetForDateAsync(DateOnly planDate, CancellationToken cancellationToken = default)
    {
        if (planDate == default)
        {
            throw new ArgumentException("The plan date must not be empty.", nameof(planDate));
        }

        DailyPlan? plan = await planRepository.FindAsync(planDate, cancellationToken).ConfigureAwait(false);
        return plan is null ? null : await ToSummaryAsync(plan, planDate, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DailyPlanTaskCandidate>> ListTodayCandidatesAsync(CancellationToken cancellationToken = default)
    {
        DateOnly today = LocalToday();
        DailyPlan plan = await planRepository.GetOrCreateAsync(today, UtcNow(), cancellationToken).ConfigureAwait(false);
        EnsureOpen(plan);
        HashSet<Guid> plannedTaskIds = plan.ActiveItems.Select(item => item.TaskItemId).ToHashSet();
        IReadOnlyList<TimTicker.Domain.Tasks.TaskItem> tasks = await taskRepository.ListAsync(
            new TaskQuery(DomainTaskStatus.Pending, null, TaskSortOrder.PriorityDescending),
            cancellationToken).ConfigureAwait(false);

        return tasks
            .Where(task => !plannedTaskIds.Contains(task.Id))
            .Select(task => new DailyPlanTaskCandidate(TaskService.ToSummary(task), IsOverdue(task.DueDate, today)))
            .ToArray();
    }

    public async Task<DailyPlanSummary> AddTasksToTodayAsync(IEnumerable<Guid> taskIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taskIds);
        Guid[] ids = taskIds.ToArray();
        if (ids.Length == 0)
        {
            throw new ArgumentException("At least one task must be selected.", nameof(taskIds));
        }

        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
        {
            throw new ArgumentException("Selected task identifiers must be non-empty and unique.", nameof(taskIds));
        }

        foreach (Guid taskId in ids)
        {
            TimTicker.Domain.Tasks.TaskItem task = await taskRepository.FindAsync(taskId, cancellationToken).ConfigureAwait(false)
                ?? throw new KeyNotFoundException("A selected task was not found.");
            if (task.Status != DomainTaskStatus.Pending)
            {
                throw new InvalidOperationException("Only pending tasks can be added to today's plan.");
            }
        }

        DateOnly today = LocalToday();
        DailyPlan plan = await planRepository.GetOrCreateAsync(today, UtcNow(), cancellationToken).ConfigureAwait(false);
        EnsureOpen(plan);
        DateTimeOffset now = UtcNow();
        foreach (Guid taskId in ids)
        {
            plan.AddTask(taskId, now);
        }

        await planRepository.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(plan, today, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DailyPlanSummary> CreateTaskAndAddToTodayAsync(CreateTaskRequest request, CancellationToken cancellationToken = default)
    {
        DateOnly today = LocalToday();
        DailyPlan plan = await planRepository.GetOrCreateAsync(today, UtcNow(), cancellationToken).ConfigureAwait(false);
        EnsureOpen(plan);
        TaskSummary task = await taskService.CreateAsync(request, cancellationToken).ConfigureAwait(false);
        return await AddTasksToTodayAsync([task.Id], cancellationToken).ConfigureAwait(false);
    }

    public async Task<DailyPlanSummary> RemoveTaskFromTodayAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        DateOnly today = LocalToday();
        DailyPlan plan = await planRepository.GetOrCreateAsync(today, UtcNow(), cancellationToken).ConfigureAwait(false);
        EnsureOpen(plan);
        plan.RemoveTask(taskId, UtcNow());
        await planRepository.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(plan, today, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DailyPlanSummary> MoveTodayTaskAsync(Guid taskId, PlanMoveDirection direction, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        DateOnly today = LocalToday();
        DailyPlan plan = await planRepository.GetOrCreateAsync(today, UtcNow(), cancellationToken).ConfigureAwait(false);
        EnsureOpen(plan);
        IReadOnlyList<DailyPlanItem> activeItems = plan.ActiveItems;
        int currentIndex = activeItems.ToList().FindIndex(item => item.TaskItemId == taskId);
        if (currentIndex < 0)
        {
            throw new KeyNotFoundException("The task is not in today's plan.");
        }

        int newIndex = currentIndex + (int)direction;
        if (newIndex < 0 || newIndex >= activeItems.Count)
        {
            return await ToSummaryAsync(plan, today, cancellationToken).ConfigureAwait(false);
        }

        plan.MoveTask(taskId, newIndex, UtcNow());
        await planRepository.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(plan, today, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DailyPlanSummary> CloseTodayAsync(
        string? reviewText,
        CancellationToken cancellationToken = default)
    {
        DateOnly today = LocalToday();
        DailyPlan plan = await planRepository.GetOrCreateAsync(today, UtcNow(), cancellationToken).ConfigureAwait(false);
        EnsureOpen(plan);
        HashSet<Guid> completedTaskIds = [];
        foreach (DailyPlanItem item in plan.ActiveItems)
        {
            TimTicker.Domain.Tasks.TaskItem? task = await taskRepository.FindAsync(item.TaskItemId, cancellationToken).ConfigureAwait(false);
            if (task?.Status == DomainTaskStatus.Completed)
            {
                completedTaskIds.Add(item.TaskItemId);
            }
        }

        plan.Close(reviewText, completedTaskIds, UtcNow());
        await planRepository.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(plan, today, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DailyPlanSummary> ReopenAsync(DateOnly planDate, CancellationToken cancellationToken = default)
    {
        if (planDate == default)
        {
            throw new ArgumentException("The plan date must not be empty.", nameof(planDate));
        }

        DailyPlan plan = await planRepository.FindAsync(planDate, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("The requested daily plan was not found.");
        plan.Reopen(UtcNow());
        await planRepository.UpdateAsync(plan, cancellationToken).ConfigureAwait(false);
        return await ToSummaryAsync(plan, planDate, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DailyPlanSummary> ToSummaryAsync(DailyPlan plan, DateOnly today, CancellationToken cancellationToken)
    {
        List<DailyPlanItemSummary> items = [];
        foreach (DailyPlanItem planItem in plan.ActiveItems)
        {
            TimTicker.Domain.Tasks.TaskItem? task = await taskRepository.FindAsync(planItem.TaskItemId, cancellationToken).ConfigureAwait(false);
            if (task is null)
            {
                continue;
            }

            DomainTaskStatus effectiveStatus = plan.IsClosed && planItem.WasCompletedAtClose.HasValue
                ? planItem.WasCompletedAtClose.Value ? DomainTaskStatus.Completed : DomainTaskStatus.Pending
                : task.Status;
            items.Add(new DailyPlanItemSummary(
                task.Id,
                task.Title,
                task.DueDate,
                task.Priority,
                effectiveStatus,
                planItem.SortOrder,
                IsOverdue(task.DueDate, today)));
        }

        int totalCount = plan.ActiveItems.Count;
        int completedCount = plan.IsClosed
            ? plan.ActiveItems.Count(item => item.WasCompletedAtClose == true)
            : items.Count(item => item.Status == DomainTaskStatus.Completed);
        decimal? completionRate = plan.IsClosed
            ? plan.FinalCompletionRate
            : totalCount == 0 ? null : completedCount / (decimal)totalCount;
        return new DailyPlanSummary(
            plan.Id,
            plan.Date,
            items,
            completedCount,
            totalCount,
            completionRate,
            plan.IsClosed,
            plan.ClosedAtUtc,
            plan.ReviewText);
    }

    private DateTimeOffset UtcNow() => timeProvider.GetUtcNow();

    private DateOnly LocalToday() => DayBoundary.GetBusinessDate(
        timeProvider.GetLocalNow().DateTime,
        dayBoundarySettings.CurrentDayEndTime);

    private static bool IsOverdue(DateOnly? dueDate, DateOnly today) => dueDate.HasValue && dueDate.Value < today;

    private static void EnsureOpen(DailyPlan plan)
    {
        if (plan.IsClosed)
        {
            throw new InvalidOperationException("The daily plan is closed and read-only.");
        }
    }
}
