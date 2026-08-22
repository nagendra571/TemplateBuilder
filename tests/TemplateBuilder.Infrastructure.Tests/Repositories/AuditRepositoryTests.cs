using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Infrastructure.Data;
using TemplateBuilder.Infrastructure.Repositories;

namespace TemplateBuilder.Infrastructure.Tests.Repositories;

public class AuditRepositoryTests
{
    private static TemplateBuilderDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TemplateBuilderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task QueryAsync_FiltersByActionActorSearch_AndPaginatesDesc()
    {
        await using var context = CreateContext();
        var repo = new AuditRepository(context);
        for (var i = 1; i <= 5; i++)
            await repo.AddAsync(new AuditLog { EntityType = "Template", EntityId = 1, Action = "published", Actor = "bob", Comment = $"c{i}", OccurredAt = DateTime.UtcNow.AddMinutes(-i) }, CancellationToken.None);

        var page = await repo.QueryAsync(new AuditQuery { Action = "published", Actor = "bob", Page = 1, PageSize = 2 });

        page.Should().HaveCount(2);
        page[0].Comment.Should().Be("c1");   // desc order
        (await repo.CountAsync(new AuditQuery { Action = "published" })).Should().Be(5);
    }

    [Fact]
    public async Task QueryAsync_ToDateIsExclusive_FromDateInclusive()
    {
        await using var context = CreateContext();
        var repo = new AuditRepository(context);
        var day = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        await repo.AddAsync(new AuditLog { EntityType = "Template", EntityId = 1, Action = "created", Actor = "bob", OccurredAt = day }, CancellationToken.None);
        await repo.AddAsync(new AuditLog { EntityType = "Template", EntityId = 1, Action = "created", Actor = "bob", OccurredAt = day.AddDays(1) }, CancellationToken.None);

        var rows = await repo.QueryAsync(new AuditQuery { From = new DateTime(2026, 8, 1), To = new DateTime(2026, 8, 1) });

        rows.Should().ContainSingle(r => r.OccurredAt == day);
    }

    [Fact]
    public async Task GetLastOccurrenceAsync_ReturnsLatestMatching()
    {
        await using var context = CreateContext();
        var repo = new AuditRepository(context);
        await repo.AddAsync(new AuditLog { EntityType = "Template", EntityId = 7, Action = "draft_saved", Actor = "bob", OccurredAt = DateTime.UtcNow.AddMinutes(-2) }, CancellationToken.None);
        var latest = DateTime.UtcNow;
        await repo.AddAsync(new AuditLog { EntityType = "Template", EntityId = 7, Action = "draft_saved", Actor = "bob", OccurredAt = latest }, CancellationToken.None);

        var result = await repo.GetLastOccurrenceAsync("Template", 7, "draft_saved");

        result.Should().BeCloseTo(latest, TimeSpan.FromSeconds(5));
    }
}
