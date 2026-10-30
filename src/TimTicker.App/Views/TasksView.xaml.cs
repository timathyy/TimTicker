using System.Windows.Controls;
using System.Windows.Input;
using TimTicker.Application.Tasks;
using TimTicker.App.ViewModels;

namespace TimTicker.App.Views;

public partial class TasksView : UserControl
{
    public TasksView()
    {
        InitializeComponent();
    }

    private void TasksView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is TasksViewModel viewModel)
        {
            viewModel.LoadTasksCommand.Execute(null);
        }
    }

    private async void NewTaskButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        await ShowCreateTaskWindowAsync();
    }

    private async void TasksView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        {
            await ShowCreateTaskWindowAsync();
            e.Handled = true;
        }
    }

    private async Task ShowCreateTaskWindowAsync()
    {
        if (DataContext is not TasksViewModel viewModel)
        {
            return;
        }

        TaskEditorWindow window = new()
        {
            Owner = System.Windows.Window.GetWindow(this),
        };

        if (window.ShowDialog() == true && window.Request is CreateTaskRequest request)
        {
            await viewModel.CreateTaskAsync(request);
        }
    }
}
