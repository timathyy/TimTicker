using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimTicker.Application.Tasks;
using TimTicker.Application.Templates;
using TimTicker.App.Services;
using TimTicker.Domain.Tasks;
using DomainTaskStatus = TimTicker.Domain.Tasks.TaskStatus;

namespace TimTicker.App.ViewModels;

public sealed class TaskFolderFilter(
    string name,
    Guid? folderId,
    bool rootOnly,
    TaskFolderKind? kind = null)
{
    public string Name { get; } = name;

    public Guid? FolderId { get; } = folderId;

    public bool RootOnly { get; } = rootOnly;

    public TaskFolderKind? Kind { get; } = kind;
}

public sealed class TaskFolderNode : ObservableObject
{
    private readonly TaskFolderSummary _folder;
    private bool _isExpanded;
    private bool _isSelected;

    public TaskFolderNode(TaskFolderSummary folder, TaskTemplateSummary? taskTemplate)
    {
        _folder = folder;
        Template = taskTemplate;
    }

    public Guid Id => _folder.Id;

    public Guid? ParentId => _folder.ParentId;

    public string Name => _folder.Name;

    public TaskFolderKind Kind => _folder.Kind;

    public bool IsTemplateFolder => Kind == TaskFolderKind.Template;

    public string IconGlyph => IsTemplateFolder ? "\uE8F1" : "\uE8B7";

    public TaskTemplateSummary? Template { get; }

    public ObservableCollection<TaskFolderNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed record TaskFolderOption(string Name, Guid? FolderId);

public sealed partial class TasksViewModel(
    ITaskService taskService,
    ITaskFolderService folderService,
    ITaskTemplateService templateService,
    IDetailNavigationService detailNavigationService) : ViewModelBase
{
    private static readonly TaskFolderFilter AllTasksFilter = new("全部任务", null, false);
    private static readonly TaskFolderFilter RootFolderFilter = new("根目录", null, true);
    private IReadOnlyList<TaskFolderSummary> _folderSummaries = [];
    private Dictionary<Guid, TaskTemplateSummary> _templatesByFolder = [];
    private Dictionary<Guid, TaskFolderNode> _folderNodes = [];
    private bool _suppressSelectionLoad;
    private bool _synchronizingTreeSelection;
    private int _taskLoadVersion;

    public ObservableCollection<TaskSummary> Tasks { get; } = [];

    public ObservableCollection<TaskFolderNode> RootFolders { get; } = [];

    public IReadOnlyList<TaskFolderOption> FolderOptions
    {
        get
        {
            IReadOnlyDictionary<Guid, TaskFolderSummary> foldersById = _folderSummaries.ToDictionary(folder => folder.Id);
            return
            [
                new("根目录", null),
                .. _folderSummaries
                    .Select(folder => new TaskFolderOption(BuildFolderPath(folder, foldersById), folder.Id))
                    .OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase),
            ];
        }
    }

    [ObservableProperty]
    private bool _hasTasks;

    [ObservableProperty]
    private DomainTaskStatus? _selectedStatus;

    [ObservableProperty]
    private TaskSortOrder _selectedSortOrder = TaskSortOrder.CreatedAtDescending;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private TaskFolderFilter _selectedFolder = RootFolderFilter;

    [ObservableProperty]
    private TaskTemplateSummary? _selectedTemplate;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool IsTemplateFolderSelected => SelectedTemplate is not null;

    public bool CanNavigateUp => SelectedFolder.FolderId.HasValue;

    public string CurrentLocationText => SelectedFolder.Name;

    public string EmptyStateTitle => SelectedFolder.FolderId.HasValue || SelectedFolder.RootOnly
        ? $"“{SelectedFolder.Name}”中还没有任务"
        : SelectedStatus switch
        {
            null => "还没有任何任务",
            DomainTaskStatus.Pending => "任务池还是空的",
            DomainTaskStatus.Completed => "还没有已完成任务",
            _ => "没有任务",
        };

    public string EmptyStateDescription => SelectedFolder.FolderId.HasValue || SelectedFolder.RootOnly
        ? "在当前目录新建任务，或编辑现有任务并移动到这里。"
        : SelectedStatus switch
        {
            null => "创建第一项任务，开始整理接下来要做的事情。",
            DomainTaskStatus.Pending => "随时记下接下来要做的事，不必依赖记忆。",
            DomainTaskStatus.Completed => "完成的任务会保留在这里，方便回顾。",
            _ => string.Empty,
        };

    [RelayCommand]
    public async Task LoadAsync()
    {
        Guid? selectedFolderId = SelectedFolder.FolderId;
        bool selectedRootOnly = SelectedFolder.RootOnly;
        await templateService.GenerateDueAsync();
        _folderSummaries = await folderService.ListAsync();
        IReadOnlyList<TaskTemplateSummary> templates = await templateService.ListAsync();
        _templatesByFolder = templates.ToDictionary(taskTemplate => taskTemplate.FolderId);

        RebuildFolderTree();
        _suppressSelectionLoad = true;
        try
        {
            if (selectedFolderId is Guid folderId)
            {
                SelectFolder(folderId);
            }
            else if (selectedRootOnly)
            {
                SelectRoot();
            }
            else
            {
                SelectAllTasks();
            }
        }
        finally
        {
            _suppressSelectionLoad = false;
        }

        OnPropertyChanged(nameof(FolderOptions));
        await LoadTasksAsync();
    }

    [RelayCommand]
    public async Task LoadTasksAsync()
    {
        int loadVersion = Interlocked.Increment(ref _taskLoadVersion);
        IReadOnlyList<TaskSummary> tasks = await taskService.ListAsync(new TaskQuery(
            SelectedStatus,
            SearchText,
            SelectedSortOrder,
            SelectedFolder.FolderId,
            SelectedFolder.RootOnly));
        if (loadVersion != _taskLoadVersion)
        {
            return;
        }

        Tasks.Clear();
        foreach (TaskSummary task in tasks)
        {
            Tasks.Add(task);
        }

        HasTasks = Tasks.Count > 0;
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
    }

    public void SelectAllTasks()
    {
        ClearNodeSelection();
        ApplyFolderSelection(AllTasksFilter);
    }

    public void SelectRoot()
    {
        ClearNodeSelection();
        ApplyFolderSelection(RootFolderFilter);
    }

    public void SelectFolder(TaskFolderNode folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        folder.IsExpanded = true;
        if (_synchronizingTreeSelection || SelectedFolder.FolderId == folder.Id)
        {
            return;
        }

        SelectFolder(folder.Id);
    }

    public void NavigateUp()
    {
        if (SelectedFolder.FolderId is not Guid selectedId)
        {
            return;
        }

        TaskFolderSummary? selected = _folderSummaries.SingleOrDefault(folder => folder.Id == selectedId);
        if (selected?.ParentId is Guid parentId)
        {
            SelectFolder(parentId);
        }
        else
        {
            SelectRoot();
        }
    }

    public async Task CreateFolderAsync(string name)
    {
        TaskFolderSummary folder = await folderService.CreateAsync(name, CurrentDirectoryId());
        StatusMessage = $"已在当前目录创建文件夹“{folder.Name}”。";
        await LoadAsync();
        SelectFolder(folder.Id);
    }

    public IReadOnlyList<TaskFolderOption> GetMoveTargets(TaskFolderNode folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        HashSet<Guid> excludedFolderIds = [folder.Id];
        Queue<Guid> pendingFolderIds = new([folder.Id]);
        while (pendingFolderIds.TryDequeue(out Guid currentId))
        {
            foreach (TaskFolderSummary child in _folderSummaries.Where(item => item.ParentId == currentId))
            {
                if (excludedFolderIds.Add(child.Id))
                {
                    pendingFolderIds.Enqueue(child.Id);
                }
            }
        }

        IReadOnlyDictionary<Guid, TaskFolderSummary> foldersById = _folderSummaries.ToDictionary(item => item.Id);
        return
        [
            new("根目录", null),
            .. _folderSummaries
                .Where(item => !excludedFolderIds.Contains(item.Id))
                .Select(item => new TaskFolderOption(BuildFolderPath(item, foldersById), item.Id))
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    public async Task RenameFolderAsync(TaskFolderNode folder, string name)
    {
        ArgumentNullException.ThrowIfNull(folder);
        await folderService.RenameAsync(folder.Id, name);
        StatusMessage = $"已将文件夹重命名为“{name.Trim()}”。";
        await LoadAsync();
        SelectFolder(folder.Id);
    }

    public async Task MoveFolderAsync(TaskFolderNode folder, Guid? parentFolderId)
    {
        ArgumentNullException.ThrowIfNull(folder);
        await folderService.MoveAsync(folder.Id, parentFolderId);
        StatusMessage = $"已移动文件夹“{folder.Name}”。";
        await LoadAsync();
        SelectFolder(folder.Id);
    }

    public async Task DeleteFolderAsync(TaskFolderNode folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        Guid? parentFolderId = folder.ParentId;
        await folderService.DeleteAsync(folder.Id);
        StatusMessage = $"已删除文件夹“{folder.Name}”，其中的任务已移到根目录。";
        await LoadAsync();
        if (parentFolderId is Guid parentId)
        {
            SelectFolder(parentId);
        }
        else
        {
            SelectRoot();
        }
    }

    public async Task CreateTemplateAsync(CreateTaskTemplateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        TaskTemplateSummary taskTemplate = await templateService.CreateAsync(
            request with { ParentFolderId = CurrentDirectoryId() });
        StatusMessage = $"已创建自动任务文件夹“{taskTemplate.Name}”。";
        await LoadAsync();
        SelectFolder(taskTemplate.FolderId);
    }

    public async Task GenerateSelectedTemplateAsync()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        TemplateGenerationResult result = await templateService.GenerateManualAsync(SelectedTemplate.Id);
        StatusMessage = $"已在“{SelectedTemplate.Name}”中生成 {result.GeneratedCount} 项任务。";
        await LoadTasksAsync();
    }

    public async Task CreateTaskAsync(CreateTaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        await taskService.CreateAsync(request);
        StatusMessage = "任务已创建。";
        await LoadTasksAsync();
    }

    public async Task UpdateTaskAsync(UpdateTaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        await taskService.UpdateAsync(request);
        StatusMessage = "任务已保存。";
        await LoadTasksAsync();
    }

    public async Task ShowTaskDetailsAsync(Guid taskId, System.Windows.Window? owner)
    {
        if (await detailNavigationService.ShowTaskAsync(taskId, owner))
        {
            await LoadTasksAsync();
        }
    }

    [RelayCommand]
    private async Task CompleteAsync(TaskSummary? task)
    {
        if (task is not null)
        {
            await taskService.CompleteAsync(task.Id);
            await LoadTasksAsync();
        }
    }

    [RelayCommand]
    private async Task ReopenAsync(TaskSummary? task)
    {
        if (task is not null)
        {
            await taskService.ReopenAsync(task.Id);
            await LoadTasksAsync();
        }
    }

    public async Task DeleteTaskAsync(TaskSummary task)
    {
        ArgumentNullException.ThrowIfNull(task);
        await taskService.DeleteAsync(task.Id);
        await LoadTasksAsync();
    }

    partial void OnSelectedFolderChanged(TaskFolderFilter value)
    {
        SelectedTemplate = value.FolderId is Guid folderId && _templatesByFolder.TryGetValue(folderId, out TaskTemplateSummary? taskTemplate)
            ? taskTemplate
            : null;
        OnPropertyChanged(nameof(IsTemplateFolderSelected));
        OnPropertyChanged(nameof(CanNavigateUp));
        OnPropertyChanged(nameof(CurrentLocationText));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
        if (!_suppressSelectionLoad)
        {
            _ = LoadTasksAsync();
        }
    }

    partial void OnSelectedStatusChanged(DomainTaskStatus? value) => _ = LoadTasksAsync();

    partial void OnSelectedSortOrderChanged(TaskSortOrder value) => _ = LoadTasksAsync();

    partial void OnSearchTextChanged(string value) => _ = LoadTasksAsync();

    private Guid? CurrentDirectoryId() => SelectedFolder.FolderId;

    private void ApplyFolderSelection(TaskFolderFilter filter)
    {
        if (ReferenceEquals(SelectedFolder, filter))
        {
            OnSelectedFolderChanged(filter);
            return;
        }

        SelectedFolder = filter;
    }

    private void SelectFolder(Guid folderId)
    {
        TaskFolderSummary? folder = _folderSummaries.SingleOrDefault(item => item.Id == folderId);
        if (folder is null)
        {
            SelectRoot();
            return;
        }

        IReadOnlyDictionary<Guid, TaskFolderSummary> foldersById = _folderSummaries.ToDictionary(item => item.Id);
        MarkNodeSelected(folder);
        ApplyFolderSelection(new TaskFolderFilter(
            BuildFolderPath(folder, foldersById),
            folder.Id,
            false,
            folder.Kind));
    }

    private void RebuildFolderTree()
    {
        _folderNodes = _folderSummaries.ToDictionary(
            folder => folder.Id,
            folder => new TaskFolderNode(
                folder,
                _templatesByFolder.TryGetValue(folder.Id, out TaskTemplateSummary? taskTemplate) ? taskTemplate : null));

        RootFolders.Clear();
        foreach (TaskFolderSummary folder in _folderSummaries.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            TaskFolderNode node = _folderNodes[folder.Id];
            if (folder.ParentId is Guid parentId && _folderNodes.TryGetValue(parentId, out TaskFolderNode? parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                RootFolders.Add(node);
            }
        }
    }

    private void ClearNodeSelection()
    {
        foreach (TaskFolderNode node in _folderNodes.Values)
        {
            node.IsSelected = false;
        }
    }

    private void MarkNodeSelected(TaskFolderSummary folder)
    {
        _synchronizingTreeSelection = true;
        try
        {
            ClearNodeSelection();
            if (!_folderNodes.TryGetValue(folder.Id, out TaskFolderNode? node))
            {
                return;
            }

            node.IsSelected = true;
            Guid? parentId = folder.ParentId;
            while (parentId is Guid id && _folderNodes.TryGetValue(id, out TaskFolderNode? parent))
            {
                parent.IsExpanded = true;
                parentId = parent.ParentId;
            }
        }
        finally
        {
            _synchronizingTreeSelection = false;
        }
    }

    private static string BuildFolderPath(
        TaskFolderSummary folder,
        IReadOnlyDictionary<Guid, TaskFolderSummary> foldersById)
    {
        List<string> segments = [folder.Name];
        HashSet<Guid> visited = [folder.Id];
        Guid? parentId = folder.ParentId;
        while (parentId is Guid id && foldersById.TryGetValue(id, out TaskFolderSummary? parent) && visited.Add(id))
        {
            segments.Add(parent.Name);
            parentId = parent.ParentId;
        }

        segments.Reverse();
        return string.Join(" / ", segments);
    }
}
