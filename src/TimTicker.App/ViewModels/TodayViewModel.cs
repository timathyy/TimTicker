using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;

namespace TimTicker.App.ViewModels;

public sealed partial class TodayViewModel(ITaskService taskService) : ViewModelBase
{
    public string DateText { get; } = DateTime.Today.ToString("M月d日，dddd", CultureInfo.GetCultureInfo("zh-CN"));

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuickAddTaskCommand))]
    private string _quickTaskTitle = string.Empty;

    [RelayCommand(CanExecute = nameof(CanQuickAddTask))]
    private async Task QuickAddTaskAsync()
    {
        await taskService.CreateAsync(new CreateTaskRequest(
            QuickTaskTitle,
            null,
            DateOnly.FromDateTime(DateTime.Today),
            null,
            TaskPriority.None));
        QuickTaskTitle = string.Empty;
    }

    private bool CanQuickAddTask() => !string.IsNullOrWhiteSpace(QuickTaskTitle);
}
