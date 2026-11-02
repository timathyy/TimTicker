using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TimTicker.Application.Tasks;
using TimTicker.Application.Timeline;
using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.App.Views;

public partial class TaskDetailWindow : Window
{
    public TaskDetailWindow(TaskSummary task, IReadOnlyList<TimelineEntrySummary> timelineEntries)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(timelineEntries);
        InitializeComponent();
        DataContext = new TaskDetailViewModel(task, timelineEntries);
    }

    public bool EditRequested { get; private set; }

    public Guid? SelectedTimelineEntryId { get; private set; }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        EditRequested = true;
        Close();
    }

    private void TimelineCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TaskTimelineCardViewModel card })
        {
            SelectedTimelineEntryId = card.Id;
            Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}

public sealed class TaskDetailViewModel
{
    public TaskDetailViewModel(TaskSummary task, IReadOnlyList<TimelineEntrySummary> entries)
    {
        Title = task.Title;
        DescriptionText = string.IsNullOrWhiteSpace(task.Description) ? "暂无说明" : task.Description;
        StatusText = task.Status == DomainTaskStatus.Completed ? "已完成" : "待完成";
        PriorityText = task.Priority switch
        {
            TaskPriority.High => "高优先级",
            TaskPriority.Medium => "中优先级",
            TaskPriority.Low => "低优先级",
            _ => "无优先级",
        };
        CreatedDateText = task.CreatedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        DueDateText = task.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "未设置";
        CompletedDateText = task.CompletedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "未完成";
        TimelineEntries = new ObservableCollection<TaskTimelineCardViewModel>(entries.Select(entry => new TaskTimelineCardViewModel(entry)));
        HasTimelineEntries = TimelineEntries.Count > 0;
    }

    public string Title { get; }
    public string DescriptionText { get; }
    public string StatusText { get; }
    public string PriorityText { get; }
    public string CreatedDateText { get; }
    public string DueDateText { get; }
    public string CompletedDateText { get; }
    public ObservableCollection<TaskTimelineCardViewModel> TimelineEntries { get; }
    public bool HasTimelineEntries { get; }
}

public sealed class TaskTimelineCardViewModel
{
    public TaskTimelineCardViewModel(TimelineEntrySummary entry)
    {
        Id = entry.Id;
        Title = entry.Title;
        string end = entry.EndLocal.HasValue ? FormatTime(entry.EndLocal.Value, entry.RecordDate) : "进行中";
        TimeText = $"{entry.RecordDate:yyyy-MM-dd} · {FormatTime(entry.StartLocal, entry.RecordDate)} — {end}";
    }

    public Guid Id { get; }
    public string Title { get; }
    public string TimeText { get; }

    private static string FormatTime(DateTime value, DateOnly recordDate) =>
        DateOnly.FromDateTime(value).DayNumber - recordDate.DayNumber == 1
            ? $"{value:HH:mm}(+1)"
            : value.ToString("HH:mm", CultureInfo.InvariantCulture);
}
