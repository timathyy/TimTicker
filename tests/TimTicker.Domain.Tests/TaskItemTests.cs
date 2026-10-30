using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.Domain.Tests;

public sealed class TaskItemTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConstructorNormalizesDetailsAndSetsPendingState()
    {
        TaskItem task = new(Guid.NewGuid(), "  Finish v0.2  ", "  Add task pool  ", new DateOnly(2026, 11, 1), TaskPriority.High, CreatedAt);

        Assert.Equal("Finish v0.2", task.Title);
        Assert.Equal("Add task pool", task.Description);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal(DomainTaskStatus.Pending, task.Status);
        Assert.Equal(CreatedAt, task.CreatedAtUtc);
        Assert.Equal(CreatedAt, task.UpdatedAtUtc);
        Assert.Null(task.CompletedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstructorRejectsBlankTitle(string title)
    {
        Assert.Throws<ArgumentException>(() => CreateTask(title));
    }

    [Fact]
    public void ConstructorRejectsNonUtcTimestamp()
    {
        DateTimeOffset localTime = new(2026, 10, 29, 8, 0, 0, TimeSpan.FromHours(8));

        Assert.Throws<ArgumentException>(() => new TaskItem(Guid.NewGuid(), "Task", null, null, TaskPriority.None, localTime));
    }

    [Fact]
    public void CompleteAndReopenUpdateStateAndTimestamp()
    {
        TaskItem task = CreateTask();
        DateTimeOffset completedAt = CreatedAt.AddHours(1);
        DateTimeOffset reopenedAt = completedAt.AddMinutes(10);

        task.Complete(completedAt);
        task.Reopen(reopenedAt);

        Assert.Equal(DomainTaskStatus.Pending, task.Status);
        Assert.Null(task.CompletedAtUtc);
        Assert.Equal(reopenedAt, task.UpdatedAtUtc);
    }

    [Fact]
    public void ArchiveRestoreAndDeleteFollowAllowedTransitions()
    {
        TaskItem task = CreateTask();
        task.Complete(CreatedAt.AddMinutes(1));
        task.Archive(CreatedAt.AddMinutes(2));
        task.Restore(CreatedAt.AddMinutes(3));
        task.Delete(CreatedAt.AddMinutes(4));

        Assert.Equal(DomainTaskStatus.Pending, task.Status);
        Assert.True(task.IsDeleted);
        Assert.Throws<InvalidOperationException>(() => task.Update("Changed", null, null, TaskPriority.None, CreatedAt.AddMinutes(5)));
    }

    [Fact]
    public void InvalidStatusChangesAreRejected()
    {
        TaskItem task = CreateTask();

        Assert.Throws<InvalidOperationException>(() => task.Reopen(CreatedAt.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => task.Restore(CreatedAt.AddMinutes(1)));
    }

    private static TaskItem CreateTask(string title = "Task") =>
        new(Guid.NewGuid(), title, null, null, TaskPriority.None, CreatedAt);
}
