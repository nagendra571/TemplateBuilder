using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Infrastructure.Data;

namespace TemplateBuilder.Infrastructure.Repositories;

public class SnippetRepository : ISnippetRepository
{
    private readonly TemplateBuilderDbContext _context;

    public SnippetRepository(TemplateBuilderDbContext context) => _context = context;

    public async Task<IReadOnlyList<Snippet>> GetAllAsync(CancellationToken ct = default) =>
        await _context.Snippets
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

    public async Task<Snippet?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _context.Snippets
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<Snippet> CreateAsync(Snippet snippet, CancellationToken ct = default)
    {
        snippet.CreatedAt = DateTime.UtcNow;
        snippet.UpdatedAt = DateTime.UtcNow;
        _context.Snippets.Add(snippet);
        await _context.SaveChangesAsync(ct);
        return snippet;
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var snippet = await _context.Snippets.FindAsync([id], ct);
        if (snippet is null) return;
        _context.Snippets.Remove(snippet);
        await _context.SaveChangesAsync(ct);
    }
}
