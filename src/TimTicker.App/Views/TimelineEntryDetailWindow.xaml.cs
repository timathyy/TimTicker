using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TimTicker.Application.Tasks;
using TimTicker.Application.Timeline;
using TimTicker.Domain.Timeline;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.App.Views;

public partial class TimelineEntryDetailWindow : Window
{
    public TimelineEntryDetailWindow(TimelineEntrySummary entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        InitializeComponent();
        DataContext = new TimelineEntryDetailViewModel(entry);
    }

    public bool EditRequested { get; private set; }

    public Guid? SelectedTaskId { get; private set; }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        EditRequested = true;
        Close();
    }

    private void TaskCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TimelineTaskCardViewModel card })
        {
            SelectedTaskId = card.Id;
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

public sealed class TimelineEntryDetailViewModel
{
    public TimelineEntryDetailViewModel(TimelineEntrySummary entry)
    {
        Title = entry.Title;
        DescriptionText = string.IsNullOrWhiteSpace(entry.Description) ? "暂无说明" : entry.Description;
        string end = entry.EndLocal.HasValue ? FormatTime(entry.EndLocal.Value, entry.RecordDate) : "进行中";
        TimeRangeText = $"{entry.RecordDate:yyyy-MM-dd} · {FormatTime(entry.StartLocal, entry.RecordDate)} — {end}";
        DurationText = FormatDuration(entry.DayDuration);
        IsTimerEntry = entry.Source == TimelineEntrySource.Timer;
        CanEdit = !entry.IsRunning && !entry.IsReadOnly;
        LinkedTasks = new ObservableCollection<TimelineTaskCardViewModel>(entry.LinkedTasks.Select(task => new TimelineTaskCardViewModel(task)));
        HasLinkedTasks = LinkedTasks.Count > 0;
    }

    public string Title { get; }
    public string DescriptionText { get; }
    public string TimeRangeText { get; }
    public string DurationText { get; }
    public bool IsTimerEntry { get; }
    public bool CanEdit { get; }
    public ObservableCollection<TimelineTaskCardViewModel> LinkedTasks { get; }
    public bool HasLinkedTasks { get; }

    private static string FormatDuration(TimeSpan duration)
    {
        int minutes = Math.Max(0, (int)Math.Round(duration.TotalMinutes));
        return minutes >= 60 ? $"{minutes / 60} 小时 {minutes % 60} 分钟" : $"{minutes} 分钟";
    }

    private static string FormatTime(DateTime value, DateOnly recordDate) =>
        DateOnly.FromDateTime(value).DayNumber - recordDate.DayNumber == 1
            ? $"{value:HH:mm}(+1)"
            : value.ToString("HH:mm", CultureInfo.InvariantCulture);
}

public sealed class TimelineTaskCardViewModel
{
    public TimelineTaskCardViewModel(TaskSummary task)
    {
        Id = task.Id;
        Title = task.Title;
        string status = task.Status == DomainTaskStatus.Completed ? "已完成" : "待完成";
        MetaText = task.DueDate.HasValue ? $"{status} · 截止 {task.DueDate:yyyy-MM-dd}" : $"{status} · 无截止日期";
    }

    public Guid Id { get; }
    public string Title { get; }
    public string MetaText { get; }
}
