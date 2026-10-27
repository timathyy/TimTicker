using System.Globalization;

namespace TimeTicker.App.ViewModels;

public sealed class TodayViewModel : ViewModelBase
{
    public string DateText { get; } = DateTime.Today.ToString("M月d日，dddd", CultureInfo.GetCultureInfo("zh-CN"));
}
