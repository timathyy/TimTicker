using TimTicker.Domain.Timeline;

namespace TimTicker.Domain.Tests;

public sealed class TimelineEntryTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CompletedEntryRequiresEndAfterStart()
    {
        Assert.Throws<ArgumentException>(() => Create(Start));
        Assert.Throws<ArgumentException>(() => Create(Start.AddMinutes(-1)));
    }

    [Fact]
    public void RunningEntryCanBeStoppedOnce()
    {
        TimelineEntry entry = Create(null);

        entry.Stop(Start.AddHours(1));

        Assert.False(entry.IsRunning);
        Assert.Equal(Start.AddHours(1), entry.EndAtUtc);
        Assert.Throws<InvalidOperationException>(() => entry.Stop(Start.AddHours(2)));
    }

    [Fact]
    public void EntryNormalizesOptionalTextAndRejectsNonUtcTimestamps()
    {
        TimelineEntry entry = new(
            Guid.NewGuid(),
            "  Work  ",
            "  Details  ",
            "  Focus  ",
            null,
            Start,
            Start.AddHours(1),
            Start);

        Assert.Equal("Work", entry.Title);
        Assert.Equal("Details", entry.Description);
        Assert.Equal("Focus", entry.Category);
        Assert.Throws<ArgumentException>(() => new TimelineEntry(
            Guid.NewGuid(),
            "Invalid",
            null,
            null,
            null,
            Start.ToOffset(TimeSpan.FromHours(8)),
            null,
            Start));
    }

    [Fact]
    public void DeletedEntryCannotBeChanged()
    {
        TimelineEntry entry = Create(Start.AddHours(1));
        entry.Delete(Start.AddHours(2));

        Assert.True(entry.IsDeleted);
        Assert.Throws<InvalidOperationException>(() => entry.Update(
            "Changed",
            null,
            null,
            null,
            Start,
            Start.AddHours(1),
            Start.AddHours(3)));
    }

    private static TimelineEntry Create(DateTimeOffset? endAtUtc) => new(
        Guid.NewGuid(),
        "Timeline entry",
        null,
        null,
        null,
        Start,
        endAtUtc,
        Start);
}
