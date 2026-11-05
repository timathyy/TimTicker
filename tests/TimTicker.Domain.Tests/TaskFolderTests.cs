using TimTicker.Domain.Tasks;

namespace TimTicker.Domain.Tests;

public sealed class TaskFolderTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 4, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConstructorNormalizesNameAndKind()
    {
        TaskFolder folder = new(Guid.NewGuid(), "  工作  ", TaskFolderKind.User, CreatedAt);

        Assert.Equal("工作", folder.Name);
        Assert.Equal(TaskFolderKind.User, folder.Kind);
    }

    [Fact]
    public void TemplateFolderCannotBeDeletedIndependently()
    {
        TaskFolder folder = new(Guid.NewGuid(), "每日复盘", TaskFolderKind.Template, CreatedAt);

        Assert.Throws<InvalidOperationException>(() => folder.Delete(CreatedAt.AddMinutes(1)));
    }

    [Fact]
    public void FolderCanBelongToAnotherFolder()
    {
        Guid parentId = Guid.NewGuid();
        TaskFolder folder = new(
            Guid.NewGuid(),
            "后端",
            TaskFolderKind.User,
            CreatedAt,
            parentId);

        Assert.Equal(parentId, folder.ParentId);
    }

    [Fact]
    public void FolderRejectsItselfAsParent()
    {
        Guid id = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new TaskFolder(
            id,
            "循环",
            TaskFolderKind.User,
            CreatedAt,
            id));
    }
}
