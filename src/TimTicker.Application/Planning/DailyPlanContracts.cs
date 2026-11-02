using TimTicker.Application.Tasks;

namespace TimTicker.Application.Planning;

public sealed record DailyPlanItemSummary(
    Guid TaskId,
    string Title,
    DateOnly? DueDate,
    TimTicker.Domain.Tasks.TaskPriority Priority,
    TimTicker.Domain.Tasks.TaskStatus Status,
    int SortOrder,
    bool IsOverdue);

public sealed record DailyPlanSummary(
    Guid Id,
    DateOnly Date,
    IReadOnlyList<DailyPlanItemSummary> Items,
    int CompletedCount,
    int TotalCount,
    decimal? CompletionRate,
    bool IsClosed,
    DateTimeOffset? ClosedAtUtc,
    string? ReviewText);

public sealed record DailyPlanTaskCandidate(TaskSummary Task, bool IsOverdue);

public enum PlanMoveDirection
{
    Up = -1,
    Down = 1,
}
