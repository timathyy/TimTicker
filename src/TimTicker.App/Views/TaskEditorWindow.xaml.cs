using System.Windows;
using System.Windows.Input;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;

namespace TimTicker.App.Views;

public partial class TaskEditorWindow : Window
{
    public TaskEditorWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => TitleTextBox.Focus();
    }

    public CreateTaskRequest? Request { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string title = TitleTextBox.Text.Trim();
        if (title.Length == 0)
        {
            ValidationMessage.Text = "请输入任务标题。";
            TitleTextBox.Focus();
            return;
        }

        DateOnly? dueDate = DueDatePicker.SelectedDate is DateTime selectedDate
            ? DateOnly.FromDateTime(selectedDate)
            : null;
        TaskPriority priority = PriorityComboBox.SelectedValue is TaskPriority selectedPriority
            ? selectedPriority
            : TaskPriority.None;

        Request = new CreateTaskRequest(title, DescriptionTextBox.Text, dueDate, priority);
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
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SaveButton_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }
}
