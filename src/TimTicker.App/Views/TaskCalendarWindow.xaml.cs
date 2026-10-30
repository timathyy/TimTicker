using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TimTicker.App.Views;

public partial class TaskCalendarWindow : Window
{
    private DateTime _displayedMonth;
    private DateTime _selectedDate;

    public TaskCalendarWindow(DateTime initialDate)
    {
        _selectedDate = initialDate.Date;
        _displayedMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
        InitializeComponent();
        RenderMonth();
    }

    public DateTime SelectedDate => _selectedDate;

    private void RenderMonth()
    {
        DisplayedMonthText.Text = _displayedMonth.ToString("yyyy年 M月", CultureInfo.GetCultureInfo("zh-CN"));
        SelectedDateText.Text = $"已选择：{_selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        CalendarDaysGrid.Children.Clear();

        int leadingBlankCount = ((int)_displayedMonth.DayOfWeek + 6) % 7;
        for (int index = 0; index < leadingBlankCount; index++)
        {
            CalendarDaysGrid.Children.Add(new Border());
        }

        int daysInMonth = DateTime.DaysInMonth(_displayedMonth.Year, _displayedMonth.Month);
        for (int day = 1; day <= daysInMonth; day++)
        {
            DateTime date = new(_displayedMonth.Year, _displayedMonth.Month, day);
            Button button = new()
            {
                Content = day,
                Tag = date,
                Style = (Style)FindResource("CalendarDayButtonStyle"),
            };
            button.Click += DayButton_Click;

            if (date == DateTime.Today)
            {
                button.SetResourceReference(BorderBrushProperty, "AccentBrush");
            }

            if (date == _selectedDate)
            {
                button.SetResourceReference(BackgroundProperty, "AccentBrush");
                button.SetResourceReference(ForegroundProperty, "AccentForegroundBrush");
                button.SetResourceReference(BorderBrushProperty, "AccentBrush");
                button.IsHitTestVisible = false;
            }

            CalendarDaysGrid.Children.Add(button);
        }

        while (CalendarDaysGrid.Children.Count < 42)
        {
            CalendarDaysGrid.Children.Add(new Border());
        }
    }

    private void DayButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DateTime date })
        {
            _selectedDate = date;
            RenderMonth();
        }
    }

    private void PreviousMonthButton_Click(object sender, RoutedEventArgs e) => MoveDisplayedMonth(-1);

    private void NextMonthButton_Click(object sender, RoutedEventArgs e) => MoveDisplayedMonth(1);

    private void PreviousYearButton_Click(object sender, RoutedEventArgs e) => MoveDisplayedYear(-1);

    private void NextYearButton_Click(object sender, RoutedEventArgs e) => MoveDisplayedYear(1);

    private void MoveDisplayedMonth(int monthOffset)
    {
        if ((monthOffset < 0 && _displayedMonth == DateTime.MinValue.Date) ||
            (monthOffset > 0 && _displayedMonth.Year == 9999 && _displayedMonth.Month == 12))
        {
            return;
        }

        _displayedMonth = _displayedMonth.AddMonths(monthOffset);
        RenderMonth();
    }

    private void MoveDisplayedYear(int yearOffset)
    {
        int targetYear = _displayedMonth.Year + yearOffset;
        if (targetYear is < 1 or > 9999)
        {
            return;
        }

        _displayedMonth = new DateTime(targetYear, _displayedMonth.Month, 1);
        RenderMonth();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

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
    }
}
