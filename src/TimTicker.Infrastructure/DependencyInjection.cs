using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimTicker.Application.Abstractions;
using TimTicker.Application.Calendar;
using TimTicker.Application.Planning;
using TimTicker.Application.Settings;
using TimTicker.Application.Tasks;
using TimTicker.Application.Timeline;
using TimTicker.Infrastructure.Persistence;
using TimTicker.Infrastructure.Storage;

namespace TimTicker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTimTickerInfrastructure(
        this IServiceCollection services,
        string databasePath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        string fullDatabasePath = Path.GetFullPath(databasePath);
        SqliteConnectionStringBuilder connectionString = new()
        {
            DataSource = fullDatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
        };

        services.AddDbContextFactory<TimTickerDbContext>(options =>
            options.UseSqlite(connectionString.ConnectionString));
        services.AddSingleton<IAppDatabaseInitializer, AppDatabaseInitializer>();
        services.AddSingleton<ITaskItemRepository, TaskItemRepository>();
        services.AddSingleton<IDailyPlanRepository, DailyPlanRepository>();
        services.AddSingleton<ITimelineEntryRepository, TimelineEntryRepository>();
        services.AddSingleton<ICalendarReadRepository, CalendarReadRepository>();
        services.AddSingleton<IDayBoundarySettings>(_ =>
            new JsonDayBoundarySettings(Path.GetDirectoryName(fullDatabasePath)!));

        return services;
    }
}
