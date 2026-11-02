namespace TimTicker.Application.Settings;

public static class DayBoundary
{
    public const DayEndTime DefaultDayEndTime = DayEndTime.SixAm;

    public static bool IsSupported(DayEndTime value) => value is
        DayEndTime.Midnight or
        DayEndTime.TwoAm or
        DayEndTime.FourAm or
        DayEndTime.SixAm;

    public static DateOnly GetBusinessDate(DateTime localDateTime, DayEndTime dayEndTime)
    {
        Validate(dayEndTime);
        DateOnly calendarDate = DateOnly.FromDateTime(localDateTime);
        int boundaryHour = (int)dayEndTime;
        return boundaryHour != 0 && localDateTime.TimeOfDay < TimeSpan.FromHours(boundaryHour)
            ? calendarDate.AddDays(-1)
            : calendarDate;
    }

    public static DateTime GetStartLocal(DateOnly businessDate, DayEndTime dayEndTime)
    {
        Validate(dayEndTime);
        return businessDate.ToDateTime(new TimeOnly((int)dayEndTime, 0), DateTimeKind.Unspecified);
    }

    public static DateTime GetEndLocal(DateOnly businessDate, DayEndTime dayEndTime) =>
        GetStartLocal(businessDate.AddDays(1), dayEndTime);

    public static DateTime ResolveLocalTime(DateOnly businessDate, TimeOnly time, DayEndTime dayEndTime)
    {
        Validate(dayEndTime);
        int boundaryHour = (int)dayEndTime;
        DateOnly calendarDate = boundaryHour != 0 && time < new TimeOnly(boundaryHour, 0)
            ? businessDate.AddDays(1)
            : businessDate;
        return calendarDate.ToDateTime(time, DateTimeKind.Unspecified);
    }

    public static string ToDisplayText(DayEndTime value) => value switch
    {
        DayEndTime.Midnight => "24:00",
        DayEndTime.TwoAm => "2:00(+1)",
        DayEndTime.FourAm => "4:00(+1)",
        DayEndTime.SixAm => "6:00(+1)",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static void Validate(DayEndTime value)
    {
        if (!IsSupported(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
