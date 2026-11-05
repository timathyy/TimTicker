namespace TimTicker.Domain.Tasks;

public sealed class TaskFolder
{
    public const int MaximumNameLength = 80;

    private TaskFolder()
    {
    }

    public TaskFolder(
        Guid id,
        string name,
        TaskFolderKind kind,
        DateTimeOffset createdAtUtc,
        Guid? parentId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The folder identifier must not be empty.", nameof(id));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Id = id;
        ParentId = RequireOptionalIdentifier(parentId, nameof(parentId));
        if (ParentId == Id)
        {
            throw new ArgumentException("A folder cannot be its own parent.", nameof(parentId));
        }

        Name = NormalizeName(name);
        Kind = kind;
        CreatedAtUtc = RequireUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Guid? ParentId { get; private set; }

    public TaskFolderKind Kind { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    public void Rename(string name, DateTimeOffset updatedAtUtc)
    {
        EnsureActive();
        Name = NormalizeName(name);
        UpdatedAtUtc = RequireUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    public void Delete(DateTimeOffset deletedAtUtc)
    {
        EnsureActive();
        if (Kind == TaskFolderKind.Template)
        {
            throw new InvalidOperationException("A template folder cannot be deleted independently from its template.");
        }

        IsDeleted = true;
        UpdatedAtUtc = RequireUtc(deletedAtUtc, nameof(deletedAtUtc));
    }

    private void EnsureActive()
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("A deleted folder cannot be changed.");
        }
    }

    private static string NormalizeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string normalized = name.Trim();
        if (normalized.Length == 0 || normalized.Length > MaximumNameLength)
        {
            throw new ArgumentException($"A folder name must contain 1 to {MaximumNameLength} characters.", nameof(name));
        }

        return normalized;
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Folder timestamps must use UTC.", parameterName);
        }

        return value;
    }

    private static Guid? RequireOptionalIdentifier(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("The parent folder identifier must not be empty.", parameterName);
        }

        return value;
    }
}
