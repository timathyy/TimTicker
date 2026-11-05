using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.Application.Tasks;

public sealed record CreateTaskRequest(
    string Title,
    string? Description,
    DateOnly CreatedDate,
    DateOnly? DueDate,
    TaskPriority Priority,
    Guid? FolderId = null);

public sealed record UpdateTaskRequest(
    Guid Id,
    string Title,
    string? Description,
    DateOnly CreatedDate,
    DateOnly? DueDate,
    TaskPriority Priority,
    DateOnly? CompletedDate,
    Guid? FolderId = null);

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
    DateOnly? CompletedDate,
    Guid? FolderId,
    Guid? TemplateId,
    int? TemplateSequenceNumber,
    DateOnly? TemplateOccurrenceDate);

public sealed record TaskQuery(
    DomainTaskStatus? Status,
    string? SearchText,
    TaskSortOrder SortOrder,
    Guid? FolderId = null,
    bool RootOnly = false);

public enum TaskSortOrder
{
    CreatedAtDescending = 0,
    DueDateAscending = 1,
    PriorityDescending = 2,
    CreatedAtAscending = 3,
    DueDateDescending = 4,
    PriorityAscending = 5,
}
