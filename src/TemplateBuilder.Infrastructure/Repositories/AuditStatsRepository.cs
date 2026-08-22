using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Infrastructure.Data;

namespace TemplateBuilder.Infrastructure.Repositories;

public class AuditStatsRepository : IAuditStatsRepository
{
    private readonly TemplateBuilderDbContext _context;

    public AuditStatsRepository(TemplateBuilderDbContext context) => _context = context;

    public async Task<AuditStats> GetStatsAsync(AuditQuery query, CancellationToken ct = default)
    {
        var filtered = AuditFiltering.Apply(_context.AuditLogs.AsNoTracking(), query);

        // EF Core's DbContext does not support concurrent operations, so these queries run
        // sequentially (each awaited before the next starts) rather than via Task.WhenAll.
        var total = await filtered.CountAsync(ct);
        var templateCount = await filtered.CountAsync(a => a.EntityType == "Template", ct);
        var snippetCount = await filtered.CountAsync(a => a.EntityType == "Snippet", ct);
        var uniqueActors = await filtered.Select(a => a.Actor).Distinct().CountAsync(ct);
        var first = await filtered.OrderBy(a => a.OccurredAt).Select(a => (DateTime?)a.OccurredAt).FirstOrDefaultAsync(ct);
        var last = await filtered.OrderByDescending(a => a.OccurredAt).Select(a => (DateTime?)a.OccurredAt).FirstOrDefaultAsync(ct);

        var (start, end) = ResolveWindow(query, last);

        var buckets = await filtered
            .Where(a => a.OccurredAt >= start && a.OccurredAt < end.AddDays(1))
            .GroupBy(a => a.OccurredAt.Date)
            .Select(g => new AuditDailyBucket { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byDate = buckets.ToDictionary(b => b.Date);
        var filled = new List<AuditDailyBucket>();
        for (var d = start; d <= end; d = d.AddDays(1))
            filled.Add(byDate.TryGetValue(d, out var b) ? b : new AuditDailyBucket { Date = d, Count = 0 });

        return new AuditStats
        {
            Total = total,
            TemplateCount = templateCount,
            SnippetCount = snippetCount,
            UniqueActors = uniqueActors,
            FirstOccurrence = first,
            LastOccurrence = last,
            DailyBuckets = filled
        };
    }

    private static (DateTime Start, DateTime End) ResolveWindow(AuditQuery query, DateTime? last)
    {
        var end = query.To?.Date ?? last?.Date ?? DateTime.UtcNow.Date;
        var start = query.From?.Date ?? end.AddDays(-29);

        return start > end ? (end, end) : (start, end);
    }
}
