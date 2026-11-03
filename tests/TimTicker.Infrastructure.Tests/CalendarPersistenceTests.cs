using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TimTicker.Application;
using TimTicker.Application.Abstractions;
using TimTicker.Application.Calendar;
using TimTicker.Application.Planning;
using TimTicker.Application.Tasks;
using TimTicker.Application.Timeline;
using TimTicker.Domain.Tasks;
using TimTicker.Domain.Timeline;
using TimTicker.Infrastructure;

namespace TimTicker.Infrastructure.Tests;

public sealed class CalendarPersistenceTests
{
    [Fact]
    public async Task RangeReadReturnsOnlyPlansAndTimelineIntervalsThatCanBeVisible()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "timticker.db");
        Directory.CreateDirectory(testRoot);
        FixedTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));

        try
        {
            await using ServiceProvider provider = CreateProvider(databasePath, clock);
            await provider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
            ITaskItemRepository tasks = provider.GetRequiredService<ITaskItemRepository>();
            TaskItem task = new(
                Guid.NewGuid(),
                "Completed task",
                null,
                new DateOnly(2026, 11, 2),
                null,
                TaskPriority.None,
                clock.GetUtcNow());
            await tasks.AddAsync(task);
            task.Complete(clock.GetUtcNow().AddMinutes(1), new DateOnly(2026, 11, 2));
            await tasks.UpdateAsync(task);

            IDailyPlanRepository plans = provider.GetRequiredService<IDailyPlanRepository>();
            TimTicker.Domain.Planning.DailyPlan inRange = await plans.GetOrCreateAsync(
                new DateOnly(2026, 11, 2),
                clock.GetUtcNow());
            inRange.AddTask(task.Id, clock.GetUtcNow());
            await plans.UpdateAsync(inRange);
            await plans.GetOrCreateAsync(new DateOnly(2026, 12, 2), clock.GetUtcNow());

            ITimelineEntryRepository timeline = provider.GetRequiredService<ITimelineEntryRepository>();
            await timeline.AddAsync(new TimelineEntry(
                Guid.NewGuid(),
                "Crosses into range",
                null,
                [],
                new DateTimeOffset(2026, 10, 31, 23, 30, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 1, 0, 30, 0, TimeSpan.Zero),
                clock.GetUtcNow()));
            await timeline.AddAsync(new TimelineEntry(
                Guid.NewGuid(),
                "Outside range",
                null,
                [],
                new DateTimeOffset(2026, 12, 1, 1, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 12, 1, 2, 0, 0, TimeSpan.Zero),
                clock.GetUtcNow()));

            CalendarRangeData result = await provider.GetRequiredService<ICalendarReadRepository>().ReadRangeAsync(
                new DateOnly(2026, 11, 1),
                new DateOnly(2026, 12, 1),
                new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero));

            CalendarPlanData plan = Assert.Single(result.Plans);
            Assert.Equal(new DateOnly(2026, 11, 2), plan.Date);
            Assert.Equal(1, plan.CompletedCount);
            Assert.Equal(1, plan.TotalCount);
            Assert.Equal(1m, plan.CompletionRate);
            CalendarTimelineInterval interval = Assert.Single(result.TimelineIntervals);
            Assert.Equal(new DateTimeOffset(2026, 10, 31, 23, 30, 0, TimeSpan.Zero), interval.StartAtUtc);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static ServiceProvider CreateProvider(string databasePath, TimeProvider clock)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddTimTickerApplication();
        services.AddSingleton(clock);
        services.AddTimTickerInfrastructure(databasePath);
        return services.BuildServiceProvider();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
