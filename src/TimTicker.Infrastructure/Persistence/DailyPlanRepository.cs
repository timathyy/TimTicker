using Microsoft.EntityFrameworkCore;
using TimTicker.Application.Planning;
using TimTicker.Domain.Planning;

namespace TimTicker.Infrastructure.Persistence;

public sealed class DailyPlanRepository(IDbContextFactory<TimTickerDbContext> contextFactory) : IDailyPlanRepository
{
    public async Task<DailyPlan> GetOrCreateAsync(
        DateOnly planDate,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        DailyPlan? existing = await context.DailyPlans
            .AsNoTracking()
            .Include(plan => plan.Items)
            .SingleOrDefaultAsync(plan => plan.Date == planDate, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        DailyPlan plan = new(Guid.NewGuid(), planDate, createdAtUtc);
        context.DailyPlans.Add(plan);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return plan;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            DailyPlan? concurrentlyCreated = await context.DailyPlans
                .AsNoTracking()
                .Include(candidate => candidate.Items)
                .SingleOrDefaultAsync(candidate => candidate.Date == planDate, cancellationToken)
                .ConfigureAwait(false);
            if (concurrentlyCreated is null)
            {
                throw;
            }

            return concurrentlyCreated;
        }
    }

    public async Task UpdateAsync(DailyPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        await using TimTickerDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        HashSet<Guid> persistedItemIds = (await context.DailyPlanItems
            .AsNoTracking()
            .Where(item => item.DailyPlanId == plan.Id)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false))
            .ToHashSet();
        context.DailyPlans.Update(plan);
        foreach (DailyPlanItem item in plan.Items.Where(item => !persistedItemIds.Contains(item.Id)))
        {
            context.Entry(item).State = EntityState.Added;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
