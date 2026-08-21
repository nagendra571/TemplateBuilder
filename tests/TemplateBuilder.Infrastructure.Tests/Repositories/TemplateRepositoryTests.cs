using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Infrastructure.Data;
using TemplateBuilder.Infrastructure.Repositories;

namespace TemplateBuilder.Infrastructure.Tests.Repositories;

public class TemplateRepositoryTests
{
    private static TemplateBuilderDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TemplateBuilderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task CreateAsync_PersistsTemplate_ReturnsWithId()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);

        var template = await repo.CreateAsync(new Template
        {
            Name = "Invoice Email",
            TemplateType = "Email"
        });

        template.Id.Should().BeGreaterThan(0);
        template.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetByIdAsync_ExistingTemplate_ReturnsTemplate()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var created = await repo.CreateAsync(new Template { Name = "A", TemplateType = "Report" });

        var result = await repo.GetByIdAsync(created.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("A");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);

        var result = await repo.GetByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTemplateAsync_PersistsSampleData_RoundTrips()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "Invoice", TemplateType = "Email" });

        template.SampleData = "{\"Name\":\"Jane Doe\"}";
        await repo.UpdateTemplateAsync(template);

        var reloaded = await repo.GetByIdAsync(template.Id);
        reloaded.Should().NotBeNull();
        reloaded!.SampleData.Should().Be("{\"Name\":\"Jane Doe\"}");
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsMatchingTemplate()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        await repo.CreateAsync(new Template { Name = "Welcome Email", TemplateType = "Email" });

        var result = await repo.GetByNameAsync("Welcome Email");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PublishVersionAsync_UpdatesCurrentVersionIdAtomically()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "B", TemplateType = "Notice" });

        var version = await repo.PublishVersionAsync(template.Id, new TemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 1,
            Body = "<p>Hello</p>"
        });

        var versionId = await repo.GetCurrentVersionIdAsync(template.Id);
        versionId.Should().Be(version.Id);
    }

    [Fact]
    public async Task GetVersionBodyAsync_ReturnsCorrectBody()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "C", TemplateType = "Email" });
        var version = await repo.PublishVersionAsync(template.Id, new TemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 1,
            Body = "<h1>{{ model.Title }}</h1>"
        });

        var body = await repo.GetVersionBodyAsync(version.Id);

        body.Should().Be("<h1>{{ model.Title }}</h1>");
    }

    [Fact]
    public async Task GetNextVersionNumberAsync_NoVersions_Returns1()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "D", TemplateType = "Report" });

        var next = await repo.GetNextVersionNumberAsync(template.Id);

        next.Should().Be(1);
    }

    [Fact]
    public async Task GetNextVersionNumberAsync_ExistingVersions_ReturnsNext()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "E", TemplateType = "Report" });
        await repo.PublishVersionAsync(template.Id, new TemplateVersion { TemplateId = template.Id, VersionNumber = 1, Body = "v1" });
        await repo.PublishVersionAsync(template.Id, new TemplateVersion { TemplateId = template.Id, VersionNumber = 2, Body = "v2" });

        var next = await repo.GetNextVersionNumberAsync(template.Id);

        next.Should().Be(3);
    }

    [Fact]
    public async Task GetAllAsync_OnlyReturnsActiveTemplates()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        await repo.CreateAsync(new Template { Name = "Active", TemplateType = "Email" });
        var inactive = await repo.CreateAsync(new Template { Name = "Inactive", TemplateType = "Email" });
        inactive.IsActive = false;
        await repo.UpdateTemplateAsync(inactive);

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Active");
    }

    [Fact]
    public async Task GetVersionHistoryAsync_ReturnsVersionsDescending()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "F", TemplateType = "Email" });
        await repo.PublishVersionAsync(template.Id, new TemplateVersion { TemplateId = template.Id, VersionNumber = 1, Body = "v1" });
        await repo.PublishVersionAsync(template.Id, new TemplateVersion { TemplateId = template.Id, VersionNumber = 2, Body = "v2" });

        var history = await repo.GetVersionHistoryAsync(template.Id);

        history.Should().HaveCount(2);
        history[0].VersionNumber.Should().Be(2);
        history[1].VersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetByNameAsync_UnknownName_ReturnsNull()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);

        var result = await repo.GetByNameAsync("DoesNotExist");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_PopulatesCurrentVersion_VersionNumberNotZero()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "Versioned", TemplateType = "Email" });
        await repo.PublishVersionAsync(template.Id, new TemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 1,
            Body = "<p>Hello</p>"
        });

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(1);
        result[0].CurrentVersion.Should().NotBeNull();
        result[0].CurrentVersion!.VersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetLastActiveVersionAsync_ReturnsLatestActive_SkipsDrafts()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "A", TemplateType = "Email" });
        await repo.PublishVersionAsync(template.Id, new TemplateVersion { TemplateId = template.Id, VersionNumber = 1, Body = "active-1", IsActive = true });
        await repo.PublishVersionAsync(template.Id, new TemplateVersion { TemplateId = template.Id, VersionNumber = 2, Body = "draft-2", IsActive = false });
        var active3 = await repo.PublishVersionAsync(template.Id, new TemplateVersion { TemplateId = template.Id, VersionNumber = 3, Body = "active-3", IsActive = true });
        await repo.PublishVersionAsync(template.Id, new TemplateVersion { TemplateId = template.Id, VersionNumber = 4, Body = "draft-4", IsActive = false });

        var result = await repo.GetLastActiveVersionAsync(template.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(active3.Id);
        result.Body.Should().Be("active-3");
    }

    [Fact]
    public async Task GetLastActiveVersionAsync_ReturnsNull_WhenAllVersionsAreDrafts()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "A", TemplateType = "Email" });
        await repo.PublishVersionAsync(template.Id, new TemplateVersion { TemplateId = template.Id, VersionNumber = 1, Body = "draft", IsActive = false });

        var result = await repo.GetLastActiveVersionAsync(template.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetVersionAsync_ReturnsSingleVersion()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var template = await repo.CreateAsync(new Template { Name = "A", TemplateType = "Email" });
        var v = await repo.PublishVersionAsync(template.Id, new TemplateVersion { TemplateId = template.Id, VersionNumber = 1, Body = "x" });

        var result = await repo.GetVersionAsync(v.Id);

        result.Should().NotBeNull();
        result!.Body.Should().Be("x");
    }

    [Fact]
    public async Task CreateAsync_AssignsNonEmptyExternalKey()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var t = await repo.CreateAsync(new Template { Name = "A", TemplateType = "Email" });
        t.ExternalKey.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ExternalKeys_AreUniquePerRow()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var a = await repo.CreateAsync(new Template { Name = "A", TemplateType = "Email" });
        var b = await repo.CreateAsync(new Template { Name = "B", TemplateType = "Email" });
        a.ExternalKey.Should().NotBe(b.ExternalKey);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTemplateAndVersions_ReturnsTrueThenFalse()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        var t = await repo.CreateAsync(new Template { Name = "A", TemplateType = "Email" });
        await repo.PublishVersionAsync(t.Id, new TemplateVersion { Body = "<p>v1</p>" });

        (await repo.DeleteAsync(t.Id)).Should().BeTrue();
        (await repo.GetByIdAsync(t.Id)).Should().BeNull();
        (await repo.GetVersionHistoryAsync(t.Id)).Should().BeEmpty();
        (await repo.DeleteAsync(t.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task GetAllIncludingInactiveAsync_IncludesInactiveTemplates()
    {
        await using var context = CreateContext();
        var repo = new TemplateRepository(context);
        await repo.CreateAsync(new Template { Name = "Off", TemplateType = "Email", IsActive = false });
        await repo.CreateAsync(new Template { Name = "On", TemplateType = "Email", IsActive = true });

        var all = await repo.GetAllIncludingInactiveAsync();

        all.Select(t => t.Name).Should().BeEquivalentTo("Off", "On");
    }
}
