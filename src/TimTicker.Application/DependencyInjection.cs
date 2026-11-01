using Microsoft.Extensions.DependencyInjection;
using TimTicker.Application.Tasks;

namespace TimTicker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTimTickerApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ITaskService, TaskService>();
        return services;
    }
}
