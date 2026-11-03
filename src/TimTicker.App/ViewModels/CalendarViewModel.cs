using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimTicker.Application.Calendar;
using TimTicker.Application.Settings;
using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.App.ViewModels;

public sealed partial class CalendarViewModel(
    ICalendarService calendarService,
    IDayBoundarySettings dayBoundarySettings,
    TimeProvider timeProvider) : ViewModelBase
{
    private static readonly CultureInfo ChineseCulture = CultureInfo.GetCultureInfo("zh-CN");
    private DateOnly _monthStart;
    private int _loadVersion;

    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];

    public ObservableCollection<CalendarPlanItemViewModel> PlanItems { get; } = [];

    public ObservableCollection<CalendarTimelineItemViewModel> TimelineItems { get; } = [];

    [ObservableProperty]
    private string _monthText = string.Empty;

    [ObservableProperty]
    private string _selectedDateText = string.Empty;

    [ObservableProperty]
    private string _selectedStateText = string.Empty;

    [ObservableProperty]
    private string _selectedStateDescription = string.Empty;

    [ObservableProperty]
    private string _completionPercentageText = string.Empty;

    [ObservableProperty]
    private string _completionCountText = "未制定计划";

    [ObservableProperty]
    private string _timelineSummaryText = "无时间记录";

    [ObservableProperty]
    private string _reviewText = string.Empty;

    [ObservableProperty]
    private bool _hasPlan;

    [ObservableProperty]
    private bool _hasPlanItems;

    [ObservableProperty]
    private bool _hasTimelineItems;

    [ObservableProperty]
    private bool _hasSelectedData;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public DateOnly SelectedDate { get; private set; }

    [RelayCommand]
    private async Task LoadCalendarAsync()
    {
        DateOnly today = GetBusinessToday();
        DateOnly selectedDate = SelectedDate == default ? today : SelectedDate;
        DateOnly monthStart = _monthStart == default ? new DateOnly(today.Year, today.Month, 1) : _monthStart;
        await LoadMonthAsync(monthStart, selectedDate);
    }

    [RelayCommand]
    private async Task PreviousMonthAsync() => await ChangeMonthAsync(-1);

    [RelayCommand]
    private async Task NextMonthAsync() => await ChangeMonthAsync(1);

    [RelayCommand]
    private async Task GoToTodayAsync()
    {
        DateOnly today = GetBusinessToday();
        await LoadMonthAsync(new DateOnly(today.Year, today.Month, 1), today);
    }

    [RelayCommand]
    private async Task SelectDayAsync(CalendarDayViewModel? day)
    {
        if (day is null || IsLoading)
        {
            return;
        }

        SelectDayCell(day.Date);
        await LoadDayAsync(day.Date, _loadVersion);
    }

    private async Task ChangeMonthAsync(int monthOffset)
    {
        if (IsLoading)
        {
            return;
        }

        DateOnly today = GetBusinessToday();
        DateOnly currentMonth = _monthStart == default
            ? new DateOnly(today.Year, today.Month, 1)
            : _monthStart;
        DateOnly targetMonth = currentMonth.AddMonths(monthOffset);
        int preferredDay = SelectedDate == default ? 1 : SelectedDate.Day;
        DateOnly targetDate = new(
            targetMonth.Year,
            targetMonth.Month,
            Math.Min(preferredDay, DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month)));
        await LoadMonthAsync(targetMonth, targetDate);
    }

    private async Task LoadMonthAsync(DateOnly monthStart, DateOnly selectedDate)
    {
        int loadVersion = Interlocked.Increment(ref _loadVersion);
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        try
        {
            CalendarMonthSummary month = await calendarService.GetMonthAsync(monthStart.Year, monthStart.Month);
            if (loadVersion != _loadVersion)
            {
                return;
            }

            _monthStart = month.MonthStart;
            MonthText = month.MonthStart.ToDateTime(TimeOnly.MinValue).ToString("yyyy年 M月", ChineseCulture);
            DateOnly today = GetBusinessToday();
            Days.Clear();
            foreach (CalendarDaySummary day in month.Days)
            {
                Days.Add(new CalendarDayViewModel(day, month.MonthStart.Month, today));
            }

            DateOnly effectiveSelectedDate = selectedDate >= month.VisibleStart && selectedDate <= month.VisibleEnd
                ? selectedDate
                : month.MonthStart;
            SelectDayCell(effectiveSelectedDate);
            await LoadDayAsync(effectiveSelectedDate, loadVersion);
        }
        catch (ArgumentOutOfRangeException)
        {
            ShowLoadError("无法打开该月份，请选择有效日期。");
        }
        catch (InvalidOperationException)
        {
            ShowLoadError("日历数据暂时不可用，请稍后重试。");
        }
        finally
        {
            if (loadVersion == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    private async Task LoadDayAsync(DateOnly date, int loadVersion)
    {
        try
        {
            CalendarDayDetails details = await calendarService.GetDayAsync(date);
            if (loadVersion != _loadVersion || date != SelectedDate)
            {
                return;
            }

            ApplyDay(details);
        }
        catch (InvalidOperationException)
        {
            if (loadVersion == _loadVersion)
            {
                ShowLoadError("当日详情暂时不可用，请稍后重试。");
            }
        }
    }

    private void SelectDayCell(DateOnly date)
    {
        SelectedDate = date;
        foreach (CalendarDayViewModel day in Days)
        {
            day.IsSelected = day.Date == date;
        }
    }

    private void ApplyDay(CalendarDayDetails details)
    {
        SelectedDateText = details.Date.ToDateTime(TimeOnly.MinValue).ToString("M月d日，dddd", ChineseCulture);
        HasSelectedData = details.HasData;
        HasPlan = details.Plan is not null;

        PlanItems.Clear();
        if (details.Plan is not null)
        {
            foreach (Application.Planning.DailyPlanItemSummary item in details.Plan.Items)
            {
                PlanItems.Add(new CalendarPlanItemViewModel(item));
            }
        }

        HasPlanItems = PlanItems.Count > 0;
        CompletionPercentageText = details.Plan?.CompletionRate is decimal rate
            ? $"{Math.Round(rate * 100, MidpointRounding.AwayFromZero):0}%"
            : string.Empty;
        CompletionCountText = details.Plan is null || details.Plan.TotalCount == 0
            ? "未制定计划"
            : $"已完成 {details.Plan.CompletedCount} / {details.Plan.TotalCount} 项";

        TimelineItems.Clear();
        foreach (Application.Timeline.TimelineEntrySummary entry in details.Timeline.Entries)
        {
            TimelineItems.Add(new CalendarTimelineItemViewModel(entry));
        }

        HasTimelineItems = TimelineItems.Count > 0;
        TimelineSummaryText = HasTimelineItems
            ? $"{TimelineItems.Count} 条记录 · 共 {FormatDuration(details.Timeline.TotalDuration)}"
            : "无时间记录";
        ReviewText = details.Plan is null
            ? "当天没有每日计划与复盘。"
            : details.Plan.IsClosed
                ? string.IsNullOrWhiteSpace(details.Plan.ReviewText) ? "当天未填写复盘。" : details.Plan.ReviewText
                : "当天尚未结束，复盘将在结束后显示。";

        SelectedStateText = details.Plan?.IsClosed == true
            ? "已结束"
            : details.HasData ? "进行中 / 未结束" : "无数据";
        SelectedStateDescription = details.Plan?.IsClosed == true
            ? "计划完成状态与完成度已保存为历史快照。"
            : details.HasData
                ? "当前显示实时状态，之后的修改会同步更新。"
                : "这一天还没有计划、时间记录或复盘。";
    }

    private void ShowLoadError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }

    private DateOnly GetBusinessToday() => DayBoundary.GetBusinessDate(
        timeProvider.GetLocalNow().DateTime,
        dayBoundarySettings.CurrentDayEndTime);

    private static string FormatDuration(TimeSpan duration)
    {
        int totalMinutes = Math.Max(0, (int)Math.Round(duration.TotalMinutes));
        return totalMinutes >= 60
            ? $"{totalMinutes / 60} 小时 {totalMinutes % 60} 分钟"
            : $"{totalMinutes} 分钟";
    }
}

public sealed partial class CalendarDayViewModel : ObservableObject
{
    public CalendarDayViewModel(CalendarDaySummary day, int displayedMonth, DateOnly today)
    {
        Date = day.Date;
        DayNumberText = day.Date.Day.ToString(CultureInfo.InvariantCulture);
        IsOutsideMonth = day.Date.Month != displayedMonth;
        IsToday = day.Date == today;
        HasData = day.HasData;
        HasPlan = day.HasPlan;
        IsClosed = day.IsClosed;
        HasTimelineEntries = day.HasTimelineEntries;
        CompletionText = day.HasPlan
            ? day.TotalCount == 0
                ? "未制定计划"
                : $"{Math.Round((day.CompletionRate ?? 0) * 100, MidpointRounding.AwayFromZero):0}% · {day.CompletedCount}/{day.TotalCount}"
            : string.Empty;
        AutomationText = $"{day.Date:yyyy年M月d日}，{(day.HasData ? IsClosed ? "已结束" : "有数据" : "无数据")}";
    }

    public DateOnly Date { get; }

    public string DayNumberText { get; }

    public bool IsOutsideMonth { get; }

    public bool IsToday { get; }

    public bool HasData { get; }

    public bool HasPlan { get; }

    public bool IsClosed { get; }

    public bool HasTimelineEntries { get; }

    public string CompletionText { get; }

    public string AutomationText { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed class CalendarPlanItemViewModel
{
    public CalendarPlanItemViewModel(Application.Planning.DailyPlanItemSummary item)
    {
        Title = item.Title;
        IsCompleted = item.Status == DomainTaskStatus.Completed;
        StatusText = IsCompleted ? "已完成" : "待完成";
        DetailText = item.DueDate.HasValue ? $"截止 {item.DueDate:yyyy-MM-dd}" : "无截止日期";
        PriorityText = item.Priority switch
        {
            TaskPriority.High => "高",
            TaskPriority.Medium => "中",
            TaskPriority.Low => "低",
            _ => "无",
        };
        MetadataText = $"{DetailText} · 优先级 {PriorityText}";
    }

    public string Title { get; }

    public bool IsCompleted { get; }

    public string StatusText { get; }

    public string DetailText { get; }

    public string PriorityText { get; }

    public string MetadataText { get; }
}

public sealed class CalendarTimelineItemViewModel
{
    public CalendarTimelineItemViewModel(Application.Timeline.TimelineEntrySummary entry)
    {
        Title = entry.Title;
        TimeText = $"{FormatLocalTime(entry.StartLocal, entry.RecordDate)} – " +
                   (entry.EndLocal.HasValue ? FormatLocalTime(entry.EndLocal.Value, entry.RecordDate) : "进行中");
        DurationText = entry.IsRunning ? "正在记录" : FormatDuration(entry.DayDuration);
        LinkedTasksText = entry.LinkedTasks.Count == 0
            ? "未关联任务"
            : string.Join("、", entry.LinkedTasks.Select(task => task.Title));
    }

    public string Title { get; }

    public string TimeText { get; }

    public string DurationText { get; }

    public string LinkedTasksText { get; }

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
