using System.Windows;
using System.Windows.Input;

namespace TimTicker.App.Views;

public partial class EndDayReviewWindow : Window
{
    public EndDayReviewWindow(
        int completedCount,
        int totalCount,
        int timelineEntryCount,
        string totalRecordedTimeText,
        string? existingReview,
        bool readOnly = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(completedCount);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        ArgumentOutOfRangeException.ThrowIfNegative(timelineEntryCount);
        ArgumentException.ThrowIfNullOrWhiteSpace(totalRecordedTimeText);
        InitializeComponent();

        CompletionText.Text = totalCount == 0
            ? "0/0"
            : $"{Math.Round(completedCount / (decimal)totalCount * 100, MidpointRounding.AwayFromZero):0}%";
        PlanSummaryText.Text = totalCount == 0 ? "今天没有计划任务" : $"已完成 {completedCount} / {totalCount} 项";
        TimelineCountText.Text = $"{timelineEntryCount} 条";
        TimelineDurationText.Text = totalRecordedTimeText;
        ReviewTextBox.Text = existingReview ?? string.Empty;
        if (readOnly)
        {
            WindowTitleText.Text = "今日总结";
            IntroText.Text = "今天已结束，以下内容为结束时保存的计划、时间轴与复盘摘要。";
            ReviewTextBox.IsReadOnly = true;
            CancelButton.Visibility = Visibility.Collapsed;
            ConfirmButton.Content = "关闭";
        }
        else
        {
            Loaded += (_, _) => ReviewTextBox.Focus();
        }
    }

    public string? ReviewText => string.IsNullOrWhiteSpace(ReviewTextBox.Text) ? null : ReviewTextBox.Text.Trim();

    private void ConfirmButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

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
    }
}
