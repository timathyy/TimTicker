using System.Windows;

namespace TimTicker.App.Services;

public interface IDetailNavigationService
{
    Task<bool> ShowTaskAsync(Guid taskId, Window? owner);

    Task<bool> ShowTimelineEntryAsync(Guid entryId, Window? owner);
}
