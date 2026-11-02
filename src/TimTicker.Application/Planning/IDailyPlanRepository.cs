using TimTicker.Domain.Planning;

namespace TimTicker.Application.Planning;

public interface IDailyPlanRepository
{
    Task<DailyPlan?> FindAsync(DateOnly planDate, CancellationToken cancellationToken = default);

    Task<DailyPlan> GetOrCreateAsync(DateOnly planDate, DateTimeOffset createdAtUtc, CancellationToken cancellationToken = default);

    Task UpdateAsync(DailyPlan plan, CancellationToken cancellationToken = default);
}
