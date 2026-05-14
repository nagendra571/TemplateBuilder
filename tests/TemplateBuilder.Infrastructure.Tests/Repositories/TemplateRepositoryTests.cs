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
}
