using System.Windows;
using System.Windows.Input;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.App.Views;

public partial class TaskEditorWindow : Window
{
    private readonly TaskSummary? _task;

    public TaskEditorWindow(TaskSummary? task = null)
    {
        _task = task;
        InitializeComponent();
        InitializeFields();
        Loaded += (_, _) => TitleTextBox.Focus();
    }

    public CreateTaskRequest? CreateRequest { get; private set; }

    public UpdateTaskRequest? UpdateRequest { get; private set; }

    private void InitializeFields()
    {
        bool isEditing = _task is not null;
        WindowTitleText.Text = isEditing ? "编辑任务" : "新建任务";
        SaveButton.Content = isEditing ? "保存修改" : "创建任务";
        CreatedDateSelector.SelectedDate = (_task?.CreatedDate ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue);

        if (_task is null)
        {
            return;
        }

        TitleTextBox.Text = _task.Title;
        DescriptionTextBox.Text = _task.Description ?? string.Empty;
        HasDueDateCheckBox.IsChecked = _task.DueDate.HasValue;
        DueDateSelector.SelectedDate = _task.DueDate?.ToDateTime(TimeOnly.MinValue);
        PriorityComboBox.SelectedValue = _task.Priority;

        if (_task.Status == DomainTaskStatus.Completed)
        {
            CompletedDateSection.Visibility = Visibility.Visible;
            CompletedDateSelector.SelectedDate = _task.CompletedDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string title = TitleTextBox.Text.Trim();
        if (title.Length == 0)
        {
            ShowValidationError("请输入任务标题。", TitleTextBox);
            return;
        }

        if (CreatedDateSelector.SelectedDate is not DateTime createdDateValue)
        {
            ShowValidationError("请选择创建日期。", CreatedDateSelector);
            return;
        }

        DateOnly? dueDate = HasDueDateCheckBox.IsChecked == true && DueDateSelector.SelectedDate is DateTime dueDateValue
            ? DateOnly.FromDateTime(dueDateValue)
            : null;
        if (HasDueDateCheckBox.IsChecked == true && dueDate is null)
        {
            ShowValidationError("请选择截止日期，或取消勾选“设置截止日期”。", DueDateSelector);
            return;
        }

        DateOnly? completedDate = null;
        if (_task?.Status == DomainTaskStatus.Completed)
        {
            if (CompletedDateSelector.SelectedDate is not DateTime completedDateValue)
            {
                ShowValidationError("请选择完成日期。", CompletedDateSelector);
                return;
            }

            completedDate = DateOnly.FromDateTime(completedDateValue);
        }

        TaskPriority priority = PriorityComboBox.SelectedValue is TaskPriority selectedPriority
            ? selectedPriority
            : TaskPriority.None;
        DateOnly createdDate = DateOnly.FromDateTime(createdDateValue);

        if (_task is null)
        {
            CreateRequest = new CreateTaskRequest(title, DescriptionTextBox.Text, createdDate, dueDate, priority);
        }
        else
        {
            UpdateRequest = new UpdateTaskRequest(
                _task.Id,
                title,
                DescriptionTextBox.Text,
                createdDate,
                dueDate,
                priority,
                completedDate);
        }

        DialogResult = true;
    }

    private void ShowValidationError(string message, UIElement control)
    {
        ValidationMessage.Text = message;
        control.Focus();
    }

    private void HasDueDateCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (DueDateSelector is null)
        {
            return;
        }

        bool hasDueDate = HasDueDateCheckBox.IsChecked == true;
        DueDateSelector.IsEnabled = hasDueDate;
        if (hasDueDate && DueDateSelector.SelectedDate is null)
        {
            DueDateSelector.SelectedDate = DateTime.Today;
        }
        else if (!hasDueDate)
        {
            DueDateSelector.SelectedDate = null;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SaveButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }
}
