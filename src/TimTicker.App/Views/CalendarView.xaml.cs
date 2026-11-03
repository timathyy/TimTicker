using System.Windows.Controls;
using TimTicker.App.ViewModels;

namespace TimTicker.App.Views;

public partial class CalendarView : UserControl
{
    public CalendarView()
    {
        InitializeComponent();
    }

    private void CalendarView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is CalendarViewModel viewModel)
        {
            viewModel.LoadCalendarCommand.Execute(null);
        }
    }
}
