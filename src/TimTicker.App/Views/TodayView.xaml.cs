using System.Windows.Controls;
using TimTicker.Application.Planning;
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
}
