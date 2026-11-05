using TimTicker.Domain.Tasks;
using TimTicker.Domain.Templates;

namespace TimTicker.Application.Templates;

public sealed record CreateTaskTemplateRequest(
    string Name,
    string TitlePattern,
    string? Description,
    TaskPriority Priority,
    TaskTemplateSchedule Schedule,
    int RepeatInterval,
    DateOnly StartDate,
    bool IsEnabled,
    Guid? ParentFolderId = null);

public sealed record TaskTemplateSummary(
    Guid Id,
    Guid FolderId,
    string Name,
    string FolderName,
    string TitlePattern,
    string? Description,
    TaskPriority Priority,
    TaskTemplateSchedule Schedule,
    int RepeatInterval,
    DateOnly StartDate,
    bool IsEnabled,
    int NextSequenceNumber,
    DateOnly? LastGeneratedDate);

public sealed record TemplateGenerationResult(int GeneratedCount, IReadOnlyList<Guid> TaskIds);
