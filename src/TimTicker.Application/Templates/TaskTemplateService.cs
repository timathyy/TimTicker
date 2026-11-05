using TimTicker.Application.Settings;
using TimTicker.Application.Tasks;
using TimTicker.Domain.Tasks;
using TimTicker.Domain.Templates;

namespace TimTicker.Application.Templates;

public sealed class TaskTemplateService(
    ITaskTemplateRepository repository,
    IDayBoundarySettings dayBoundarySettings,
    TimeProvider timeProvider) : ITaskTemplateService
{
    private const int MaximumCatchUpCountPerTemplate = 10_000;

    public async Task<TaskTemplateSummary> CreateAsync(
        CreateTaskTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string normalizedName = request.Name.Trim();
        if (request.ParentFolderId is Guid parentFolderId &&
            !await repository.FolderExistsAsync(parentFolderId, cancellationToken).ConfigureAwait(false))
        {
            throw new KeyNotFoundException("The parent task folder was not found.");
        }

        if (await repository.NameExistsAsync(request.ParentFolderId, normalizedName, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("A folder with this name already exists in the current folder.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        TaskFolder folder = new(
            Guid.NewGuid(),
            normalizedName,
            TaskFolderKind.Template,
            now,
            request.ParentFolderId);
        TaskTemplate template = new(
            Guid.NewGuid(),
            folder.Id,
            normalizedName,
            request.TitlePattern,
            request.Description,
            request.Priority,
            request.Schedule,
            request.RepeatInterval,
            request.StartDate,
            request.IsEnabled,
            now);
        await repository.AddAsync(template, folder, cancellationToken).ConfigureAwait(false);
        await GenerateDueForTemplateAsync(template, LocalToday(), cancellationToken).ConfigureAwait(false);
        return ToSummary(template, folder.Name);
    }

    public async Task<IReadOnlyList<TaskTemplateSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TaskTemplate> templates = await repository.ListAsync(cancellationToken).ConfigureAwait(false);
        return templates.Select(template => ToSummary(template, template.Name)).ToArray();
    }

    public async Task<TemplateGenerationResult> GenerateManualAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        TaskTemplate template = await GetAsync(templateId, cancellationToken).ConfigureAwait(false);
        TaskItem task = template.GenerateManual(LocalToday(), timeProvider.GetUtcNow());
        await repository.SaveGenerationAsync(template, [task], cancellationToken).ConfigureAwait(false);
        return new TemplateGenerationResult(1, [task.Id]);
    }

    public async Task<TemplateGenerationResult> GenerateDueAsync(CancellationToken cancellationToken = default)
    {
        DateOnly today = LocalToday();
        List<Guid> taskIds = [];
        foreach (TaskTemplate template in await repository.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!template.IsEnabled || template.Schedule == TaskTemplateSchedule.Manual)
            {
                continue;
            }

            IReadOnlyList<TaskItem> generated = await GenerateDueForTemplateAsync(template, today, cancellationToken).ConfigureAwait(false);
            taskIds.AddRange(generated.Select(task => task.Id));
        }

        return new TemplateGenerationResult(taskIds.Count, taskIds);
    }

    private async Task<IReadOnlyList<TaskItem>> GenerateDueForTemplateAsync(
        TaskTemplate template,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (!template.IsEnabled || template.Schedule == TaskTemplateSchedule.Manual)
        {
            return [];
        }

        List<TaskItem> generated = [];
        while (template.GetNextOccurrenceDate() <= today)
        {
            if (generated.Count >= MaximumCatchUpCountPerTemplate)
            {
                throw new InvalidOperationException("The template has too many missed occurrences to generate safely.");
            }

            generated.Add(template.GenerateScheduled(template.GetNextOccurrenceDate(), timeProvider.GetUtcNow()));
        }

        if (generated.Count > 0)
        {
            await repository.SaveGenerationAsync(template, generated, cancellationToken).ConfigureAwait(false);
        }

        return generated;
    }

    private async Task<TaskTemplate> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("The template identifier must not be empty.", nameof(id));
        }

        return await repository.FindAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("The requested task template was not found.");
    }

    private DateOnly LocalToday() => DayBoundary.GetBusinessDate(
        timeProvider.GetLocalNow().DateTime,
        dayBoundarySettings.CurrentDayEndTime);

    private static TaskTemplateSummary ToSummary(TaskTemplate template, string folderName) => new(
        template.Id,
        template.FolderId,
        template.Name,
        folderName,
        template.TitlePattern,
        template.Description,
        template.Priority,
        template.Schedule,
        template.RepeatInterval,
        template.StartDate,
        template.IsEnabled,
        template.NextSequenceNumber,
        template.LastGeneratedDate);
}
