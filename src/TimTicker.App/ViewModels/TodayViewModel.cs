using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimTicker.Application.Planning;
using TimTicker.Application.Tasks;
using TimTicker.Application.Timeline;
using TimTicker.App.Services;
using TimTicker.Domain.Tasks;
using TimTicker.Domain.Timeline;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.App.ViewModels;

public sealed partial class TodayViewModel(
    IDailyPlanService planService,
    ITaskService taskService,
    ITimelineService timelineService,
    IDetailNavigationService detailNavigationService,
    TimeProvider timeProvider) : ViewModelBase
{
    public ObservableCollection<TodayPlanItemViewModel> PlanItems { get; } = [];

    public ObservableCollection<TodayTimelineItemViewModel> TimelineItems { get; } = [];

    [ObservableProperty]
    private string _dateText = string.Empty;

    [ObservableProperty]
    private bool _hasPlanItems;

    [ObservableProperty]
    private string _planItemCountText = "0 项";

    [ObservableProperty]
    private bool _hasTimelineItems;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopTimelineCommand))]
    private bool _hasRunningTimeline;

    [ObservableProperty]
    private string _runningTimelineTitle = string.Empty;

    [ObservableProperty]
    private string _totalRecordedTimeText = "已记录 0 分钟";

    [ObservableProperty]
    private string _timelineErrorMessage = string.Empty;

    public async Task<IReadOnlyList<DailyPlanTaskCandidate>> GetTaskCandidatesAsync() =>
        await planService.ListTodayCandidatesAsync();

    public Task<IReadOnlyList<TimelineTaskOption>> GetTimelineTaskOptionsAsync() =>
        timelineService.ListTaskOptionsAsync();

    [RelayCommand]
    public async Task LoadTodayAsync()
    {
        DateOnly today = LocalToday();
        DateText = today.ToDateTime(TimeOnly.MinValue).ToString("M月d日，dddd", CultureInfo.GetCultureInfo("zh-CN"));
        ApplyPlan(await planService.GetTodayAsync());
        ApplyTimeline(await timelineService.GetTodayAsync());
    }

    public async Task AddSelectedTasksAsync(IEnumerable<Guid> taskIds) =>
        ApplyPlan(await planService.AddTasksToTodayAsync(taskIds));

    public async Task CreateTaskAndAddAsync(CreateTaskRequest request) =>
        ApplyPlan(await planService.CreateTaskAndAddToTodayAsync(request));

    public async Task StartTimelineEntryAsync(StartTimelineEntryRequest request)
    {
        await timelineService.StartAsync(request);
        TimelineErrorMessage = string.Empty;
        ApplyTimeline(await timelineService.GetTodayAsync());
    }

    public async Task CreateTimelineEntryAsync(CreateTimelineEntryRequest request)
    {
        await timelineService.CreateAsync(request);
        ApplyTimeline(await timelineService.GetTodayAsync());
    }

    public async Task UpdateTimelineEntryAsync(UpdateTimelineEntryRequest request)
    {
        await timelineService.UpdateAsync(request);
        ApplyTimeline(await timelineService.GetTodayAsync());
    }

    public async Task DeleteTimelineEntryAsync(Guid id)
    {
        await timelineService.DeleteAsync(id);
        ApplyTimeline(await timelineService.GetTodayAsync());
    }

    public async Task ShowTaskDetailsAsync(Guid taskId, System.Windows.Window? owner)
    {
        if (await detailNavigationService.ShowTaskAsync(taskId, owner))
        {
            ApplyPlan(await planService.GetTodayAsync());
            ApplyTimeline(await timelineService.GetTodayAsync());
        }
    }

    public async Task ShowTimelineEntryDetailsAsync(Guid entryId, System.Windows.Window? owner)
    {
        if (await detailNavigationService.ShowTimelineEntryAsync(entryId, owner))
        {
            ApplyPlan(await planService.GetTodayAsync());
            ApplyTimeline(await timelineService.GetTodayAsync());
        }
    }

    [RelayCommand]
    private async Task CompleteLinkedTaskAsync(TimelineLinkedTaskViewModel? item)
    {
        if (item?.Status != DomainTaskStatus.Pending)
        {
            return;
        }

        await taskService.CompleteAsync(item.TaskId);
        ApplyPlan(await planService.GetTodayAsync());
        ApplyTimeline(await timelineService.GetTodayAsync());
    }

    [RelayCommand]
    private async Task RemoveLinkedTaskAsync(TimelineLinkedTaskViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        await timelineService.RemoveTaskAsync(item.TimelineEntryId, item.TaskId);
        ApplyTimeline(await timelineService.GetTodayAsync());
    }

    [RelayCommand]
    private async Task CompleteAsync(TodayPlanItemViewModel? item)
    {
        if (item?.Status == DomainTaskStatus.Pending)
        {
            await taskService.CompleteAsync(item.TaskId);
            ApplyPlan(await planService.GetTodayAsync());
        }
    }

    [RelayCommand]
    private async Task ReopenAsync(TodayPlanItemViewModel? item)
    {
        if (item?.Status == DomainTaskStatus.Completed)
        {
            await taskService.ReopenAsync(item.TaskId);
            ApplyPlan(await planService.GetTodayAsync());
        }
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

    [RelayCommand(CanExecute = nameof(CanStopTimeline))]
    private async Task StopTimelineAsync()
    {
        try
        {
            await timelineService.StopCurrentAsync();
            TimelineErrorMessage = string.Empty;
            ApplyTimeline(await timelineService.GetTodayAsync());
        }
        catch (TimelineOverlapException)
        {
            TimelineErrorMessage = "停止时间与已有记录重叠，请先调整冲突记录。";
        }
        catch (ArgumentException)
        {
            TimelineErrorMessage = "记录时间过短，请稍后再停止。";
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

    private void ApplyTimeline(TimelineDaySummary timeline)
    {
        TimelineItems.Clear();
        foreach (TimelineEntrySummary entry in timeline.Entries)
        {
            TimelineItems.Add(new TodayTimelineItemViewModel(entry, timeline.Date));
        }

        HasTimelineItems = TimelineItems.Count > 0;
        TodayTimelineItemViewModel? running = TimelineItems.SingleOrDefault(item => item.IsRunning);
        HasRunningTimeline = running is not null;
        RunningTimelineTitle = running?.Title ?? string.Empty;
        TotalRecordedTimeText = $"已记录 {FormatDuration(timeline.TotalDuration)}";
    }

    private bool CanStopTimeline() => HasRunningTimeline;

    private static string FormatDuration(TimeSpan duration)
    {
        int totalMinutes = Math.Max(0, (int)Math.Round(duration.TotalMinutes));
        return totalMinutes >= 60
            ? $"{totalMinutes / 60} 小时 {totalMinutes % 60} 分钟"
            : $"{totalMinutes} 分钟";
    }

    private DateOnly LocalToday() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
}

public sealed class TodayTimelineItemViewModel
{
    public TodayTimelineItemViewModel(TimelineEntrySummary entry, DateOnly displayDate)
    {
        Summary = entry;
        Id = entry.Id;
        Title = entry.Title;
        Description = entry.Description;
        CategoryText = entry.Category ?? string.Empty;
        TimeRangeText = $"{FormatLocalTime(entry.StartLocal, displayDate)} — " +
            (entry.EndLocal.HasValue ? FormatLocalTime(entry.EndLocal.Value, displayDate) : "进行中");
        DurationText = entry.IsRunning ? "正在记录" : FormatDuration(entry.DayDuration);
        IsRunning = entry.IsRunning;
        IsTimerEntry = entry.Source == TimelineEntrySource.Timer;
        StartLocal = entry.StartLocal;
        EndLocal = entry.EndLocal;
        LinkedTasks = new ObservableCollection<TimelineLinkedTaskViewModel>(
            entry.LinkedTasks.Select(task => new TimelineLinkedTaskViewModel(entry.Id, task)));
        HasLinkedTasks = LinkedTasks.Count > 0;
    }

    public TimelineEntrySummary Summary { get; }

    public Guid Id { get; }

    public string Title { get; }

    public string? Description { get; }

    public string CategoryText { get; }

    public string TimeRangeText { get; }

    public string DurationText { get; }

    public bool IsRunning { get; }

    public bool IsTimerEntry { get; }

    public DateTime StartLocal { get; }

    public DateTime? EndLocal { get; }

    public ObservableCollection<TimelineLinkedTaskViewModel> LinkedTasks { get; }

    public bool HasLinkedTasks { get; }

    private static string FormatLocalTime(DateTime value, DateOnly displayDate) =>
        DateOnly.FromDateTime(value) == displayDate
            ? value.ToString("HH:mm", CultureInfo.InvariantCulture)
            : value.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string FormatDuration(TimeSpan duration)
    {
        int totalMinutes = Math.Max(0, (int)Math.Round(duration.TotalMinutes));
        return totalMinutes >= 60
            ? $"{totalMinutes / 60}小时{totalMinutes % 60}分钟"
            : $"{totalMinutes}分钟";
    }
}

public sealed class TimelineLinkedTaskViewModel
{
    public TimelineLinkedTaskViewModel(Guid timelineEntryId, TaskSummary task)
    {
        TimelineEntryId = timelineEntryId;
        TaskId = task.Id;
        Title = task.Title;
        Status = task.Status;
        CanComplete = task.Status == DomainTaskStatus.Pending;
        StatusText = task.Status == DomainTaskStatus.Completed ? "已完成" : "待完成";
        DueDateText = task.DueDate.HasValue ? $"截止 {task.DueDate:yyyy-MM-dd}" : "无截止日期";
    }

    public Guid TimelineEntryId { get; }

    public Guid TaskId { get; }

    public string Title { get; }

    public DomainTaskStatus Status { get; }

    public bool CanComplete { get; }

    public string StatusText { get; }

    public string DueDateText { get; }
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
        Status = item.Status;
        StatusText = item.Status == DomainTaskStatus.Completed ? "已完成" : "待完成";
        IsOverdue = item.IsOverdue && item.Status == DomainTaskStatus.Pending;
        CanMoveUp = index > 0;
        CanMoveDown = index < count - 1;
    }

    public Guid TaskId { get; }

    public string Title { get; }

    public string DueDateText { get; }

    public string PriorityText { get; }

    public DomainTaskStatus Status { get; }

    public string StatusText { get; }

    public bool IsOverdue { get; }

    public bool CanMoveUp { get; }

    public bool CanMoveDown { get; }
}
