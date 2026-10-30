using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.App.ViewModels;

public sealed partial class TasksViewModel(ITaskService taskService) : ViewModelBase
{
    public ObservableCollection<TaskSummary> Tasks { get; } = [];

    public IReadOnlyList<DomainTaskStatus> Statuses { get; } = [DomainTaskStatus.Pending, DomainTaskStatus.Completed, DomainTaskStatus.Archived];

    public IReadOnlyList<TaskSortOrder> SortOrders { get; } =
        [TaskSortOrder.CreatedAtDescending, TaskSortOrder.DueDateAscending, TaskSortOrder.PriorityDescending];

    [ObservableProperty]
    private bool _hasTasks;

    [ObservableProperty]
    private DomainTaskStatus _selectedStatus = DomainTaskStatus.Pending;

    [ObservableProperty]
    private TaskSortOrder _selectedSortOrder = TaskSortOrder.CreatedAtDescending;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isEditorOpen;

    [ObservableProperty]
    private Guid? _editingTaskId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private DateTime? _dueDate;

    [ObservableProperty]
    private TaskPriority _priority;

    [ObservableProperty]
    private string? _editorError;

    public bool IsEditing => EditingTaskId.HasValue;

    public string EditorTitle => IsEditing ? "编辑任务" : "新建任务";

    public string EmptyStateTitle => SelectedStatus switch
    {
        DomainTaskStatus.Pending => "任务池还是空的",
        DomainTaskStatus.Completed => "还没有已完成任务",
        DomainTaskStatus.Archived => "还没有归档任务",
        _ => "没有任务",
    };

    public string EmptyStateDescription => SelectedStatus switch
    {
        DomainTaskStatus.Pending => "随时记下接下来要做的事，不必依赖记忆。",
        DomainTaskStatus.Completed => "完成的任务会保留在这里，方便回顾。",
        DomainTaskStatus.Archived => "归档的任务会保留在这里，随时可以恢复。",
        _ => string.Empty,
    };

    public static string StatusDisplay(DomainTaskStatus status) => status switch
    {
        DomainTaskStatus.Pending => "待完成",
        DomainTaskStatus.Completed => "已完成",
        DomainTaskStatus.Archived => "已归档",
        _ => status.ToString(),
    };

    public static string SortOrderDisplay(TaskSortOrder sortOrder) => sortOrder switch
    {
        TaskSortOrder.CreatedAtDescending => "最近创建",
        TaskSortOrder.DueDateAscending => "截止日期",
        TaskSortOrder.PriorityDescending => "优先级",
        _ => sortOrder.ToString(),
    };

    public static string PriorityDisplay(TaskPriority priority) => priority switch
    {
        TaskPriority.High => "高优先级",
        TaskPriority.Medium => "中优先级",
        TaskPriority.Low => "低优先级",
        _ => "无优先级",
    };

    [RelayCommand]
    public async Task LoadTasksAsync()
    {
        IReadOnlyList<TaskSummary> tasks = await taskService
            .ListAsync(new TaskQuery(SelectedStatus, SearchText, SelectedSortOrder));
        Tasks.Clear();
        foreach (TaskSummary task in tasks)
        {
            Tasks.Add(task);
        }

        HasTasks = Tasks.Count > 0;
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
    }

    [RelayCommand]
    private void NewTask()
    {
        EditingTaskId = null;
        Title = string.Empty;
        Description = string.Empty;
        DueDate = null;
        Priority = TaskPriority.None;
        EditorError = null;
        IsEditorOpen = true;
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(EditorTitle));
    }

    [RelayCommand]
    private void EditTask(TaskSummary? task)
    {
        if (task is null)
        {
            return;
        }

        EditingTaskId = task.Id;
        Title = task.Title;
        Description = task.Description ?? string.Empty;
        DueDate = task.DueDate?.ToDateTime(TimeOnly.MinValue);
        Priority = task.Priority;
        EditorError = null;
        IsEditorOpen = true;
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(EditorTitle));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        try
        {
            DateOnly? dueDate = DueDate is null ? null : DateOnly.FromDateTime(DueDate.Value);
            if (EditingTaskId is Guid taskId)
            {
                await taskService.UpdateAsync(new UpdateTaskRequest(taskId, Title, Description, dueDate, Priority));
            }
            else
            {
                await taskService.CreateAsync(new CreateTaskRequest(Title, Description, dueDate, Priority));
            }

            IsEditorOpen = false;
            EditorError = null;
            await LoadTasksAsync();
        }
        catch (ArgumentException exception)
        {
            EditorError = exception.Message;
        }
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditorOpen = false;
        EditorError = null;
    }

    [RelayCommand]
    private async Task CompleteAsync(TaskSummary? task)
    {
        if (task is not null)
        {
            await taskService.CompleteAsync(task.Id);
            await LoadTasksAsync();
        }
    }

    [RelayCommand]
    private async Task ReopenAsync(TaskSummary? task)
    {
        if (task is not null)
        {
            await taskService.ReopenAsync(task.Id);
            await LoadTasksAsync();
        }
    }

    [RelayCommand]
    private async Task ArchiveAsync(TaskSummary? task)
    {
        if (task is not null)
        {
            await taskService.ArchiveAsync(task.Id);
            await LoadTasksAsync();
        }
    }

    [RelayCommand]
    private async Task RestoreAsync(TaskSummary? task)
    {
        if (task is not null)
        {
            await taskService.RestoreAsync(task.Id);
            await LoadTasksAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(TaskSummary? task)
    {
        if (task is not null)
        {
            await taskService.DeleteAsync(task.Id);
            await LoadTasksAsync();
        }
    }

    partial void OnSelectedStatusChanged(DomainTaskStatus value) => _ = LoadTasksAsync();

    partial void OnSelectedSortOrderChanged(TaskSortOrder value) => _ = LoadTasksAsync();

    partial void OnSearchTextChanged(string value) => _ = LoadTasksAsync();

    private bool CanSave() => !string.IsNullOrWhiteSpace(Title);
}
