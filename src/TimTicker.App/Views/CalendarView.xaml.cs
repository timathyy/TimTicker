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

    private async void EditSelectedDayButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not CalendarViewModel viewModel || viewModel.SelectedDate == default)
        {
            return;
        }

        System.Windows.Window? owner = System.Windows.Window.GetWindow(this);
        if (viewModel.IsSelectedDateToday)
        {
            if (owner?.DataContext is MainWindowViewModel mainWindowViewModel)
            {
                mainWindowViewModel.NavigateTo(AppPage.Today);
            }

            return;
        }

        await viewModel.EditSelectedDayAsync(owner);
    }
}
