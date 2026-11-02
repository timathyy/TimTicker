using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimTicker.Application.Planning;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.App.ViewModels;

public sealed partial class TodayViewModel(IDailyPlanService planService, TimeProvider timeProvider) : ViewModelBase
{
    public ObservableCollection<TodayPlanItemViewModel> PlanItems { get; } = [];

    [ObservableProperty]
    private string _dateText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuickAddTaskCommand))]
    private string _quickTaskTitle = string.Empty;

    [ObservableProperty]
    private bool _hasPlanItems;

    [ObservableProperty]
    private string _planItemCountText = "0 项";

    public async Task<IReadOnlyList<DailyPlanTaskCandidate>> GetTaskCandidatesAsync() =>
        await planService.ListTodayCandidatesAsync();

    [RelayCommand]
    public async Task LoadTodayAsync()
    {
        DateOnly today = LocalToday();
        DateText = today.ToDateTime(TimeOnly.MinValue).ToString("M月d日，dddd", CultureInfo.GetCultureInfo("zh-CN"));
        ApplyPlan(await planService.GetTodayAsync());
    }

    public async Task AddSelectedTasksAsync(IEnumerable<Guid> taskIds) =>
        ApplyPlan(await planService.AddTasksToTodayAsync(taskIds));

    [RelayCommand(CanExecute = nameof(CanQuickAddTask))]
    private async Task QuickAddTaskAsync()
    {
        DailyPlanSummary plan = await planService.CreateTaskAndAddToTodayAsync(new CreateTaskRequest(
            QuickTaskTitle,
            null,
            LocalToday(),
            null,
            TaskPriority.None));
        QuickTaskTitle = string.Empty;
        ApplyPlan(plan);
    }

    [RelayCommand]
    private async Task MoveUpAsync(TodayPlanItemViewModel? item)
    {
        if (item?.CanMoveUp == true)
        {
            ApplyPlan(await planService.MoveTodayTaskAsync(item.TaskId, PlanMoveDirection.Up));
        }
    }

    [RelayCommand]
    private async Task MoveDownAsync(TodayPlanItemViewModel? item)
    {
        if (item?.CanMoveDown == true)
        {
            ApplyPlan(await planService.MoveTodayTaskAsync(item.TaskId, PlanMoveDirection.Down));
        }
    }

    [RelayCommand]
    private async Task RemoveFromPlanAsync(TodayPlanItemViewModel? item)
    {
        if (item is not null)
        {
            ApplyPlan(await planService.RemoveTaskFromTodayAsync(item.TaskId));
        }
    }

    private void ApplyPlan(DailyPlanSummary plan)
    {
        PlanItems.Clear();
        for (int index = 0; index < plan.Items.Count; index++)
        {
            PlanItems.Add(new TodayPlanItemViewModel(plan.Items[index], index, plan.Items.Count));
        }

        HasPlanItems = PlanItems.Count > 0;
        PlanItemCountText = $"{PlanItems.Count} 项";
    }

    private bool CanQuickAddTask() => !string.IsNullOrWhiteSpace(QuickTaskTitle);

    private DateOnly LocalToday() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
}

public sealed class TodayPlanItemViewModel
{
    public TodayPlanItemViewModel(DailyPlanItemSummary item, int index, int count)
    {
        TaskId = item.TaskId;
        Title = item.Title;
        DueDateText = item.DueDate.HasValue ? $"截止 {item.DueDate:yyyy-MM-dd}" : "无截止日期";
        PriorityText = item.Priority switch
        {
            TaskPriority.High => "高优先级",
            TaskPriority.Medium => "中优先级",
            TaskPriority.Low => "低优先级",
            _ => "无优先级",
        };
        StatusText = item.Status == DomainTaskStatus.Completed ? "已完成" : "待完成";
        IsOverdue = item.IsOverdue && item.Status == DomainTaskStatus.Pending;
        CanMoveUp = index > 0;
        CanMoveDown = index < count - 1;
    }

    public Guid TaskId { get; }

    public string Title { get; }

    public string DueDateText { get; }

    public string PriorityText { get; }

    public string StatusText { get; }

    public bool IsOverdue { get; }

    public bool CanMoveUp { get; }

    public bool CanMoveDown { get; }
}
