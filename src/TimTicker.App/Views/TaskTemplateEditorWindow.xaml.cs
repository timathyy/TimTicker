using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TimTicker.Application.Templates;
using TimTicker.Domain.Tasks;
using TimTicker.Domain.Templates;

namespace TimTicker.App.Views;

public partial class TaskTemplateEditorWindow : Window
{
    public TaskTemplateEditorWindow()
    {
        InitializeComponent();
        StartDateSelector.SelectedDate = DateTime.Today;
        Loaded += (_, _) => NameTextBox.Focus();
        UpdatePreview();
        UpdateScheduleControls();
    }

    public CreateTaskTemplateRequest? Request { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string name = NameTextBox.Text.Trim();
        string pattern = PatternTextBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowError("请输入文件夹名称。", NameTextBox);
            return;
        }

        if (pattern.Length == 0)
        {
            ShowError("请输入任务标题规则。", PatternTextBox);
            return;
        }

        if (!int.TryParse(IntervalTextBox.Text, out int interval) || interval <= 0)
        {
            ShowError("周期倍数必须是大于 0 的整数。", IntervalTextBox);
            return;
        }

        if (StartDateSelector.SelectedDate is not DateTime startDate)
        {
            ShowError("请选择开始日期。", StartDateSelector);
            return;
        }

        TaskPriority priority = PriorityComboBox.SelectedValue is TaskPriority selectedPriority
            ? selectedPriority
            : TaskPriority.None;
        TaskTemplateSchedule schedule = ScheduleComboBox.SelectedValue is TaskTemplateSchedule selectedSchedule
            ? selectedSchedule
            : TaskTemplateSchedule.Manual;
        Request = new CreateTaskTemplateRequest(
            name,
            pattern,
            DescriptionTextBox.Text,
            priority,
            schedule,
            interval,
            DateOnly.FromDateTime(startDate),
            AutoEnabledCheckBox.IsChecked == true);
        DialogResult = true;
    }

    private void ShowError(string message, UIElement control)
    {
        ValidationMessage.Text = message;
        control.Focus();
    }

    private void PatternTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void ScheduleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateScheduleControls();

    private void UpdatePreview()
    {
        if (PreviewText is null || PatternTextBox is null)
        {
            return;
        }

        string pattern = PatternTextBox.Text.Trim();
        PreviewText.Text = pattern.Length == 0
            ? "示例：每日复盘-%y%m%d-第%n次"
            : $"预览：{TaskTitlePatternFormatter.Format(pattern, DateOnly.FromDateTime(DateTime.Today), 1)}";
    }

    private void UpdateScheduleControls()
    {
        if (ScheduleComboBox is null || AutoEnabledCheckBox is null || IntervalTextBox is null)
        {
            return;
        }

        bool automatic = ScheduleComboBox.SelectedValue is TaskTemplateSchedule schedule && schedule != TaskTemplateSchedule.Manual;
        AutoEnabledCheckBox.IsEnabled = automatic;
        AutoEnabledCheckBox.IsChecked = automatic;
        IntervalTextBox.IsEnabled = automatic;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SaveButton_Click(sender, e);
        }
    }
}
