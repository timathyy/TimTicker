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
        WindowRoot.Margin = isMaximized
            ? SystemParameters.WindowResizeBorderThickness
            : new Thickness(0);

        MaximizeIcon.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreIcon.Visibility = isMaximized ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source &&
            (FindVisualAncestor<TextBox>(source) is not null || FindVisualAncestor<ComboBox>(source) is not null))
        {
            return;
        }

        Keyboard.ClearFocus();
        FocusManager.SetFocusedElement(FocusManager.GetFocusScope(this), null);
    }

    private static T? FindVisualAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
