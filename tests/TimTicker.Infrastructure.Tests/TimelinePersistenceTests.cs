using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TimTicker.Application;
using TimTicker.Application.Abstractions;
using TimTicker.Application.Timeline;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;
using TimTicker.Domain.Timeline;
using TimTicker.Infrastructure;

namespace TimTicker.Infrastructure.Tests;

public sealed class TimelinePersistenceTests
{
    [Fact]
    public async Task MultipleTaskLinksPersistAcrossServiceProviders()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "timticker.db");
        Directory.CreateDirectory(testRoot);
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero));

        try
        {
            Guid entryId;
            Guid firstTaskId;
            Guid secondTaskId;
            await using (ServiceProvider firstProvider = CreateProvider(databasePath, clock))
            {
                await firstProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
                ITaskService tasks = firstProvider.GetRequiredService<ITaskService>();
                firstTaskId = (await tasks.CreateAsync(new CreateTaskRequest(
                    "First",
                    null,
                    new DateOnly(2026, 11, 2),
                    null,
                    TaskPriority.None))).Id;
                secondTaskId = (await tasks.CreateAsync(new CreateTaskRequest(
                    "Second",
                    null,
                    new DateOnly(2026, 11, 2),
                    null,
                    TaskPriority.None))).Id;
                TimelineEntrySummary entry = await firstProvider.GetRequiredService<ITimelineService>().CreateAsync(
                    new CreateTimelineEntryRequest(
                        "Shared record",
                        null,
                        [firstTaskId, secondTaskId],
                        new DateOnly(2026, 11, 2),
                        new DateTime(2026, 11, 2, 9, 0, 0),
                        new DateTime(2026, 11, 2, 10, 0, 0)));
                entryId = entry.Id;
            }

            await using (ServiceProvider secondProvider = CreateProvider(databasePath, clock))
            {
                await secondProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
                ITimelineService timeline = secondProvider.GetRequiredService<ITimelineService>();
                TimelineEntrySummary recovered = await timeline.GetAsync(entryId);
                Assert.Equal(new[] { firstTaskId, secondTaskId }.Order(), recovered.LinkedTasks.Select(task => task.Id).Order());
                Assert.Equal(new DateOnly(2026, 11, 2), recovered.RecordDate);

                await timeline.RemoveTaskAsync(entryId, firstTaskId);
                Assert.Equal(secondTaskId, Assert.Single((await timeline.GetAsync(entryId)).LinkedTasks).Id);
            }
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

    [Fact]
    public async Task RunningEntrySurvivesNewServiceProviderAndCanBeStopped()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "TimTicker.Tests", Guid.NewGuid().ToString("N"));
        string databasePath = Path.Combine(testRoot, "timticker.db");
        Directory.CreateDirectory(testRoot);
        MutableTimeProvider clock = new(new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero));

        try
        {
            Guid entryId;
            await using (ServiceProvider firstProvider = CreateProvider(databasePath, clock))
            {
                await firstProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
                TimelineEntrySummary started = await firstProvider.GetRequiredService<ITimelineService>()
                    .StartAsync(new StartTimelineEntryRequest("Persist running entry", null, []));
                entryId = started.Id;
            }

            clock.UtcNow = clock.UtcNow.AddHours(1);
            await using (ServiceProvider secondProvider = CreateProvider(databasePath, clock))
            {
                await secondProvider.GetRequiredService<IAppDatabaseInitializer>().InitializeAsync();
                ITimelineService service = secondProvider.GetRequiredService<ITimelineService>();
                TimelineEntrySummary recovered = Assert.Single((await service.GetTodayAsync()).Entries);
                TimelineEntrySummary stopped = await service.StopCurrentAsync();

                Assert.Equal(entryId, recovered.Id);
                Assert.True(recovered.IsRunning);
                Assert.Equal(TimelineEntrySource.Timer, recovered.Source);
                Assert.Equal(entryId, stopped.Id);
                Assert.False(stopped.IsRunning);
                Assert.Equal(TimeSpan.FromHours(1), stopped.DayDuration);
            }
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

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
