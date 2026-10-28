using Microsoft.Extensions.DependencyInjection;
using TimTicker.App.ViewModels;

namespace TimTicker.App.Services;

public sealed class NavigationService(IServiceProvider serviceProvider) : INavigationService
{
    public ViewModelBase Navigate(AppPage page)
    {
        return page switch
        {
            AppPage.Today => serviceProvider.GetRequiredService<TodayViewModel>(),
            AppPage.Tasks => serviceProvider.GetRequiredService<TasksViewModel>(),
            AppPage.Calendar => serviceProvider.GetRequiredService<CalendarViewModel>(),
            AppPage.Settings => serviceProvider.GetRequiredService<SettingsViewModel>(),
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, "Unknown navigation page."),
        };
    }
}
