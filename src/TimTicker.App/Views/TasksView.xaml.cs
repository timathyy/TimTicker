using System.Windows.Controls;
using System.Windows.Input;
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

    private void TasksView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            e.Handled = true;
        }
    }
}
