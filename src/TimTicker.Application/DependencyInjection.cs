using Microsoft.Extensions.DependencyInjection;
using TimTicker.Application.Planning;
using TimTicker.Application.Tasks;
using TimTicker.Application.Timeline;

namespace TimTicker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTimTickerApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITaskService, TaskService>();
        services.AddSingleton<IDailyPlanService, DailyPlanService>();
        services.AddSingleton<ITimelineService, TimelineService>();
        return services;
    }
}
