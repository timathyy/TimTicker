using System.Windows.Controls;
using System.Windows.Input;
using TimTicker.Application.Tasks;
using TimTicker.App.ViewModels;
using TimTicker.Application.Templates;

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
            viewModel.LoadCommand.Execute(null);
        }
    }

    private async void NewTaskButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        await ShowCreateTaskWindowAsync();
    }

    private async void NewFolderButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not TasksViewModel viewModel)
        {
            return;
        }

        TaskFolderEditorWindow window = new() { Owner = System.Windows.Window.GetWindow(this) };
        if (window.ShowDialog() == true && window.FolderName is string name)
        {
            try
            {
                await viewModel.CreateFolderAsync(name);
            }
            catch (InvalidOperationException)
            {
                System.Windows.MessageBox.Show("当前目录中已经存在同名文件夹。", "无法创建文件夹", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
    }

    private async void NewTemplateButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not TasksViewModel viewModel)
        {
            return;
        }

        TaskTemplateEditorWindow window = new() { Owner = System.Windows.Window.GetWindow(this) };
        if (window.ShowDialog() == true && window.Request is CreateTaskTemplateRequest request)
        {
            try
            {
                await viewModel.CreateTemplateAsync(request);
            }
            catch (InvalidOperationException)
            {
                System.Windows.MessageBox.Show("当前目录中已经存在同名文件夹。", "无法创建自动任务文件夹", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
    }

    private async void GenerateSelectedTemplateButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is TasksViewModel viewModel)
        {
            await viewModel.GenerateSelectedTemplateAsync();
        }
    }

    private void AllTasksButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is TasksViewModel viewModel)
        {
            viewModel.SelectAllTasks();
        }
    }

    private void RootFolderButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is TasksViewModel viewModel)
        {
            viewModel.SelectRoot();
        }
    }

    private void NavigateUpButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is TasksViewModel viewModel)
        {
            viewModel.NavigateUp();
        }
    }

    private void FolderTree_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TaskFolderNode folder && DataContext is TasksViewModel viewModel)
        {
            viewModel.SelectFolder(folder);
        }
    }

    private async void EditTaskButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TaskSummary task })
        {
            await ShowEditTaskWindowAsync(task);
        }
    }

    private async void TaskDetailButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TaskSummary task } && DataContext is TasksViewModel viewModel)
        {
            await viewModel.ShowTaskDetailsAsync(task.Id, System.Windows.Window.GetWindow(this));
        }
    }

    private async void DeleteTaskButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TaskSummary task } || DataContext is not TasksViewModel viewModel)
        {
            return;
        }

        DeleteTaskConfirmationWindow window = new(task.Title)
        {
            Owner = System.Windows.Window.GetWindow(this),
        };

        if (window.ShowDialog() == true)
        {
            await viewModel.DeleteTaskAsync(task);
        }
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

        TaskEditorWindow window = new(
            folderOptions: viewModel.FolderOptions,
            defaultFolderId: viewModel.SelectedFolder.FolderId)
        {
            Owner = System.Windows.Window.GetWindow(this),
        };

        if (window.ShowDialog() == true && window.CreateRequest is CreateTaskRequest request)
        {
            await viewModel.CreateTaskAsync(request);
        }
    }

    private async Task ShowEditTaskWindowAsync(TaskSummary task)
    {
        if (DataContext is not TasksViewModel viewModel)
        {
            return;
        }

        TaskEditorWindow window = new(task, folderOptions: viewModel.FolderOptions)
        {
            Owner = System.Windows.Window.GetWindow(this),
        };

        if (window.ShowDialog() == true && window.UpdateRequest is UpdateTaskRequest request)
        {
            await viewModel.UpdateTaskAsync(request);
        }
    }
}
