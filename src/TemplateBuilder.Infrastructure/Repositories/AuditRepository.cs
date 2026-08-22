using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Infrastructure.Data;

namespace TemplateBuilder.Infrastructure.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly TemplateBuilderDbContext _context;

    public AuditRepository(TemplateBuilderDbContext context) => _context = context;

    public async Task AddAsync(AuditLog entry, CancellationToken ct = default)
    {
        _context.AuditLogs.Add(entry);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<DateTime?> GetLastOccurrenceAsync(string entityType, int entityId, string action, CancellationToken ct = default) =>
        await _context.AuditLogs
            .Where(a => a.EntityType == entityType && a.EntityId == entityId && a.Action == action)
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => (DateTime?)a.OccurredAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AuditLog>> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        var filtered = AuditFiltering.Apply(_context.AuditLogs.AsNoTracking(), query);
        return await filtered
            .OrderByDescending(a => a.OccurredAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(AuditQuery query, CancellationToken ct = default)
    {
        var filtered = AuditFiltering.Apply(_context.AuditLogs.AsNoTracking(), query);
        return await filtered.CountAsync(ct);
    }
}
