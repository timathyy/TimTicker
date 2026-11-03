using TimTicker.Application.Planning;
using TimTicker.Application.Tasks;
using TimTicker.Application.Settings;
using TimTicker.Domain.Planning;
using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.Application.Tests;

public sealed class DailyPlanServiceTests
{
    public static TheoryData<int, int, decimal?> CompletionCases => new()
    {
        { 0, 0, null },
        { 3, 0, 0m },
        { 3, 1, 1m / 3m },
        { 3, 3, 1m },
    };

    [Fact]
    public async Task RepeatedGetTodayReturnsOnePlanForTheDate()
    {
        InMemoryDailyPlanRepository plans = new();
        InMemoryTaskRepository tasks = new();
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));
        DailyPlanService service = CreateService(plans, tasks, clock);

        DailyPlanSummary first = await service.GetTodayAsync();
        DailyPlanSummary second = await service.GetTodayAsync();

        Assert.Equal(first.Id, second.Id);
        Assert.Single(plans.Plans);
    }

    [Fact]
    public async Task DuplicateTaskCannotBeAddedToToday()
    {
        InMemoryDailyPlanRepository plans = new();
        InMemoryTaskRepository tasks = new();
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));
        DailyPlanService service = CreateService(plans, tasks, clock);
        TaskItem task = AddPendingTask(tasks, "Plan me", clock.GetUtcNow());
        await service.AddTasksToTodayAsync([task.Id]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddTasksToTodayAsync([task.Id]));

        Assert.Single((await service.GetTodayAsync()).Items);
    }

    [Fact]
    public async Task NewDayCreatesEmptyPlanAndLeavesPendingTaskInPool()
    {
        InMemoryDailyPlanRepository plans = new();
        InMemoryTaskRepository tasks = new();
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));
        DailyPlanService service = CreateService(plans, tasks, clock);
        TaskItem task = AddPendingTask(tasks, "Carry only in pool", clock.GetUtcNow());
        await service.AddTasksToTodayAsync([task.Id]);

        clock.UtcNow = new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero);
        DailyPlanSummary nextDay = await service.GetTodayAsync();
        IReadOnlyList<DailyPlanTaskCandidate> candidates = await service.ListTodayCandidatesAsync();

        Assert.Empty(nextDay.Items);
        Assert.Equal(2, plans.Plans.Count);
        Assert.Contains(candidates, candidate => candidate.Task.Id == task.Id);
        Assert.Equal(DomainTaskStatus.Pending, task.Status);
    }

    [Fact]
    public async Task BeforeConfiguredDayEndTodayStillUsesPreviousDate()
    {
        InMemoryDailyPlanRepository plans = new();
        InMemoryTaskRepository tasks = new();
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 3, 3, 0, 0, TimeSpan.Zero));
        DailyPlanService service = CreateService(
            plans,
            tasks,
            clock,
            new TestDayBoundarySettings(DayEndTime.SixAm));

        DailyPlanSummary today = await service.GetTodayAsync();

        Assert.Equal(new DateOnly(2026, 11, 2), today.Date);
        Assert.True(plans.Plans.ContainsKey(new DateOnly(2026, 11, 2)));
    }

    [Theory]
    [MemberData(nameof(CompletionCases))]
    public async Task CompletionRateHandlesAllPlanBoundaries(
        int totalCount,
        int completedCount,
        decimal? expectedRate)
    {
        InMemoryDailyPlanRepository plans = new();
        InMemoryTaskRepository tasks = new();
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));
        DailyPlanService service = CreateService(plans, tasks, clock);
        List<TaskItem> taskItems = [];
        for (int index = 0; index < totalCount; index++)
        {
            taskItems.Add(AddPendingTask(tasks, $"Task {index}", clock.GetUtcNow()));
        }

        if (taskItems.Count > 0)
        {
            await service.AddTasksToTodayAsync(taskItems.Select(task => task.Id));
        }

        foreach (TaskItem task in taskItems.Take(completedCount))
        {
            task.Complete(clock.GetUtcNow(), new DateOnly(2026, 11, 2));
        }

        DailyPlanSummary summary = await service.GetTodayAsync();

        Assert.Equal(totalCount, summary.TotalCount);
        Assert.Equal(completedCount, summary.CompletedCount);
        Assert.Equal(expectedRate, summary.CompletionRate);
    }

    [Fact]
    public async Task CompletionUpdatesAfterCompletingRemovingReopeningAndAddingTasks()
    {
        InMemoryDailyPlanRepository plans = new();
        InMemoryTaskRepository tasks = new();
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));
        DailyPlanService service = CreateService(plans, tasks, clock);
        TaskItem first = AddPendingTask(tasks, "First", clock.GetUtcNow());
        TaskItem second = AddPendingTask(tasks, "Second", clock.GetUtcNow());
        await service.AddTasksToTodayAsync([first.Id, second.Id]);

        first.Complete(clock.GetUtcNow(), new DateOnly(2026, 11, 2));
        Assert.Equal(0.5m, (await service.GetTodayAsync()).CompletionRate);

        Assert.Equal(1m, (await service.RemoveTaskFromTodayAsync(second.Id)).CompletionRate);

        first.Reopen(clock.GetUtcNow());
        Assert.Equal(0m, (await service.GetTodayAsync()).CompletionRate);

        DailyPlanSummary restored = await service.AddTasksToTodayAsync([second.Id]);
        Assert.Equal(0m, restored.CompletionRate);
        Assert.Equal(2, restored.TotalCount);
    }

    [Fact]
    public async Task DeletedTaskIsRemovedFromOpenPlanAndCompletionDenominator()
    {
        InMemoryDailyPlanRepository plans = new();
        InMemoryTaskRepository tasks = new();
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));
        DailyPlanService service = CreateService(plans, tasks, clock);
        TaskItem completedTask = AddPendingTask(tasks, "Completed", clock.GetUtcNow());
        TaskItem deletedTask = AddPendingTask(tasks, "Deleted", clock.GetUtcNow());
        await service.AddTasksToTodayAsync([completedTask.Id, deletedTask.Id]);
        completedTask.Complete(clock.GetUtcNow(), new DateOnly(2026, 11, 2));
        deletedTask.Delete(clock.GetUtcNow());

        DailyPlanSummary summary = await service.GetTodayAsync();

        Assert.Single(summary.Items);
        Assert.Equal(1, summary.TotalCount);
        Assert.Equal(1, summary.CompletedCount);
        Assert.Equal(1m, summary.CompletionRate);
        Assert.Equal(completedTask.Id, Assert.Single(plans.Plans.Values).ActiveItems.Single().TaskItemId);
    }

    [Fact]
    public async Task CloseFreezesCompletionAndReopenRecalculatesHistoricalPlan()
    {
        InMemoryDailyPlanRepository plans = new();
        InMemoryTaskRepository tasks = new();
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));
        DailyPlanService service = CreateService(plans, tasks, clock);
        TaskItem completedTask = AddPendingTask(tasks, "Completed", clock.GetUtcNow());
        TaskItem pendingTask = AddPendingTask(tasks, "Pending", clock.GetUtcNow());
        await service.AddTasksToTodayAsync([completedTask.Id, pendingTask.Id]);
        completedTask.Complete(clock.GetUtcNow(), new DateOnly(2026, 11, 2));

        DailyPlanSummary closed = await service.CloseTodayAsync("  Review text  ");
        completedTask.Reopen(clock.GetUtcNow().AddMinutes(1));
        DailyPlanSummary frozen = (await service.GetForDateAsync(new DateOnly(2026, 11, 2)))!;

        Assert.True(closed.IsClosed);
        Assert.Equal(0.5m, closed.CompletionRate);
        Assert.Equal("Review text", closed.ReviewText);
        Assert.Equal(1, frozen.CompletedCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RemoveTaskFromTodayAsync(pendingTask.Id));

        clock.UtcNow = clock.UtcNow.AddDays(1);
        DailyPlanSummary reopened = await service.ReopenAsync(new DateOnly(2026, 11, 2));

        Assert.False(reopened.IsClosed);
        Assert.Equal(0, reopened.CompletedCount);
        Assert.Equal(0m, reopened.CompletionRate);
        Assert.Equal("Review text", reopened.ReviewText);
    }

    [Fact]
    public async Task SelectedHistoricalDateSupportsFullPlanEditingWithoutChangingToday()
    {
        InMemoryDailyPlanRepository plans = new();
        InMemoryTaskRepository tasks = new();
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero));
        DailyPlanService service = CreateService(plans, tasks, clock);
        DateOnly historicalDate = new(2026, 11, 2);
        TaskItem existingTask = AddPendingTask(tasks, "Existing", clock.GetUtcNow());

        await service.AddTasksAsync(historicalDate, [existingTask.Id]);
        DailyPlanSummary withCreatedTask = await service.CreateTaskAndAddAsync(
            historicalDate,
            new CreateTaskRequest("Created for history", null, historicalDate, null, TaskPriority.Medium));
        Guid createdTaskId = withCreatedTask.Items.Single(item => item.TaskId != existingTask.Id).TaskId;
        DailyPlanSummary moved = await service.MoveTaskAsync(historicalDate, createdTaskId, PlanMoveDirection.Up);
        DailyPlanSummary removed = await service.RemoveTaskAsync(historicalDate, existingTask.Id);
        DailyPlanSummary closed = await service.CloseAsync(historicalDate, "Historical review");
        DailyPlanSummary reopened = await service.ReopenAsync(historicalDate);
        IReadOnlyList<DailyPlanTaskCandidate> candidates = await service.ListCandidatesAsync(historicalDate);

        Assert.Equal(createdTaskId, moved.Items[0].TaskId);
        Assert.Equal(createdTaskId, Assert.Single(removed.Items).TaskId);
        Assert.True(closed.IsClosed);
        Assert.Equal("Historical review", closed.ReviewText);
        Assert.False(reopened.IsClosed);
        Assert.Contains(candidates, candidate => candidate.Task.Id == existingTask.Id);
        Assert.DoesNotContain(candidates, candidate => candidate.Task.Id == createdTaskId);
        Assert.Single(plans.Plans);
        Assert.True(plans.Plans.ContainsKey(historicalDate));
    }

    private static DailyPlanService CreateService(
        InMemoryDailyPlanRepository plans,
        InMemoryTaskRepository tasks,
        MutableTimeProvider clock,
        IDayBoundarySettings? dayBoundarySettings = null)
    {
        IDayBoundarySettings settings = dayBoundarySettings ?? new TestDayBoundarySettings();
        TaskService taskService = new(tasks, settings, clock);
        return new DailyPlanService(
            plans,
            tasks,
            taskService,
            settings,
            clock);
    }

    private static TaskItem AddPendingTask(InMemoryTaskRepository repository, string title, DateTimeOffset createdAtUtc)
    {
        TaskItem task = new(
            Guid.NewGuid(),
            title,
            null,
            DateOnly.FromDateTime(createdAtUtc.Date),
            null,
            TaskPriority.None,
            createdAtUtc);
        repository.Tasks.Add(task);
        return task;
    }

    private sealed class InMemoryDailyPlanRepository : IDailyPlanRepository
    {
        public Dictionary<DateOnly, DailyPlan> Plans { get; } = [];

        public Task<DailyPlan?> FindAsync(DateOnly planDate, CancellationToken cancellationToken = default) =>
            Task.FromResult(Plans.GetValueOrDefault(planDate));

        public Task<DailyPlan> GetOrCreateAsync(DateOnly planDate, DateTimeOffset createdAtUtc, CancellationToken cancellationToken = default)
        {
            if (!Plans.TryGetValue(planDate, out DailyPlan? plan))
            {
                plan = new DailyPlan(Guid.NewGuid(), planDate, createdAtUtc);
                Plans.Add(planDate, plan);
            }

            return Task.FromResult(plan);
        }

        public Task UpdateAsync(DailyPlan plan, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryTaskRepository : ITaskItemRepository
    {
        public List<TaskItem> Tasks { get; } = [];

        public Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
        {
            Tasks.Add(task);
            return Task.CompletedTask;
        }

        public Task<TaskItem?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Tasks.SingleOrDefault(task => task.Id == id && !task.IsDeleted));

        public Task<IReadOnlyList<TaskItem>> ListAsync(TaskQuery query, CancellationToken cancellationToken = default)
        {
            IEnumerable<TaskItem> result = Tasks.Where(task => !task.IsDeleted &&
                (!query.Status.HasValue || task.Status == query.Status.Value));
            return Task.FromResult<IReadOnlyList<TaskItem>>(result.ToArray());
        }

        public Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
