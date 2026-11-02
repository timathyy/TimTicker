using CommunityToolkit.Mvvm.ComponentModel;
using TimTicker.Application.Planning;
using TimTicker.Domain.Tasks;

namespace TimTicker.App.ViewModels;

public sealed partial class PlanTaskCandidateViewModel : ObservableObject
{
    public PlanTaskCandidateViewModel(DailyPlanTaskCandidate candidate)
    {
        TaskId = candidate.Task.Id;
        Title = candidate.Task.Title;
        DueDateText = candidate.Task.DueDate.HasValue ? $"截止 {candidate.Task.DueDate:yyyy-MM-dd}" : "无截止日期";
        PriorityText = candidate.Task.Priority switch
        {
            TaskPriority.High => "高优先级",
            TaskPriority.Medium => "中优先级",
            TaskPriority.Low => "低优先级",
            _ => "无优先级",
        };
        IsOverdue = candidate.IsOverdue;
    }

    public Guid TaskId { get; }

    public string Title { get; }

    public string DueDateText { get; }

    public string PriorityText { get; }

    public bool IsOverdue { get; }

    [ObservableProperty]
    private bool _isSelected;
}
