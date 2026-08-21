using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Infrastructure.Data;

namespace TemplateBuilder.Infrastructure.Repositories;

public class TemplatePromotionRepository : ITemplatePromotionRepository
{
    private readonly TemplateBuilderDbContext _context;

    public TemplatePromotionRepository(TemplateBuilderDbContext context) => _context = context;

    public async Task<Template?> GetByExternalKeyAsync(Guid externalKey, CancellationToken ct = default) =>
        await _context.Templates.FirstOrDefaultAsync(t => t.ExternalKey == externalKey, ct);

    public async Task<Template> AddWithVersionsAsync(Template template, IReadOnlyList<TemplateVersion> versions, CancellationToken ct = default)
    {
        template.CreatedAt = template.UpdatedAt = DateTime.UtcNow;
        foreach (var version in versions)
        {
            if (version.CreatedAt == default)
                version.CreatedAt = DateTime.UtcNow;
            template.Versions.Add(version);
        }

        _context.Templates.Add(template);
        await _context.SaveChangesAsync(ct);

        if (versions.Count > 0)
        {
            template.CurrentVersionId = versions[^1].Id;
            await _context.SaveChangesAsync(ct);
        }

        return template;
    }

    public async Task<IReadOnlyList<int>> UpdateFromImportAsync(Template template, IReadOnlyList<TemplateVersion> versions, CancellationToken ct = default)
    {
        var next = await GetMaxVersionNumberAsync(template.Id, ct) + 1;
        var assigned = new List<int>();

        foreach (var version in versions)
        {
            version.TemplateId = template.Id;
            version.VersionNumber = next;
            if (version.CreatedAt == default)
                version.CreatedAt = DateTime.UtcNow;
            _context.TemplateVersions.Add(version);
            assigned.Add(next);
            next++;
        }

        template.UpdatedAt = DateTime.UtcNow;
        _context.Templates.Update(template);
        await _context.SaveChangesAsync(ct);

        return assigned;
    }

    public async Task<int> GetMaxVersionNumberAsync(int templateId, CancellationToken ct = default)
    {
        var max = await _context.TemplateVersions
            .Where(v => v.TemplateId == templateId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(ct);
        return max ?? 0;
    }
}
