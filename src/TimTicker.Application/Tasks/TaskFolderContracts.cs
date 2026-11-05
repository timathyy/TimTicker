using TimTicker.Domain.Tasks;

namespace TimTicker.Application.Tasks;

public sealed record TaskFolderSummary(
    Guid Id,
    string Name,
    Guid? ParentId,
    TaskFolderKind Kind,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
