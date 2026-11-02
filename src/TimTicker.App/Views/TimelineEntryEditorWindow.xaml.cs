using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using TimTicker.Application.Timeline;
using TimTicker.App.ViewModels;

namespace TimTicker.App.Views;

public partial class TimelineEntryEditorWindow : Window
{
    private readonly TodayTimelineItemViewModel? _entry;
    private readonly bool _startMode;

    public TimelineEntryEditorWindow(
        IReadOnlyList<TimelineTaskOption> taskOptions,
        TodayTimelineItemViewModel? entry = null,
        bool startMode = false)
    {
        ArgumentNullException.ThrowIfNull(taskOptions);
        _entry = entry;
        _startMode = startMode;
        if (entry is not null && startMode)
        {
            throw new ArgumentException("An existing entry cannot be opened in start mode.", nameof(startMode));
        }
        TaskChoices = new ObservableCollection<TimelineTaskChoice>(
            new[] { new TimelineTaskChoice(null, "不关联任务") }
                .Concat(taskOptions.Select(option => new TimelineTaskChoice(option.Id, option.Title))));
        InitializeComponent();
        DataContext = this;
        InitializeFields();
        Loaded += (_, _) => TitleTextBox.Focus();
    }

    public ObservableCollection<TimelineTaskChoice> TaskChoices { get; }

    public CreateTimelineEntryRequest? CreateRequest { get; private set; }

    public UpdateTimelineEntryRequest? UpdateRequest { get; private set; }

    public StartTimelineEntryRequest? StartRequest { get; private set; }

    private void InitializeFields()
    {
        bool isEditing = _entry is not null;
        WindowTitleText.Text = _startMode ? "开始记录" : isEditing ? "编辑时间记录" : "手动录入";
        SaveButton.Content = _startMode ? "开始记录" : isEditing ? "保存修改" : "添加记录";
        TimeFieldsPanel.Visibility = _startMode ? Visibility.Collapsed : Visibility.Visible;
        Height = _startMode ? 520 : 690;
        DateTime end = _entry?.EndLocal ?? DateTime.Now;
        DateTime start = _entry?.StartLocal ?? end.AddHours(-1);
        TitleTextBox.Text = _entry?.Title ?? string.Empty;
        DescriptionTextBox.Text = _entry?.Description ?? string.Empty;
        CategoryTextBox.Text = _entry?.Category ?? string.Empty;
        StartDateSelector.SelectedDate = start.Date;
        StartTimeTextBox.Text = start.ToString("HH:mm", CultureInfo.InvariantCulture);
        EndDateSelector.SelectedDate = end.Date;
        EndTimeTextBox.Text = end.ToString("HH:mm", CultureInfo.InvariantCulture);
        TaskComboBox.SelectedIndex = Math.Max(0, TaskChoices.ToList().FindIndex(choice => choice.Id == _entry?.TaskItemId));
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string title = TitleTextBox.Text.Trim();
        if (title.Length == 0)
        {
            ShowValidationError("请输入记录标题。", TitleTextBox);
            return;
        }

        Guid? taskItemId = (TaskComboBox.SelectedItem as TimelineTaskChoice)?.Id;
        if (_startMode)
        {
            StartRequest = new StartTimelineEntryRequest(
                title,
                DescriptionTextBox.Text,
                CategoryTextBox.Text,
                taskItemId);
            DialogResult = true;
            return;
        }

        if (StartDateSelector.SelectedDate is not DateTime startDate ||
            !TryParseTime(StartTimeTextBox.Text, out TimeOnly startTime))
        {
            ShowValidationError("请输入有效的开始日期和时间，例如 09:30。", StartTimeTextBox);
            return;
        }

        if (EndDateSelector.SelectedDate is not DateTime endDate ||
            !TryParseTime(EndTimeTextBox.Text, out TimeOnly endTime))
        {
            ShowValidationError("请输入有效的结束日期和时间，例如 10:30。", EndTimeTextBox);
            return;
        }

        DateTime startLocal = startDate.Date + startTime.ToTimeSpan();
        DateTime endLocal = endDate.Date + endTime.ToTimeSpan();
        if (endLocal <= startLocal)
        {
            ShowValidationError("结束时间必须晚于开始时间；跨午夜记录请选择次日结束日期。", EndTimeTextBox);
            return;
        }

        if (_entry is null)
        {
            CreateRequest = new CreateTimelineEntryRequest(
                title,
                DescriptionTextBox.Text,
                CategoryTextBox.Text,
                taskItemId,
                startLocal,
                endLocal);
        }
        else
        {
            UpdateRequest = new UpdateTimelineEntryRequest(
                _entry.Id,
                title,
                DescriptionTextBox.Text,
                CategoryTextBox.Text,
                taskItemId,
                startLocal,
                endLocal);
        }

        DialogResult = true;
    }

    private static bool TryParseTime(string text, out TimeOnly time) =>
        TimeOnly.TryParseExact(
            text.Trim(),
            ["H:mm", "HH:mm"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);

    private void ShowValidationError(string message, UIElement control)
    {
        ValidationMessage.Text = message;
        control.Focus();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

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

public sealed record TimelineTaskChoice(Guid? Id, string Title);
