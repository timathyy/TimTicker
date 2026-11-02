namespace TimTicker.Application.Settings;

public interface IDayBoundarySettings
{
    DayEndTime CurrentDayEndTime { get; }

    void SetDayEndTime(DayEndTime value);
}
