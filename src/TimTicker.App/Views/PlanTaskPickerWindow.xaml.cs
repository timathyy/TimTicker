using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TimTicker.Application.Planning;
using TimTicker.App.ViewModels;

namespace TimTicker.App.Views;

public partial class PlanTaskPickerWindow : Window
{
    public PlanTaskPickerWindow(IReadOnlyList<DailyPlanTaskCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        InitializeComponent();
        Candidates = new ObservableCollection<PlanTaskCandidateViewModel>(
            candidates.Select(candidate => new PlanTaskCandidateViewModel(candidate)));
        DataContext = this;
    }

    public ObservableCollection<PlanTaskCandidateViewModel> Candidates { get; }

    public IReadOnlyList<Guid> SelectedTaskIds { get; private set; } = [];

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        Guid[] selectedIds = Candidates.Where(candidate => candidate.IsSelected).Select(candidate => candidate.TaskId).ToArray();
        if (selectedIds.Length == 0)
        {
            MessageBox.Show(this, "请至少选择一项任务。", "TimTicker", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedTaskIds = selectedIds;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
