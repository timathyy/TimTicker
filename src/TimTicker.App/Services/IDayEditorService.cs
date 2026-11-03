using System.Windows;

namespace TimTicker.App.Services;

public interface IDayEditorService
{
    void Show(DateOnly selectedDate, Window? owner);
}
