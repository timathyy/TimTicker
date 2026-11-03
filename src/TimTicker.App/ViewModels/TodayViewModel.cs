using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimTicker.Application.Planning;
using TimTicker.Application.Settings;
using TimTicker.Application.Tasks;
using TimTicker.Application.Timeline;
using TimTicker.App.Services;
using TimTicker.Domain.Tasks;
using TimTicker.Domain.Timeline;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.App.ViewModels;

public sealed partial class TodayViewModel : ViewModelBase
{
    private readonly IDailyPlanService _planService;
    private readonly ITaskService _taskService;
    private readonly ITimelineService _timelineService;
    private readonly IDetailNavigationService _detailNavigationService;
    private readonly IDayBoundarySettings _dayBoundarySettings;
    private readonly DateOnly? _requestedDate;

    public TodayViewModel(
        IDailyPlanService planService,
        ITaskService taskService,
        ITimelineService timelineService,
        IDetailNavigationService detailNavigationService,
        IDayBoundarySettings dayBoundarySettings)
    {
        _planService = planService;
        _taskService = taskService;
        _timelineService = timelineService;
        _detailNavigationService = detailNavigationService;
        _dayBoundarySettings = dayBoundarySettings;
    }

    internal TodayViewModel(
        IDailyPlanService planService,
        ITaskService taskService,
        ITimelineService timelineService,
        IDetailNavigationService detailNavigationService,
        IDayBoundarySettings dayBoundarySettings,
        DateOnly requestedDate)
        : this(planService, taskService, timelineService, detailNavigationService, dayBoundarySettings)
    {
        if (requestedDate == default)
        {
            throw new ArgumentException("The requested date must not be empty.", nameof(requestedDate));
        }

        _requestedDate = requestedDate;
        IsHistoricalEditor = true;
        HeaderTitleText = requestedDate.ToString("yyyy年M月d日", CultureInfo.GetCultureInfo("zh-CN"));
        ClosedStatusText = "当日已结束";
        EndDayActionText = "结束当日";
        ReopenDayActionText = "重新打开当日";
        PlanSectionTitleText = "当日计划";
        EmptyPlanTitleText = "当日还没有计划";
        EmptyPlanDescriptionText = "从任务池多选当日要完成的事情，或在上方新建任务并直接加入。";
        DaySummaryActionText = "查看当日总结";
        TimelineSectionTitleText = "当日时间轴";
    }

    public ObservableCollection<TodayPlanItemViewModel> PlanItems { get; } = [];

    public ObservableCollection<TodayTimelineItemViewModel> TimelineItems { get; } = [];

    [ObservableProperty]
    private string _dateText = string.Empty;

    [ObservableProperty]
    private string _headerTitleText = "今天";

    [ObservableProperty]
    private string _closedStatusText = "今日已结束";

    [ObservableProperty]
    private string _endDayActionText = "结束今天";

    [ObservableProperty]
    private string _reopenDayActionText = "重新打开今天";

    [ObservableProperty]
    private string _planSectionTitleText = "今日计划";

    [ObservableProperty]
    private string _emptyPlanTitleText = "今天还没有计划";

    [ObservableProperty]
    private string _emptyPlanDescriptionText = "从任务池多选今天要完成的事情，或在上方新建任务并直接加入。";

    [ObservableProperty]
    private string _daySummaryActionText = "查看今日总结";

    [ObservableProperty]
    private string _timelineSectionTitleText = "今日时间轴";

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

    [ObservableProperty]
    private bool _isDayClosed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopTimelineCommand))]
    private bool _isDayOpen = true;

    [ObservableProperty]
    private bool _canEndDay = true;

    [ObservableProperty]
    private bool _canStartTimeline = true;

    [ObservableProperty]
    private string _completionPercentageText = string.Empty;

    [ObservableProperty]
    private string _completionCountText = "未制定计划";

    [ObservableProperty]
    private string _completionStateText = "进行中";

    [ObservableProperty]
    private string _reviewSummaryText = "结束今天时可以填写每日复盘。";

    [ObservableProperty]
    private int _completedPlanItemCount;

    [ObservableProperty]
    private int _totalPlanItemCount;

    [ObservableProperty]
    private int _timelineEntryCount;

    public DateOnly CurrentDate { get; private set; }

    public bool IsHistoricalEditor { get; }

    public string? CurrentReviewText { get; private set; }

    public DayEndTime CurrentDayEndTime => _dayBoundarySettings.CurrentDayEndTime;

    public async Task<IReadOnlyList<DailyPlanTaskCandidate>> GetTaskCandidatesAsync() =>
        await _planService.ListCandidatesAsync(CurrentDate);

    public Task<IReadOnlyList<TimelineTaskOption>> GetTimelineTaskOptionsAsync() =>
        _timelineService.ListTaskOptionsAsync(CurrentDate);

    [RelayCommand]
    public async Task LoadTodayAsync()
    {
        DailyPlanSummary plan = _requestedDate.HasValue
            ? await _planService.GetOrCreateForDateAsync(_requestedDate.Value)
            : await _planService.GetTodayAsync();
        CurrentDate = plan.Date;
        DateText = plan.Date.ToDateTime(TimeOnly.MinValue).ToString(
            IsHistoricalEditor ? "dddd" : "M月d日，dddd",
            CultureInfo.GetCultureInfo("zh-CN"));
        ApplyPlan(plan);
        ApplyTimeline(await _timelineService.GetForDateAsync(CurrentDate));
    }

    public async Task AddSelectedTasksAsync(IEnumerable<Guid> taskIds) =>
        ApplyPlan(await _planService.AddTasksAsync(CurrentDate, taskIds));

    public async Task CreateTaskAndAddAsync(CreateTaskRequest request) =>
        ApplyPlan(await _planService.CreateTaskAndAddAsync(CurrentDate, request));

    public async Task StartTimelineEntryAsync(StartTimelineEntryRequest request)
    {
        if (IsHistoricalEditor)
        {
            throw new InvalidOperationException("Live timers can only be started for today.");
        }

        await _timelineService.StartAsync(request);
        TimelineErrorMessage = string.Empty;
        ApplyTimeline(await _timelineService.GetForDateAsync(CurrentDate));
    }

    public async Task CreateTimelineEntryAsync(CreateTimelineEntryRequest request)
    {
        await _timelineService.CreateAsync(request);
        ApplyTimeline(await _timelineService.GetForDateAsync(CurrentDate));
    }

    public async Task UpdateTimelineEntryAsync(UpdateTimelineEntryRequest request)
    {
        await _timelineService.UpdateAsync(request);
        ApplyTimeline(await _timelineService.GetForDateAsync(CurrentDate));
    }

    public async Task DeleteTimelineEntryAsync(Guid id)
    {
        await _timelineService.DeleteAsync(id);
        ApplyTimeline(await _timelineService.GetForDateAsync(CurrentDate));
    }

    public async Task CloseTodayAsync(string? reviewText)
    {
        ApplyPlan(await _planService.CloseAsync(CurrentDate, reviewText));
        ApplyTimeline(await _timelineService.GetForDateAsync(CurrentDate));
    }

    public async Task ReopenTodayAsync()
    {
        ApplyPlan(await _planService.ReopenAsync(CurrentDate));
        ApplyTimeline(await _timelineService.GetForDateAsync(CurrentDate));
    }

    public async Task ShowTaskDetailsAsync(Guid taskId, System.Windows.Window? owner)
    {
        if (await _detailNavigationService.ShowTaskAsync(taskId, owner))
        {
            ApplyPlan(await _planService.GetOrCreateForDateAsync(CurrentDate));
            ApplyTimeline(await _timelineService.GetForDateAsync(CurrentDate));
        }
    }

    public async Task ShowTimelineEntryDetailsAsync(Guid entryId, System.Windows.Window? owner)
    {
        if (await _detailNavigationService.ShowTimelineEntryAsync(entryId, owner))
        {
            ApplyPlan(await _planService.GetOrCreateForDateAsync(CurrentDate));
            ApplyTimeline(await _timelineService.GetForDateAsync(CurrentDate));
        }
    }

    [RelayCommand]
    private async Task CompleteLinkedTaskAsync(TimelineLinkedTaskViewModel? item)
    {
        if (!IsDayOpen || item?.Status != DomainTaskStatus.Pending)
        {
            return;
        }

        await _taskService.CompleteAsync(item.TaskId);
        ApplyPlan(await _planService.GetOrCreateForDateAsync(CurrentDate));
        ApplyTimeline(await _timelineService.GetForDateAsync(CurrentDate));
    }

    [RelayCommand]
    private async Task RemoveLinkedTaskAsync(TimelineLinkedTaskViewModel? item)
    {
        if (!IsDayOpen || item is null)
        {
            return;
        }

        await _timelineService.RemoveTaskAsync(item.TimelineEntryId, item.TaskId);
        ApplyTimeline(await _timelineService.GetForDateAsync(CurrentDate));
    }

    [RelayCommand]
    private async Task CompleteAsync(TodayPlanItemViewModel? item)
    {
        if (IsDayOpen && item?.Status == DomainTaskStatus.Pending)
        {
            await _taskService.CompleteAsync(item.TaskId);
            ApplyPlan(await _planService.GetOrCreateForDateAsync(CurrentDate));
        }
    }

    [RelayCommand]
    private async Task ReopenAsync(TodayPlanItemViewModel? item)
    {
        if (IsDayOpen && item?.Status == DomainTaskStatus.Completed)
        {
            await _taskService.ReopenAsync(item.TaskId);
            ApplyPlan(await _planService.GetOrCreateForDateAsync(CurrentDate));
        }
    }

    [RelayCommand]
    private async Task MoveUpAsync(TodayPlanItemViewModel? item)
    {
        if (IsDayOpen && item?.CanMoveUp == true)
        {
            ApplyPlan(await _planService.MoveTaskAsync(CurrentDate, item.TaskId, PlanMoveDirection.Up));
        }
    }

    [RelayCommand]
    private async Task MoveDownAsync(TodayPlanItemViewModel? item)
    {
        if (IsDayOpen && item?.CanMoveDown == true)
        {
            ApplyPlan(await _planService.MoveTaskAsync(CurrentDate, item.TaskId, PlanMoveDirection.Down));
        }
    }

    [RelayCommand]
    private async Task RemoveFromPlanAsync(TodayPlanItemViewModel? item)
    {
        if (IsDayOpen && item is not null)
        {
            ApplyPlan(await _planService.RemoveTaskAsync(CurrentDate, item.TaskId));
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopTimeline))]
    private async Task StopTimelineAsync()
    {
        try
        {
            await _timelineService.StopCurrentAsync();
            TimelineErrorMessage = string.Empty;
            ApplyTimeline(await _timelineService.GetForDateAsync(CurrentDate));
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
            PlanItems.Add(new TodayPlanItemViewModel(plan.Items[index], index, plan.Items.Count, plan.IsClosed));
        }

        HasPlanItems = PlanItems.Count > 0;
        PlanItemCountText = $"{PlanItems.Count} 项";
        IsDayClosed = plan.IsClosed;
        IsDayOpen = !plan.IsClosed;
        CompletedPlanItemCount = plan.CompletedCount;
        TotalPlanItemCount = plan.TotalCount;
        CompletionPercentageText = plan.CompletionRate.HasValue
            ? $"{Math.Round(plan.CompletionRate.Value * 100, MidpointRounding.AwayFromZero):0}%"
            : string.Empty;
        CompletionCountText = plan.TotalCount == 0
            ? "未制定计划"
            : $"已完成 {plan.CompletedCount} / {plan.TotalCount} 项";
        CompletionStateText = plan.IsClosed ? ClosedStatusText : "实时完成度";
        CurrentReviewText = plan.ReviewText;
        ReviewSummaryText = plan.IsClosed
            ? string.IsNullOrWhiteSpace(plan.ReviewText) ? IsHistoricalEditor ? "当日未填写复盘。" : "今日未填写复盘。" : plan.ReviewText
            : IsHistoricalEditor ? "结束当日时可以填写每日复盘。" : "结束今天时可以填写每日复盘。";
        UpdateDayActions();
    }

    private void ApplyTimeline(TimelineDaySummary timeline)
    {
        TimelineItems.Clear();
        foreach (TimelineEntrySummary entry in timeline.Entries)
        {
            TimelineItems.Add(new TodayTimelineItemViewModel(entry));
        }

        HasTimelineItems = TimelineItems.Count > 0;
        TimelineEntryCount = TimelineItems.Count;
        TodayTimelineItemViewModel? running = TimelineItems.SingleOrDefault(item => item.IsRunning);
        HasRunningTimeline = running is not null;
        RunningTimelineTitle = running?.Title ?? string.Empty;
        TotalRecordedTimeText = $"已记录 {FormatDuration(timeline.TotalDuration)}";
        UpdateDayActions();
    }

    private bool CanStopTimeline() => !IsHistoricalEditor && IsDayOpen && HasRunningTimeline;

    private void UpdateDayActions()
    {
        CanEndDay = IsDayOpen && !HasRunningTimeline;
        CanStartTimeline = !IsHistoricalEditor && CanEndDay;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        int totalMinutes = Math.Max(0, (int)Math.Round(duration.TotalMinutes));
        return totalMinutes >= 60
            ? $"{totalMinutes / 60} 小时 {totalMinutes % 60} 分钟"
            : $"{totalMinutes} 分钟";
    }

}

public sealed class TodayTimelineItemViewModel
{
    public TodayTimelineItemViewModel(TimelineEntrySummary entry)
    {
        Summary = entry;
        Id = entry.Id;
        Title = entry.Title;
        Description = entry.Description;
        StartTimeText = $"{FormatLocalTime(entry.StartLocal, entry.RecordDate)} -";
        EndTimeText = $"- {(entry.EndLocal.HasValue ? FormatLocalTime(entry.EndLocal.Value, entry.RecordDate) : "进行中")}";
        DurationText = entry.IsRunning ? "正在记录" : FormatDuration(entry.DayDuration);
        IsRunning = entry.IsRunning;
        IsTimerEntry = entry.Source == TimelineEntrySource.Timer;
        IsReadOnly = entry.IsReadOnly;
        CanEdit = !entry.IsRunning && !entry.IsReadOnly;
        CanDelete = !entry.IsReadOnly;
        StartLocal = entry.StartLocal;
        EndLocal = entry.EndLocal;
        LinkedTasks = new ObservableCollection<TimelineLinkedTaskViewModel>(
            entry.LinkedTasks.Select(task => new TimelineLinkedTaskViewModel(entry.Id, task, entry.IsReadOnly)));
        HasLinkedTasks = LinkedTasks.Count > 0;
    }

    public TimelineEntrySummary Summary { get; }

    public Guid Id { get; }

    public string Title { get; }

    public string? Description { get; }

    public string StartTimeText { get; }

    public string EndTimeText { get; }

    public string DurationText { get; }

    public bool IsRunning { get; }

    public bool IsTimerEntry { get; }

    public bool IsReadOnly { get; }

    public bool CanEdit { get; }

    public bool CanDelete { get; }

    public DateTime StartLocal { get; }

    public DateTime? EndLocal { get; }

    public ObservableCollection<TimelineLinkedTaskViewModel> LinkedTasks { get; }

    public bool HasLinkedTasks { get; }

    private static string FormatLocalTime(DateTime value, DateOnly recordDate)
    {
        int dayOffset = DateOnly.FromDateTime(value).DayNumber - recordDate.DayNumber;
        return dayOffset switch
        {
            0 => value.ToString("HH:mm", CultureInfo.InvariantCulture),
            1 => $"{value:HH:mm}(+1)",
            _ => value.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture),
        };
    }

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
    public TimelineLinkedTaskViewModel(Guid timelineEntryId, TaskSummary task, bool isReadOnly)
    {
        TimelineEntryId = timelineEntryId;
        TaskId = task.Id;
        Title = task.Title;
        Status = task.Status;
        CanComplete = task.Status == DomainTaskStatus.Pending && !isReadOnly;
        CanRemove = !isReadOnly;
        StatusText = task.Status == DomainTaskStatus.Completed ? "已完成" : "待完成";
        DueDateText = task.DueDate.HasValue ? $"截止 {task.DueDate:yyyy-MM-dd}" : "无截止日期";
    }

    public Guid TimelineEntryId { get; }

    public Guid TaskId { get; }

    public string Title { get; }

    public DomainTaskStatus Status { get; }

    public bool CanComplete { get; }

    public bool CanRemove { get; }

    public string StatusText { get; }

    public string DueDateText { get; }
}

public sealed class TodayPlanItemViewModel
{
    public TodayPlanItemViewModel(DailyPlanItemSummary item, int index, int count, bool isReadOnly)
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
        CanComplete = item.Status == DomainTaskStatus.Pending && !isReadOnly;
        CanReopen = item.Status == DomainTaskStatus.Completed && !isReadOnly;
        CanMoveUp = index > 0 && !isReadOnly;
        CanMoveDown = index < count - 1 && !isReadOnly;
        CanRemove = !isReadOnly;
    }

    public Guid TaskId { get; }

    public string Title { get; }

    public string DueDateText { get; }

    public string PriorityText { get; }

    public DomainTaskStatus Status { get; }

    public string StatusText { get; }

    public bool IsOverdue { get; }

    public bool CanComplete { get; }

    public bool CanReopen { get; }

    public bool CanMoveUp { get; }

    public bool CanMoveDown { get; }

    public bool CanRemove { get; }
}
