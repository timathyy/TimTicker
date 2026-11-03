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
    }
}
