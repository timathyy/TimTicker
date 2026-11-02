using System.Windows.Controls;
using TimTicker.Application.Planning;
using TimTicker.Application.Timeline;
using TimTicker.App.ViewModels;

namespace TimTicker.App.Views;

public partial class TodayView : UserControl
{
    public TodayView()
    {
        InitializeComponent();
    }

    private void TodayView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is TodayViewModel viewModel)
        {
            viewModel.LoadTodayCommand.Execute(null);
        }
    }

    private async void SelectFromTaskPoolButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not TodayViewModel viewModel)
        {
            return;
        }

        IReadOnlyList<DailyPlanTaskCandidate> candidates = await viewModel.GetTaskCandidatesAsync();
        if (candidates.Count == 0)
        {
            System.Windows.MessageBox.Show(
                System.Windows.Window.GetWindow(this),
                "任务池中没有可加入今日计划的待完成任务。",
                "TimTicker",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        PlanTaskPickerWindow window = new(candidates)
        {
            Owner = System.Windows.Window.GetWindow(this),
        };
        if (window.ShowDialog() == true)
        {
            await viewModel.AddSelectedTasksAsync(window.SelectedTaskIds);
        }
    }

    private async void AddTimelineEntryButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not TodayViewModel viewModel)
        {
            return;
        }

        IReadOnlyList<TimelineTaskOption> taskOptions = await viewModel.GetTimelineTaskOptionsAsync();
        TimelineEntryEditorWindow window = new(taskOptions)
        {
            Owner = System.Windows.Window.GetWindow(this),
        };
        if (window.ShowDialog() == true && window.CreateRequest is CreateTimelineEntryRequest request)
        {
            await RunTimelineActionAsync(() => viewModel.CreateTimelineEntryAsync(request));
        }
    }

    private async void EditTimelineEntryButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TodayTimelineItemViewModel entry } ||
            DataContext is not TodayViewModel viewModel ||
            entry.IsRunning)
        {
            return;
        }

        IReadOnlyList<TimelineTaskOption> taskOptions = await viewModel.GetTimelineTaskOptionsAsync();
        TimelineEntryEditorWindow window = new(taskOptions, entry)
        {
            Owner = System.Windows.Window.GetWindow(this),
        };
        if (window.ShowDialog() == true && window.UpdateRequest is UpdateTimelineEntryRequest request)
        {
            await RunTimelineActionAsync(() => viewModel.UpdateTimelineEntryAsync(request));
        }
    }

    private async void DeleteTimelineEntryButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TodayTimelineItemViewModel entry } ||
            DataContext is not TodayViewModel viewModel)
        {
            return;
        }

        System.Windows.MessageBoxResult result = System.Windows.MessageBox.Show(
            System.Windows.Window.GetWindow(this),
            "确定删除这条时间记录吗？此操作不会删除关联任务。",
            "删除时间记录",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            await RunTimelineActionAsync(() => viewModel.DeleteTimelineEntryAsync(entry.Id));
        }
    }

    private async Task RunTimelineActionAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (TimelineOverlapException)
        {
            ShowTimelineError("该时间段与已有记录重叠，请调整开始或结束时间。");
        }
        catch (ArgumentException)
        {
            ShowTimelineError("时间记录无效，请检查标题、日期与时间。");
        }
        catch (InvalidOperationException)
        {
            ShowTimelineError("当前记录状态已发生变化，请刷新后重试。");
        }
    }

    private void ShowTimelineError(string message) => System.Windows.MessageBox.Show(
        System.Windows.Window.GetWindow(this),
        message,
        "TimTicker",
        System.Windows.MessageBoxButton.OK,
        System.Windows.MessageBoxImage.Information);
}
