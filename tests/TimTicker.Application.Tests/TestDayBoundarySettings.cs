using TimTicker.Application.Settings;

namespace TimTicker.Application.Tests;

internal sealed class TestDayBoundarySettings(
    DayEndTime currentDayEndTime = DayEndTime.Midnight) : IDayBoundarySettings
{
    public DayEndTime CurrentDayEndTime { get; private set; } = currentDayEndTime;

    public void SetDayEndTime(DayEndTime value)
    {
        if (!DayBoundary.IsSupported(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        CurrentDayEndTime = value;
    }
}
