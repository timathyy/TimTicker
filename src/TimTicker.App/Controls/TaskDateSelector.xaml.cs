using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TimTicker.App.Views;

namespace TimTicker.App.Controls;

public partial class TaskDateSelector : UserControl
{
    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate),
        typeof(DateTime?),
        typeof(TaskDateSelector),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateChanged));

    public TaskDateSelector()
    {
        InitializeComponent();
        UpdateDisplayText();
    }

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value?.Date);
    }

    private static void OnSelectedDateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        ((TaskDateSelector)dependencyObject).UpdateDisplayText();
    }

    private void UpdateDisplayText()
    {
        if (DisplayTextBox is not null)
        {
            DisplayTextBox.Text = SelectedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "未选择日期";
        }
    }

    private void SelectDateButton_Click(object sender, RoutedEventArgs e)
    {
        TaskCalendarWindow window = new(SelectedDate ?? DateTime.Today)
        {
            Owner = Window.GetWindow(this),
        };

        if (window.ShowDialog() == true)
        {
            SelectedDate = window.SelectedDate;
        }
    }
}
