using TimTicker.Domain.Tasks;
using TimTicker.Domain.Templates;

namespace TimTicker.Domain.Tests;

public sealed class TaskTemplateTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 4, 1, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("复盘-%y-%m-%d-第%n次", "复盘-2026-11-04-第12次")]
    [InlineData("%y%m%d-%n", "20261104-12")]
    public void FormatterReplacesEverySupportedPlaceholder(string pattern, string expected)
    {
        Assert.Equal(expected, TaskTitlePatternFormatter.Format(pattern, new DateOnly(2026, 11, 4), 12));
    }

    [Fact]
    public void ManualGenerationUsesSequenceAndDedicatedFolder()
    {
        Guid folderId = Guid.NewGuid();
        TaskTemplate template = CreateTemplate(folderId, TaskTemplateSchedule.Manual);

        TaskItem first = template.GenerateManual(new DateOnly(2026, 11, 4), CreatedAt.AddMinutes(1));
        TaskItem second = template.GenerateManual(new DateOnly(2026, 11, 5), CreatedAt.AddMinutes(2));

        Assert.Equal("日报-2026-11-04-1", first.Title);
        Assert.Equal("日报-2026-11-05-2", second.Title);
        Assert.Equal(folderId, first.FolderId);
        Assert.Equal(template.Id, first.TemplateId);
        Assert.Equal(3, template.NextSequenceNumber);
        Assert.Null(first.TemplateOccurrenceDate);
        Assert.Throws<InvalidOperationException>(() => first.MoveToFolder(null, CreatedAt.AddMinutes(3)));
    }

    [Fact]
    public void ScheduledGenerationMustFollowOccurrencesAndTracksDate()
    {
        TaskTemplate template = CreateTemplate(Guid.NewGuid(), TaskTemplateSchedule.Daily);

        TaskItem generated = template.GenerateScheduled(new DateOnly(2026, 11, 1), CreatedAt.AddMinutes(1));

        Assert.Equal(new DateOnly(2026, 11, 1), generated.TemplateOccurrenceDate);
        Assert.Equal(new DateOnly(2026, 11, 2), template.GetNextOccurrenceDate());
        Assert.Throws<InvalidOperationException>(() => template.GenerateScheduled(new DateOnly(2026, 11, 3), CreatedAt.AddMinutes(2)));
    }

    [Fact]
    public void MonthlyScheduleRemainsAnchoredToStartDay()
    {
        TaskTemplate template = CreateTemplate(
            Guid.NewGuid(),
            TaskTemplateSchedule.Monthly,
            startDate: new DateOnly(2027, 1, 31));

        template.GenerateScheduled(new DateOnly(2027, 1, 31), CreatedAt.AddMinutes(1));
        template.GenerateScheduled(new DateOnly(2027, 2, 28), CreatedAt.AddMinutes(2));

        Assert.Equal(new DateOnly(2027, 3, 31), template.GetNextOccurrenceDate());
    }

    [Fact]
    public void PatternThatCanExpandPastTaskTitleLimitIsRejected()
    {
        string pattern = new('x', TaskItem.MaximumTitleLength - 2);

        Assert.Throws<ArgumentException>(() => new TaskTemplate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Long title",
            pattern + "%n",
            null,
            TaskPriority.None,
            TaskTemplateSchedule.Manual,
            1,
            new DateOnly(2026, 11, 4),
            false,
            CreatedAt));
    }

    private static TaskTemplate CreateTemplate(
        Guid folderId,
        TaskTemplateSchedule schedule,
        DateOnly? startDate = null) =>
        new(
            Guid.NewGuid(),
            folderId,
            "日报",
            "日报-%y-%m-%d-%n",
            null,
            TaskPriority.Medium,
            schedule,
            1,
            startDate ?? new DateOnly(2026, 11, 1),
            isEnabled: true,
            CreatedAt);
}
