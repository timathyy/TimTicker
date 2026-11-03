using System.Windows.Controls;
using System.Windows.Media;
using TimTicker.Application.Planning;
using TimTicker.Application.Tasks;
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
        if (DataContext is not TodayViewModel { IsDayOpen: true } viewModel)
        {
            return;
        }

        IReadOnlyList<DailyPlanTaskCandidate> candidates = await viewModel.GetTaskCandidatesAsync();
        if (candidates.Count == 0)
        {
            System.Windows.MessageBox.Show(
                System.Windows.Window.GetWindow(this),
                viewModel.IsHistoricalEditor
                    ? "任务池中没有可加入当日计划的待完成任务。"
                    : "任务池中没有可加入今日计划的待完成任务。",
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

    private async void NewTaskAndAddButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not TodayViewModel { IsDayOpen: true } viewModel)
        {
            return;
        }

        TaskEditorWindow window = new(defaultCreatedDate: viewModel.CurrentDate)
        {
            Owner = System.Windows.Window.GetWindow(this),
        };
        if (window.ShowDialog() == true && window.CreateRequest is CreateTaskRequest request)
        {
            await viewModel.CreateTaskAndAddAsync(request);
        }
    }

    private async void StartTimelineEntryButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not TodayViewModel { IsDayOpen: true } viewModel || viewModel.HasRunningTimeline)
        {
            return;
        }

        IReadOnlyList<TimelineTaskOption> taskOptions = await viewModel.GetTimelineTaskOptionsAsync();
        TimelineEntryEditorWindow window = new(
            taskOptions,
            viewModel.CurrentDate,
            viewModel.CurrentDayEndTime,
            startMode: true)
        {
            Owner = System.Windows.Window.GetWindow(this),
        };
        if (window.ShowDialog() == true && window.StartRequest is StartTimelineEntryRequest request)
        {
            await RunTimelineActionAsync(() => viewModel.StartTimelineEntryAsync(request));
        }
    }

    private async void AddTimelineEntryButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not TodayViewModel { IsDayOpen: true } viewModel)
        {
            return;
        }

        IReadOnlyList<TimelineTaskOption> taskOptions = await viewModel.GetTimelineTaskOptionsAsync();
        TimelineEntryEditorWindow window = new(
            taskOptions,
            viewModel.CurrentDate,
            viewModel.CurrentDayEndTime)
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
            entry.IsRunning ||
            entry.IsReadOnly)
        {
            return;
        }

        IReadOnlyList<TimelineTaskOption> taskOptions = await viewModel.GetTimelineTaskOptionsAsync();
        TimelineEntryEditorWindow window = new(
            taskOptions,
            entry.Summary.RecordDate,
            viewModel.CurrentDayEndTime,
            entry.Summary)
        {
            Owner = System.Windows.Window.GetWindow(this),
        };
        if (window.ShowDialog() == true && window.UpdateRequest is UpdateTimelineEntryRequest request)
        {
            await RunTimelineActionAsync(() => viewModel.UpdateTimelineEntryAsync(request));
        }
    }

    private async void PlanTaskDetailButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TodayPlanItemViewModel item } && DataContext is TodayViewModel viewModel)
        {
            await viewModel.ShowTaskDetailsAsync(item.TaskId, System.Windows.Window.GetWindow(this));
        }
    }

    private async void TimelineEntryDetailButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TodayTimelineItemViewModel entry } && DataContext is TodayViewModel viewModel)
        {
            await viewModel.ShowTimelineEntryDetailsAsync(entry.Id, System.Windows.Window.GetWindow(this));
        }
    }

    private async void LinkedTaskCard_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FindAncestor<Button>(e.OriginalSource as System.Windows.DependencyObject) is not null ||
            sender is not Border { DataContext: TimelineLinkedTaskViewModel task } ||
            DataContext is not TodayViewModel viewModel)
        {
            return;
        }

        await viewModel.ShowTaskDetailsAsync(task.TaskId, System.Windows.Window.GetWindow(this));
        e.Handled = true;
    }

    private async void DeleteTimelineEntryButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TodayTimelineItemViewModel entry } ||
            DataContext is not TodayViewModel viewModel ||
            entry.IsReadOnly)
        {
            return;
        }

        DeleteTimelineEntryConfirmationWindow window = new(entry.Title)
        {
            Owner = System.Windows.Window.GetWindow(this),
        };
        if (window.ShowDialog() == true)
        {
            await RunTimelineActionAsync(() => viewModel.DeleteTimelineEntryAsync(entry.Id));
        }
    }

    private async void EndDayButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not TodayViewModel { CanEndDay: true } viewModel)
        {
            return;
        }

        EndDayReviewWindow window = new(
            viewModel.CompletedPlanItemCount,
            viewModel.TotalPlanItemCount,
            viewModel.TimelineEntryCount,
            viewModel.TotalRecordedTimeText,
            viewModel.CurrentReviewText)
        {
            Owner = System.Windows.Window.GetWindow(this),
        };
        if (window.ShowDialog() == true)
        {
            await viewModel.CloseTodayAsync(window.ReviewText);
        }
    }

    private async void ReopenDayButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is TodayViewModel { IsDayClosed: true } viewModel)
        {
            await viewModel.ReopenTodayAsync();
        }
    }

    private void ViewDaySummaryButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not TodayViewModel { IsDayClosed: true } viewModel)
        {
            return;
        }

        EndDayReviewWindow window = new(
            viewModel.CompletedPlanItemCount,
            viewModel.TotalPlanItemCount,
            viewModel.TimelineEntryCount,
            viewModel.TotalRecordedTimeText,
            viewModel.CurrentReviewText,
            readOnly: true)
        {
            Owner = System.Windows.Window.GetWindow(this),
        };
        window.ShowDialog();
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

    private static T? FindAncestor<T>(System.Windows.DependencyObject? source) where T : System.Windows.DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
