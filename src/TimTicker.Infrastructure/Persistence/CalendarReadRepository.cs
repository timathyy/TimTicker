using Microsoft.EntityFrameworkCore;
using TimTicker.Application.Calendar;
using TimTicker.Domain.Planning;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.Infrastructure.Persistence;

public sealed class CalendarReadRepository(IDbContextFactory<TimTickerDbContext> contextFactory) : ICalendarReadRepository
{
    public async Task<CalendarRangeData> ReadRangeAsync(
        DateOnly startDate,
        DateOnly endDateExclusive,
        DateTimeOffset startAtUtc,
        DateTimeOffset endAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (startDate == default || endDateExclusive <= startDate)
        {
            throw new ArgumentException("The calendar date range is invalid.", nameof(endDateExclusive));
        }

        if (startAtUtc.Offset != TimeSpan.Zero || endAtUtc.Offset != TimeSpan.Zero || endAtUtc <= startAtUtc)
        {
            throw new ArgumentException("The calendar time range must be an increasing UTC interval.", nameof(endAtUtc));
        }

        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var plans = await context.DailyPlans
            .AsNoTracking()
            .Include(plan => plan.Items)
            .Where(plan => plan.Date >= startDate && plan.Date < endDateExclusive)
            .OrderBy(plan => plan.Date)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid[] activeTaskIds = plans
            .SelectMany(plan => plan.ActiveItems)
            .Select(item => item.TaskItemId)
            .Distinct()
            .ToArray();
        Dictionary<Guid, DomainTaskStatus> taskStatuses = activeTaskIds.Length == 0
            ? []
            : await context.Tasks
                .AsNoTracking()
                .Where(task => activeTaskIds.Contains(task.Id) && !task.IsDeleted)
                .ToDictionaryAsync(task => task.Id, task => task.Status, cancellationToken)
                .ConfigureAwait(false);

        List<CalendarPlanData> planData = [];
        foreach (var plan in plans)
        {
            DailyPlanItem[] countedItems = plan.IsClosed
                ? plan.ActiveItems.ToArray()
                : plan.ActiveItems
                    .Where(item => taskStatuses.ContainsKey(item.TaskItemId))
                    .ToArray();
            int totalCount = countedItems.Length;
            int completedCount = plan.IsClosed
                ? countedItems.Count(item => item.WasCompletedAtClose == true)
                : countedItems.Count(item =>
                    taskStatuses.TryGetValue(item.TaskItemId, out DomainTaskStatus status) &&
                    status == DomainTaskStatus.Completed);
            decimal? completionRate = plan.IsClosed
                ? plan.FinalCompletionRate
                : totalCount == 0 ? null : completedCount / (decimal)totalCount;
            planData.Add(new CalendarPlanData(
                plan.Date,
                plan.IsClosed,
                completedCount,
                totalCount,
                completionRate));
        }

        CalendarTimelineInterval[] intervals = await context.TimelineEntries
            .AsNoTracking()
            .Where(entry => !entry.IsDeleted &&
                            entry.StartAtUtc < endAtUtc &&
                            (entry.EndAtUtc == null || entry.EndAtUtc > startAtUtc))
            .OrderBy(entry => entry.StartAtUtc)
            .Select(entry => new CalendarTimelineInterval(entry.StartAtUtc, entry.EndAtUtc))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return new CalendarRangeData(planData, intervals);
    }
}
