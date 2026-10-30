using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.Application.Tasks;

public sealed record CreateTaskRequest(string Title, string? Description, DateOnly CreatedDate, DateOnly? DueDate, TaskPriority Priority);

public sealed record UpdateTaskRequest(
    Guid Id,
    string Title,
    string? Description,
    DateOnly CreatedDate,
    DateOnly? DueDate,
    TaskPriority Priority,
    DateOnly? CompletedDate);

public sealed record TaskSummary(
    Guid Id,
    string Title,
    string? Description,
    DateOnly CreatedDate,
    DateOnly? DueDate,
    TaskPriority Priority,
    DomainTaskStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateOnly? CompletedDate);

public sealed record TaskQuery(DomainTaskStatus? Status, string? SearchText, TaskSortOrder SortOrder);

public enum TaskSortOrder
{
    CreatedAtDescending = 0,
    DueDateAscending = 1,
    PriorityDescending = 2,
}
