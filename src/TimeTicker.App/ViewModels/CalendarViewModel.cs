using System.Globalization;

namespace TimeTicker.App.ViewModels;

public sealed class CalendarViewModel : ViewModelBase
{
    public string MonthText { get; } = DateTime.Today.ToString("yyyy年 M月", CultureInfo.GetCultureInfo("zh-CN"));
}
