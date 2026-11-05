using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;

namespace TimTicker.Application.Tests;

public sealed class TaskFolderServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FoldersCanBeNestedToArbitraryDepth()
    {
        InMemoryFolderRepository repository = new();
        TaskFolderService service = new(repository, new FixedTimeProvider(Now));

        TaskFolderSummary root = await service.CreateAsync("项目");
        TaskFolderSummary child = await service.CreateAsync("客户端", root.Id);
        TaskFolderSummary grandchild = await service.CreateAsync("资源", child.Id);

        Assert.Null(root.ParentId);
        Assert.Equal(root.Id, child.ParentId);
        Assert.Equal(child.Id, grandchild.ParentId);
    }

    [Fact]
    public async Task NamesAreUniqueAmongSiblingsButReusableElsewhere()
    {
        InMemoryFolderRepository repository = new();
        TaskFolderService service = new(repository, new FixedTimeProvider(Now));
        TaskFolderSummary firstParent = await service.CreateAsync("甲");
        TaskFolderSummary secondParent = await service.CreateAsync("乙");
        await service.CreateAsync("资料", firstParent.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync("资料", firstParent.Id));
        TaskFolderSummary allowed = await service.CreateAsync("资料", secondParent.Id);

        Assert.Equal(secondParent.Id, allowed.ParentId);
    }

    [Fact]
    public async Task MissingParentIsRejected()
    {
        TaskFolderService service = new(new InMemoryFolderRepository(), new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync("孤立目录", Guid.NewGuid()));
    }

    [Fact]
    public async Task FolderCanBeRenamedAndMoved()
    {
        InMemoryFolderRepository repository = new();
        TaskFolderService service = new(repository, new FixedTimeProvider(Now));
        TaskFolderSummary firstParent = await service.CreateAsync("甲");
        TaskFolderSummary secondParent = await service.CreateAsync("乙");
        TaskFolderSummary folder = await service.CreateAsync("旧名称", firstParent.Id);

        TaskFolderSummary renamed = await service.RenameAsync(folder.Id, "新名称");
        TaskFolderSummary moved = await service.MoveAsync(folder.Id, secondParent.Id);

        Assert.Equal("新名称", renamed.Name);
        Assert.Equal(secondParent.Id, moved.ParentId);
    }

    [Fact]
    public async Task FolderCannotBeMovedIntoItsDescendant()
    {
        InMemoryFolderRepository repository = new();
        TaskFolderService service = new(repository, new FixedTimeProvider(Now));
        TaskFolderSummary parent = await service.CreateAsync("父目录");
        TaskFolderSummary child = await service.CreateAsync("子目录", parent.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.MoveAsync(parent.Id, child.Id));
    }

    [Fact]
    public async Task FolderWithChildrenCannotBeDeleted()
    {
        InMemoryFolderRepository repository = new();
        TaskFolderService service = new(repository, new FixedTimeProvider(Now));
        TaskFolderSummary parent = await service.CreateAsync("父目录");
        await service.CreateAsync("子目录", parent.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(parent.Id));
    }

    [Fact]
    public async Task EmptyUserFolderCanBeDeleted()
    {
        InMemoryFolderRepository repository = new();
        TaskFolderService service = new(repository, new FixedTimeProvider(Now));
        TaskFolderSummary folder = await service.CreateAsync("待删除");

        await service.DeleteAsync(folder.Id);

        Assert.DoesNotContain(await service.ListAsync(), item => item.Id == folder.Id);
    }

    private sealed class InMemoryFolderRepository : ITaskFolderRepository
    {
        private readonly List<TaskFolder> _folders = [];

        public Task AddAsync(TaskFolder folder, CancellationToken cancellationToken = default)
        {
            _folders.Add(folder);
            return Task.CompletedTask;
        }

        public Task SaveAsync(
            TaskFolder folder,
            bool moveTasksToRoot = false,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TaskFolder?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_folders.SingleOrDefault(folder => folder.Id == id && !folder.IsDeleted));

        public Task<IReadOnlyList<TaskFolder>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TaskFolder>>(_folders.Where(folder => !folder.IsDeleted).ToArray());

        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_folders.Any(folder => folder.Id == id && !folder.IsDeleted));

        public Task<bool> NameExistsAsync(
            Guid? parentId,
            string name,
            Guid? excludedFolderId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_folders.Any(folder =>
                !folder.IsDeleted &&
                folder.ParentId == parentId &&
                folder.Id != excludedFolderId &&
                string.Equals(folder.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_folders.Any(folder => !folder.IsDeleted && folder.ParentId == id));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
