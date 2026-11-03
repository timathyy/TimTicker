namespace TimTicker.Application.Calendar;

public interface ICalendarService
{
    Task<CalendarMonthSummary> GetMonthAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<CalendarDayDetails> GetDayAsync(
        DateOnly calendarDate,
        CancellationToken cancellationToken = default);
}
