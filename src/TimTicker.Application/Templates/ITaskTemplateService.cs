namespace TimTicker.Application.Templates;

public interface ITaskTemplateService
{
    Task<TaskTemplateSummary> CreateAsync(CreateTaskTemplateRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskTemplateSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<TemplateGenerationResult> GenerateManualAsync(Guid templateId, CancellationToken cancellationToken = default);

    Task<TemplateGenerationResult> GenerateDueAsync(CancellationToken cancellationToken = default);
}
