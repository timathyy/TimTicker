using TimTicker.Domain.Tasks;
using TimTicker.Domain.Templates;

namespace TimTicker.Application.Templates;

public interface ITaskTemplateRepository
{
    Task AddAsync(TaskTemplate taskTemplate, TaskFolder folder, CancellationToken cancellationToken = default);

    Task<TaskTemplate?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskTemplate>> ListAsync(CancellationToken cancellationToken = default);

    Task SaveGenerationAsync(TaskTemplate taskTemplate, IReadOnlyCollection<TaskItem> tasks, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(Guid? parentFolderId, string name, CancellationToken cancellationToken = default);

    Task<bool> FolderExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
