using TimTicker.Domain.Planning;

namespace TimTicker.Domain.Tests;

public sealed class DailyPlanTests
{
    private static readonly DateTimeOffset InitialTime = new(2026, 11, 2, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DuplicateActiveTaskCannotBeAdded()
    {
        DailyPlan plan = CreatePlan();
        Guid taskId = Guid.NewGuid();
        plan.AddTask(taskId, InitialTime);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => plan.AddTask(taskId, InitialTime.AddMinutes(1)));

        Assert.Contains("already", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(plan.ActiveItems);
    }

    [Fact]
    public void RemovedTaskCanBeRestoredWithoutCreatingDuplicateHistory()
    {
        DailyPlan plan = CreatePlan();
        Guid firstTaskId = Guid.NewGuid();
        Guid secondTaskId = Guid.NewGuid();
        plan.AddTask(firstTaskId, InitialTime);
        plan.AddTask(secondTaskId, InitialTime.AddMinutes(1));

        plan.RemoveTask(firstTaskId, InitialTime.AddMinutes(2));
        plan.AddTask(firstTaskId, InitialTime.AddMinutes(3));

        Assert.Equal(2, plan.Items.Count);
        List<Guid> expectedTasks = [secondTaskId, firstTaskId];
        List<int> expectedOrder = [0, 1];
        Assert.Equal(expectedTasks, plan.ActiveItems.Select(item => item.TaskItemId));
        Assert.Equal(expectedOrder, plan.ActiveItems.Select(item => item.SortOrder));
    }

    [Fact]
    public void MoveTaskMaintainsContiguousOrder()
    {
        DailyPlan plan = CreatePlan();
        Guid firstTaskId = Guid.NewGuid();
        Guid secondTaskId = Guid.NewGuid();
        Guid thirdTaskId = Guid.NewGuid();
        plan.AddTask(firstTaskId, InitialTime);
        plan.AddTask(secondTaskId, InitialTime);
        plan.AddTask(thirdTaskId, InitialTime);

        plan.MoveTask(thirdTaskId, 0, InitialTime.AddMinutes(1));

        List<Guid> expectedTasks = [thirdTaskId, firstTaskId, secondTaskId];
        List<int> expectedOrder = [0, 1, 2];
        Assert.Equal(expectedTasks, plan.ActiveItems.Select(item => item.TaskItemId));
        Assert.Equal(expectedOrder, plan.ActiveItems.Select(item => item.SortOrder));
    }

    private static DailyPlan CreatePlan() => new(Guid.NewGuid(), new DateOnly(2026, 11, 2), InitialTime);
}
