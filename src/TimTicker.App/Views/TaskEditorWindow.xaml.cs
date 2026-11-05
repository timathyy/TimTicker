using System.Windows;
using System.Windows.Input;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;
using TimTicker.App.ViewModels;

namespace TimTicker.App.Views;

public partial class TaskEditorWindow : Window
{
    private readonly TaskSummary? _task;
    private readonly DateOnly _defaultCreatedDate;
    private readonly IReadOnlyList<TaskFolderOption> _folderOptions;
    private readonly Guid? _defaultFolderId;

    public TaskEditorWindow(
        TaskSummary? task = null,
        DateOnly? defaultCreatedDate = null,
        IReadOnlyList<TaskFolderOption>? folderOptions = null,
        Guid? defaultFolderId = null)
    {
        _task = task;
        _defaultCreatedDate = defaultCreatedDate ?? DateOnly.FromDateTime(DateTime.Today);
        IReadOnlyList<TaskFolderOption> availableFolders = folderOptions ?? [new TaskFolderOption("根目录", null)];
        _folderOptions = task?.FolderId is Guid currentFolderId && availableFolders.All(folder => folder.FolderId != currentFolderId)
            ? [.. availableFolders, new TaskFolderOption("当前文件夹", currentFolderId)]
            : availableFolders;
        _defaultFolderId = defaultFolderId;
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
        CreatedDateSelector.SelectedDate = (_task?.CreatedDate ?? _defaultCreatedDate).ToDateTime(TimeOnly.MinValue);
        FolderComboBox.ItemsSource = _folderOptions;
        FolderComboBox.SelectedValue = _task?.FolderId ?? _defaultFolderId;
        if (FolderComboBox.SelectedItem is null)
        {
            FolderComboBox.SelectedIndex = 0;
        }

        if (_task is null)
        {
            return;
        }

        TitleTextBox.Text = _task.Title;
        DescriptionTextBox.Text = _task.Description ?? string.Empty;
        HasDueDateCheckBox.IsChecked = _task.DueDate.HasValue;
        DueDateSelector.SelectedDate = _task.DueDate?.ToDateTime(TimeOnly.MinValue);
        PriorityComboBox.SelectedValue = _task.Priority;
        if (_task.TemplateId.HasValue)
        {
            FolderComboBox.IsEnabled = false;
            GeneratedTaskHint.Visibility = Visibility.Visible;
        }

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
        Guid? folderId = FolderComboBox.SelectedItem is TaskFolderOption folder ? folder.FolderId : null;

        if (_task is null)
        {
            CreateRequest = new CreateTaskRequest(title, DescriptionTextBox.Text, createdDate, dueDate, priority, folderId);
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
                completedDate,
                folderId);
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
