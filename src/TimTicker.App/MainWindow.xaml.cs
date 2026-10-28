using System.Text;
using System.Windows;
using TimTicker.App.ViewModels;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TimTicker.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
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
        MaximizeIcon.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreIcon.Visibility = isMaximized ? Visibility.Visible : Visibility.Collapsed;
    }
}
