using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Application.Services;

public class AuditService : IAuditService
{
    private readonly IAuditRepository _repository;
    public AuditService(IAuditRepository repository) => _repository = repository;

    public async Task RecordAsync(string entityType, int entityId, string action, string actor,
        string? beforeState = null, string? afterState = null, string? comment = null,
        CancellationToken ct = default)
        => await _repository.AddAsync(new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Actor = actor,
            BeforeState = beforeState,
            AfterState = afterState,
            Comment = comment,
            OccurredAt = DateTime.UtcNow
        }, ct);
}
