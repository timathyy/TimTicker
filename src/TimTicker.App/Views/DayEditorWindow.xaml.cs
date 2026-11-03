using System.Globalization;
using System.Windows;
using TimTicker.App.ViewModels;

namespace TimTicker.App.Views;

public partial class DayEditorWindow : Window
{
    public DayEditorWindow(DateOnly date, TodayViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        InitializeComponent();
        Title = $"{date.ToDateTime(TimeOnly.MinValue).ToString("yyyy年M月d日", CultureInfo.GetCultureInfo("zh-CN"))} - TimTicker";
        DataContext = viewModel;
        StateChanged += OnWindowStateChanged;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        SystemCommands.MinimizeWindow(this);
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == System.Windows.WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        SystemCommands.CloseWindow(this);
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        bool isMaximized = WindowState == System.Windows.WindowState.Maximized;
        WindowRoot.Margin = isMaximized
            ? SystemParameters.WindowResizeBorderThickness
            : new Thickness(0);

        MaximizeIcon.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreIcon.Visibility = isMaximized ? Visibility.Visible : Visibility.Collapsed;
    }
}
