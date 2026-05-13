using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Infrastructure.Data;

namespace TemplateBuilder.Infrastructure.Repositories;

public class TemplateRepository : ITemplateRepository
{
    private readonly AppDbContext _context;

    public TemplateRepository(AppDbContext context) => _context = context;

    public async Task<Template?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _context.Templates
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<Template?> GetByNameAsync(string name, CancellationToken ct = default) =>
        await _context.Templates
            .Include(t => t.CurrentVersion)
            .FirstOrDefaultAsync(t => t.Name == name, ct);

    public async Task<int?> GetCurrentVersionIdAsync(int templateId, CancellationToken ct = default) =>
        await _context.Templates
            .Where(t => t.Id == templateId)
            .Select(t => t.CurrentVersionId)
            .FirstOrDefaultAsync(ct);

    public async Task<string?> GetVersionBodyAsync(int versionId, CancellationToken ct = default) =>
        await _context.TemplateVersions
            .Where(v => v.Id == versionId)
            .Select(v => v.Body)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Template>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Templates
            .Where(t => t.IsActive)
            .Include(t => t.CurrentVersion)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TemplateVersion>> GetVersionHistoryAsync(int templateId, CancellationToken ct = default) =>
        await _context.TemplateVersions
            .Where(v => v.TemplateId == templateId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);

    public async Task<int> GetNextVersionNumberAsync(int templateId, CancellationToken ct = default)
    {
        var max = await _context.TemplateVersions
            .Where(v => v.TemplateId == templateId)
            .MaxAsync(v => (int?)v.VersionNumber, ct);
        return (max ?? 0) + 1;
    }

    public async Task<Template> CreateAsync(Template template, CancellationToken ct = default)
    {
        template.CreatedAt = template.UpdatedAt = DateTime.UtcNow;
        _context.Templates.Add(template);
        await _context.SaveChangesAsync(ct);
        return template;
    }

    public async Task UpdateTemplateAsync(Template template, CancellationToken ct = default)
    {
        template.UpdatedAt = DateTime.UtcNow;
        _context.Templates.Update(template);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<TemplateVersion> PublishVersionAsync(int templateId, TemplateVersion version, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);
        version.CreatedAt = DateTime.UtcNow;
        _context.TemplateVersions.Add(version);
        await _context.SaveChangesAsync(ct);

        var template = await _context.Templates.FindAsync(new object[] { templateId }, ct);
        template!.CurrentVersionId = version.Id;
        template.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
        return version;
    }
}
