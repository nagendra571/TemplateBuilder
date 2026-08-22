using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Infrastructure.Data;
using TemplateBuilder.Infrastructure.Repositories;

namespace TemplateBuilder.Infrastructure.Tests.Repositories;

public class TemplatePromotionRepositoryTests
{
    private static TemplateBuilderDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TemplateBuilderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task AddWithVersionsAsync_PreservesOriginalVersionNumbers()
    {
        await using var context = CreateContext();
        var repo = new TemplatePromotionRepository(context);
        var t = await repo.AddWithVersionsAsync(
            new Template { Name = "P", TemplateType = "Email", ExternalKey = Guid.NewGuid() },
            new List<TemplateVersion>
            {
                new() { VersionNumber = 1, Body = "<p>one</p>" },
                new() { VersionNumber = 2, Body = "<p>two</p>" }
            });
        var history = await context.TemplateVersions.Where(v => v.TemplateId == t.Id).ToListAsync();
        history.Select(v => v.VersionNumber).Should().Equal(1, 2);
        t.CurrentVersionId.Should().Be(history.Single(v => v.VersionNumber == 2).Id);
    }

    [Fact]
    public async Task UpdateFromImportAsync_AppendsVersionsFromMaxPlusOne()
    {
        await using var context = CreateContext();
        var repo = new TemplatePromotionRepository(context);
        var t = await repo.AddWithVersionsAsync(
            new Template { Name = "P", TemplateType = "Email", ExternalKey = Guid.NewGuid() },
            new List<TemplateVersion> { new() { VersionNumber = 1, Body = "one" } });

        var assigned = await repo.UpdateFromImportAsync(t, new List<TemplateVersion>
        {
            new() { Body = "imported a", IsActive = false },
            new() { Body = "imported b", IsActive = true }
        });

        assigned.Should().Equal(2, 3);
        var history = await context.TemplateVersions.Where(v => v.TemplateId == t.Id).OrderBy(v => v.VersionNumber).ToListAsync();
        history[1].IsActive.Should().BeFalse();
        history[2].IsActive.Should().BeTrue();
        t.CurrentVersionId.Should().Be(history[2].Id);
    }

    [Fact]
    public async Task AddWithVersionsAsync_AssignsExternalKeyWhenEmpty()
    {
        await using var context = CreateContext();
        var repo = new TemplatePromotionRepository(context);
        var t = await repo.AddWithVersionsAsync(
            new Template { Name = "P", TemplateType = "Email", ExternalKey = Guid.Empty },
            new List<TemplateVersion> { new() { VersionNumber = 1, Body = "<p>one</p>" } });

        t.ExternalKey.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetByExternalKeyAsync_RoundTrips()
    {
        await using var context = CreateContext();
        var repo = new TemplatePromotionRepository(context);
        var key = Guid.NewGuid();
        await repo.AddWithVersionsAsync(new Template { Name = "P", TemplateType = "Email", ExternalKey = key }, new List<TemplateVersion>());
        (await repo.GetByExternalKeyAsync(key)).Should().NotBeNull();
        (await repo.GetByExternalKeyAsync(Guid.NewGuid())).Should().BeNull();
    }
}
