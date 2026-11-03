namespace TimTicker.Application.Calendar;

public interface ICalendarReadRepository
{
    Task<CalendarRangeData> ReadRangeAsync(
        DateOnly startDate,
        DateOnly endDateExclusive,
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc,
        CancellationToken cancellationToken = default);
}
