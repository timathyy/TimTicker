using TimTicker.Application.Settings;

namespace TimTicker.Application.Tests;

public sealed class DayBoundaryTests
{
    public static TheoryData<DayEndTime, int> BoundaryCases => new()
    {
        { DayEndTime.Midnight, 0 },
        { DayEndTime.TwoAm, 2 },
        { DayEndTime.FourAm, 4 },
        { DayEndTime.SixAm, 6 },
    };

    [Theory]
    [MemberData(nameof(BoundaryCases))]
    public void BusinessDateChangesExactlyAtConfiguredBoundary(DayEndTime dayEndTime, int boundaryHour)
    {
        DateTime beforeBoundary = boundaryHour == 0
            ? new DateTime(2026, 11, 3, 0, 0, 0)
            : new DateTime(2026, 11, 3, boundaryHour, 0, 0).AddTicks(-1);
        DateTime atBoundary = new(2026, 11, 3, boundaryHour, 0, 0);

        DateOnly beforeDate = DayBoundary.GetBusinessDate(beforeBoundary, dayEndTime);
        DateOnly atDate = DayBoundary.GetBusinessDate(atBoundary, dayEndTime);

        Assert.Equal(boundaryHour == 0 ? new DateOnly(2026, 11, 3) : new DateOnly(2026, 11, 2), beforeDate);
        Assert.Equal(new DateOnly(2026, 11, 3), atDate);
    }

    [Fact]
    public void EarlyClockTimesResolveToFollowingCalendarDateOfBusinessDay()
    {
        DateOnly businessDate = new(2026, 11, 2);

        DateTime earlyMorning = DayBoundary.ResolveLocalTime(businessDate, new TimeOnly(1, 30), DayEndTime.SixAm);
        DateTime evening = DayBoundary.ResolveLocalTime(businessDate, new TimeOnly(23, 0), DayEndTime.SixAm);

        Assert.Equal(new DateTime(2026, 11, 3, 1, 30, 0), earlyMorning);
        Assert.Equal(new DateTime(2026, 11, 2, 23, 0, 0), evening);
    }
}
