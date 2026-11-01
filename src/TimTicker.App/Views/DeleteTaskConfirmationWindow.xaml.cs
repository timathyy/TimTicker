using System.Windows;
using System.Windows.Input;

namespace TimTicker.App.Views;

public partial class DeleteTaskConfirmationWindow : Window
{
    public DeleteTaskConfirmationWindow(string taskTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskTitle);
        InitializeComponent();
        TaskTitleText.Text = $"“{taskTitle}”将被永久删除，此操作无法撤销。";
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
