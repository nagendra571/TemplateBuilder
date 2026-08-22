using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Infrastructure.Data;
using TemplateBuilder.Infrastructure.Repositories;

namespace TemplateBuilder.Infrastructure.Tests.Repositories;

public class AuditStatsRepositoryTests
{
    private static TemplateBuilderDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TemplateBuilderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task GetStatsAsync_ReturnsTotalsAndUniqueActors()
    {
        await using var context = CreateContext();
        var repo = new AuditStatsRepository(context);
        await context.AuditLogs.AddRangeAsync(
            new AuditLog { EntityType = "Template", EntityId = 1, Action = "published", Actor = "bob", OccurredAt = DateTime.UtcNow.AddDays(-1) },
            new AuditLog { EntityType = "Template", EntityId = 1, Action = "draft_saved", Actor = "bob", OccurredAt = DateTime.UtcNow },
            new AuditLog { EntityType = "Snippet", EntityId = 2, Action = "snippet_created", Actor = "alice", OccurredAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var stats = await repo.GetStatsAsync(new AuditQuery());

        stats.Total.Should().Be(3);
        stats.TemplateCount.Should().Be(2);
        stats.SnippetCount.Should().Be(1);
        stats.UniqueActors.Should().Be(2);
    }

    [Fact]
    public async Task GetStatsAsync_Fills30DayBuckets_WithZeroDays()
    {
        await using var context = CreateContext();
        var repo = new AuditStatsRepository(context);
        var today = DateTime.UtcNow.Date;
        await context.AuditLogs.AddAsync(new AuditLog { EntityType = "Template", EntityId = 1, Action = "created", Actor = "bob", OccurredAt = today });
        await context.SaveChangesAsync();

        var stats = await repo.GetStatsAsync(new AuditQuery());

        stats.DailyBuckets.Should().HaveCount(30);
        stats.DailyBuckets.Should().ContainSingle(b => b.Date == today && b.Count == 1);
        stats.DailyBuckets.Should().Contain(b => b.Date == today.AddDays(-29) && b.Count == 0);
    }

    [Fact]
    public async Task GetStatsAsync_RespectsFromToWindow()
    {
        await using var context = CreateContext();
        var repo = new AuditStatsRepository(context);
        await context.AuditLogs.AddRangeAsync(
            new AuditLog { EntityType = "Template", EntityId = 1, Action = "created", Actor = "bob", OccurredAt = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc) },
            new AuditLog { EntityType = "Template", EntityId = 1, Action = "created", Actor = "bob", OccurredAt = new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc) });
        await context.SaveChangesAsync();

        var stats = await repo.GetStatsAsync(new AuditQuery { From = new DateTime(2026, 8, 3), To = new DateTime(2026, 8, 5) });

        stats.Total.Should().Be(1);
        stats.DailyBuckets.Should().HaveCount(3);   // Aug 3, 4, 5
    }
}
