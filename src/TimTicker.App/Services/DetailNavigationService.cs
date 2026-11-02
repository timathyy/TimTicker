using System.Windows;
using TimTicker.Application.Tasks;
using TimTicker.Application.Timeline;
using TimTicker.App.Views;

namespace TimTicker.App.Services;

public sealed class DetailNavigationService(
    ITaskService taskService,
    ITimelineService timelineService) : IDetailNavigationService
{
    public async Task<bool> ShowTaskAsync(Guid taskId, Window? owner)
    {
        TaskSummary task = await taskService.GetAsync(taskId);
        IReadOnlyList<TimelineEntrySummary> entries = await timelineService.ListForTaskAsync(taskId);
        TaskDetailWindow window = new(task, entries) { Owner = owner };
        window.ShowDialog();

        if (window.SelectedTimelineEntryId is Guid entryId)
        {
            return await ShowTimelineEntryAsync(entryId, owner);
        }

        if (!window.EditRequested)
        {
            return false;
        }

        TaskEditorWindow editor = new(task) { Owner = owner };
        if (editor.ShowDialog() == true && editor.UpdateRequest is UpdateTaskRequest request)
        {
            await taskService.UpdateAsync(request);
            return true;
        }

        return false;
    }

    public async Task<bool> ShowTimelineEntryAsync(Guid entryId, Window? owner)
    {
        TimelineEntrySummary entry = await timelineService.GetAsync(entryId);
        TimelineEntryDetailWindow window = new(entry) { Owner = owner };
        window.ShowDialog();

        if (window.SelectedTaskId is Guid taskId)
        {
            return await ShowTaskAsync(taskId, owner);
        }

        if (!window.EditRequested || entry.IsRunning)
        {
            return false;
        }

        IReadOnlyList<TimelineTaskOption> options = await timelineService.ListTaskOptionsAsync();
        TimelineEntryEditorWindow editor = new(options, entry) { Owner = owner };
        if (editor.ShowDialog() == true && editor.UpdateRequest is UpdateTimelineEntryRequest request)
        {
            await timelineService.UpdateAsync(request);
            return true;
        }

        return false;
    }
}
