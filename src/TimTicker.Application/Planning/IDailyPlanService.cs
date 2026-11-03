using TimTicker.Application.Tasks;

namespace TimTicker.Application.Planning;

public interface IDailyPlanService
{
    Task<DailyPlanSummary> GetTodayAsync(CancellationToken cancellationToken = default);

    Task<DailyPlanSummary> GetOrCreateForDateAsync(DateOnly planDate, CancellationToken cancellationToken = default);

    Task<DailyPlanSummary?> GetForDateAsync(DateOnly planDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyPlanTaskCandidate>> ListTodayCandidatesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyPlanTaskCandidate>> ListCandidatesAsync(
        DateOnly planDate,
        CancellationToken cancellationToken = default);

    Task<DailyPlanSummary> AddTasksToTodayAsync(IEnumerable<Guid> taskIds, CancellationToken cancellationToken = default);

    Task<DailyPlanSummary> AddTasksAsync(
        DateOnly planDate,
        IEnumerable<Guid> taskIds,
        CancellationToken cancellationToken = default);

    Task<DailyPlanSummary> CreateTaskAndAddToTodayAsync(CreateTaskRequest request, CancellationToken cancellationToken = default);

    Task<DailyPlanSummary> CreateTaskAndAddAsync(
        DateOnly planDate,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default);

    Task<DailyPlanSummary> RemoveTaskFromTodayAsync(Guid taskId, CancellationToken cancellationToken = default);

    Task<DailyPlanSummary> RemoveTaskAsync(
        DateOnly planDate,
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<DailyPlanSummary> MoveTodayTaskAsync(Guid taskId, PlanMoveDirection direction, CancellationToken cancellationToken = default);

    Task<DailyPlanSummary> MoveTaskAsync(
        DateOnly planDate,
        Guid taskId,
        PlanMoveDirection direction,
        CancellationToken cancellationToken = default);

    Task<DailyPlanSummary> CloseTodayAsync(string? reviewText, CancellationToken cancellationToken = default);

    Task<DailyPlanSummary> CloseAsync(
        DateOnly planDate,
        string? reviewText,
        CancellationToken cancellationToken = default);

    Task<DailyPlanSummary> ReopenAsync(DateOnly planDate, CancellationToken cancellationToken = default);
}
