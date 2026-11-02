using TimTicker.Application.Planning;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Planning;
using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.Application.Tests;

public sealed class DailyPlanServiceTests
{
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

    private static DailyPlanService CreateService(
        InMemoryDailyPlanRepository plans,
        InMemoryTaskRepository tasks,
        MutableTimeProvider clock)
    {
        TaskService taskService = new(tasks, clock);
        return new DailyPlanService(plans, tasks, taskService, clock);
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
