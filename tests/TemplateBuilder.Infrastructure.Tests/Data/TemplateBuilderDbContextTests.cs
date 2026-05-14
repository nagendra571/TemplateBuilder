using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Infrastructure.Data;

namespace TemplateBuilder.Infrastructure.Tests.Data;

public class TemplateBuilderDbContextTests
{
    private static TemplateBuilderDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<TemplateBuilderDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new TemplateBuilderDbContext(options);
    }

    [Fact]
    public async Task CanInsertAndRetrieveTemplate()
    {
        await using var context = CreateInMemoryContext();

        context.Templates.Add(new Template
        {
            Name = "Test Template",
            TemplateType = "Email",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var template = await context.Templates.FirstOrDefaultAsync(t => t.Name == "Test Template");
        template.Should().NotBeNull();
        template!.TemplateType.Should().Be("Email");
    }

    [Fact]
    public async Task CanInsertTemplateVersion()
    {
        await using var context = CreateInMemoryContext();

        var template = new Template
        {
            Name = "Versioned Template",
            TemplateType = "Report",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Templates.Add(template);
        await context.SaveChangesAsync();

        context.TemplateVersions.Add(new TemplateVersion
        {
            TemplateId = template.Id,
            VersionNumber = 1,
            Body = "<p>Hello</p>",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var version = await context.TemplateVersions.FirstOrDefaultAsync(v => v.TemplateId == template.Id);
        version.Should().NotBeNull();
        version!.Body.Should().Be("<p>Hello</p>");
    }

    [Fact]
    public async Task Template_RowVersion_IsPopulatedAfterSave()
    {
        await using var context = CreateInMemoryContext();

        var template = new Template
        {
            Name = "RV Template",
            TemplateType = "Email",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Templates.Add(template);
        await context.SaveChangesAsync();

        template.RowVersion.Should().NotBeNullOrEmpty();
    }
}
