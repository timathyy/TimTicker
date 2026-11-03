using System.Windows;
using TimTicker.Application.Planning;
using TimTicker.Application.Settings;
using TimTicker.Application.Tasks;
using TimTicker.Application.Timeline;
using TimTicker.App.ViewModels;
using TimTicker.App.Views;

namespace TimTicker.App.Services;

public sealed class DayEditorService(
    IDailyPlanService planService,
    ITaskService taskService,
    ITimelineService timelineService,
    IDetailNavigationService detailNavigationService,
    IDayBoundarySettings dayBoundarySettings) : IDayEditorService
{
    public void Show(DateOnly selectedDate, Window? owner)
    {
        TodayViewModel viewModel = new(
            planService,
            taskService,
            timelineService,
            detailNavigationService,
            dayBoundarySettings,
            selectedDate);
        DayEditorWindow window = new(selectedDate, viewModel) { Owner = owner };
        window.ShowDialog();
    }
}
