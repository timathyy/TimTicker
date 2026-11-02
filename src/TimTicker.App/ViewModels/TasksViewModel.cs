using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimTicker.Application.Tasks;
using TimTicker.App.Services;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.App.ViewModels;

public sealed partial class TasksViewModel(
    ITaskService taskService,
    IDetailNavigationService detailNavigationService) : ViewModelBase
{
    public ObservableCollection<TaskSummary> Tasks { get; } = [];

    [ObservableProperty]
    private bool _hasTasks;

    [ObservableProperty]
    private DomainTaskStatus? _selectedStatus;

    [ObservableProperty]
    private TaskSortOrder _selectedSortOrder = TaskSortOrder.CreatedAtDescending;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public string EmptyStateTitle => SelectedStatus switch
    {
        null => "还没有任何任务",
        DomainTaskStatus.Pending => "任务池还是空的",
        DomainTaskStatus.Completed => "还没有已完成任务",
        _ => "没有任务",
    };

    public string EmptyStateDescription => SelectedStatus switch
    {
        null => "创建第一项任务，开始整理接下来要做的事情。",
        DomainTaskStatus.Pending => "随时记下接下来要做的事，不必依赖记忆。",
        DomainTaskStatus.Completed => "完成的任务会保留在这里，方便回顾。",
        _ => string.Empty,
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

    public async Task CreateTaskAsync(CreateTaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        await taskService.CreateAsync(request);
        await LoadTasksAsync();
    }

    public async Task UpdateTaskAsync(UpdateTaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        await taskService.UpdateAsync(request);
        await LoadTasksAsync();
    }

    public async Task ShowTaskDetailsAsync(Guid taskId, System.Windows.Window? owner)
    {
        if (await detailNavigationService.ShowTaskAsync(taskId, owner))
        {
            await LoadTasksAsync();
        }
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

    public async Task DeleteTaskAsync(TaskSummary task)
    {
        ArgumentNullException.ThrowIfNull(task);
        await taskService.DeleteAsync(task.Id);
        await LoadTasksAsync();
    }

    partial void OnSelectedStatusChanged(DomainTaskStatus? value) => _ = LoadTasksAsync();

    partial void OnSelectedSortOrderChanged(TaskSortOrder value) => _ = LoadTasksAsync();

    partial void OnSearchTextChanged(string value) => _ = LoadTasksAsync();
}
