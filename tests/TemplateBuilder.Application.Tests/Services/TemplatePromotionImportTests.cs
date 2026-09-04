using System.Text;
using FluentAssertions;
using Moq;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Application.Tests.Services;

public class TemplatePromotionImportTests
{
    private static TemplatePromotionService Create(
        Mock<ITemplateRepository>? repo = null, Mock<ITemplatePromotionRepository>? promo = null)
        => new(repo?.Object ?? new Mock<ITemplateRepository>().Object,
               promo?.Object ?? new Mock<ITemplatePromotionRepository>().Object);

    [Fact]
    public async Task Import_RejectsSchemaVersion1File()
    {
        var svc = Create();
        var json = """{"schemaVersion":1,"template":{"name":"X"}}""";
        var result = await svc.ImportAsync(Encoding.UTF8.GetBytes(json), "bob");
        result.Errors.Should().ContainSingle(e => e.Reason!.Contains("schemaVersion"));
    }

    [Fact]
    public async Task Import_RejectsSchemaVersion2File()
    {
        var svc = Create();
        var json = """{"schemaVersion":2,"template":{"name":"X"}}""";
        var result = await svc.ImportAsync(Encoding.UTF8.GetBytes(json), "bob");
        result.Errors.Should().ContainSingle(e => e.Reason!.Contains("expected 3"));
    }

    [Fact]
    public async Task Import_CreatesTemplate_PreservingFlags()
    {
        var promo = new Mock<ITemplatePromotionRepository>();
        var key = Guid.NewGuid();
        promo.Setup(p => p.GetByExternalKeyAsync(key, It.IsAny<CancellationToken>())).ReturnsAsync((Template?)null);
        Template? captured = null;
        promo.Setup(p => p.AddWithVersionsAsync(It.IsAny<Template>(), It.IsAny<IReadOnlyList<TemplateVersion>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template t, IReadOnlyList<TemplateVersion> _, CancellationToken _) => { captured = t; return t; });
        var svc = Create(promo: promo);
        var doc = new TemplateExportDocument
        {
            Template = new TemplateExportTemplate
            {
                ExternalKey = key, Name = "X", TemplateType = "Email", IsActive = false,
                SampleData = "{\"Name\":\"Jane\"}",
                Versions = { new() { VersionNumber = 1, Body = "<p>ok</p>", IsActive = true }, new() { VersionNumber = 2, Body = "<p>d</p>", IsActive = false } }
            }
        };

        var result = await svc.ImportAsync(Encoding.UTF8.GetBytes(svc.SerializeExport(doc)), "bob");

        result.Created.Should().ContainSingle(c => c.Name == "X");
        captured!.IsActive.Should().BeFalse();
        captured.ExternalKey.Should().Be(key);
        captured.SampleData.Should().Be("{\"Name\":\"Jane\"}");
    }

    [Fact]
    public async Task Import_UpdatesExisting_PreservingVersionFlags()
    {
        var promo = new Mock<ITemplatePromotionRepository>();
        var key = Guid.NewGuid();
        var existing = new Template { Id = 9, Name = "Old", TemplateType = "Email", IsActive = true, SampleData = "{\"Name\":\"Old\"}" };
        promo.Setup(p => p.GetByExternalKeyAsync(key, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        IReadOnlyList<TemplateVersion>? captured = null;
        promo.Setup(p => p.UpdateFromImportAsync(existing, It.IsAny<IReadOnlyList<TemplateVersion>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template _, IReadOnlyList<TemplateVersion> vs, CancellationToken _) => { captured = vs; return vs.Select(v => v.VersionNumber).ToArray(); });
        var svc = Create(promo: promo);
        var doc = new TemplateExportDocument
        {
            Template = new TemplateExportTemplate
            {
                ExternalKey = key, Name = "X", TemplateType = "Email", IsActive = true,
                SampleData = "{\"Name\":\"New\"}",
                Versions = { new() { VersionNumber = 1, Body = "<p>a</p>", IsActive = false } }
            }
        };

        var result = await svc.ImportAsync(Encoding.UTF8.GetBytes(svc.SerializeExport(doc)), "bob");

        result.Updated.Should().ContainSingle(u => u.Name == "X");
        result.Skipped.Should().BeEmpty();
        captured!.Single().IsActive.Should().BeFalse();
        existing.SampleData.Should().Be("{\"Name\":\"New\"}");
    }

    [Fact]
    public async Task Import_RoundTripsSubject()
    {
        var promo = new Mock<ITemplatePromotionRepository>();
        var key = Guid.NewGuid();
        promo.Setup(p => p.GetByExternalKeyAsync(key, It.IsAny<CancellationToken>())).ReturnsAsync((Template?)null);
        IReadOnlyList<TemplateVersion>? captured = null;
        promo.Setup(p => p.AddWithVersionsAsync(It.IsAny<Template>(), It.IsAny<IReadOnlyList<TemplateVersion>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template t, IReadOnlyList<TemplateVersion> vs, CancellationToken _) => { captured = vs; return t; });
        var svc = Create(promo: promo);
        var doc = new TemplateExportDocument
        {
            Template = new TemplateExportTemplate
            {
                ExternalKey = key, Name = "X", TemplateType = "Email", IsActive = true,
                Versions = { new() { VersionNumber = 1, Body = "<p>ok</p>", Subject = "Hi there", IsActive = true } }
            }
        };

        await svc.ImportAsync(Encoding.UTF8.GetBytes(svc.SerializeExport(doc)), "bob");

        captured!.Single().Subject.Should().Be("Hi there");
    }

    [Fact]
    public async Task Import_RejectsNullTemplate()
    {
        var svc = Create();
        var json = """{"schemaVersion":3,"template":null}""";
        var result = await svc.ImportAsync(Encoding.UTF8.GetBytes(json), "bob");
        result.Errors.Should().ContainSingle(e => e.Reason!.Contains("missing"));
    }

    [Fact]
    public async Task Import_RejectsEmptyName()
    {
        var svc = Create();
        var doc = new TemplateExportDocument
        {
            Template = new TemplateExportTemplate
            {
                ExternalKey = Guid.NewGuid(), Name = "   ", TemplateType = "Email",
                Versions = { new() { VersionNumber = 1, Body = "<p>ok</p>" } }
            }
        };
        var result = await svc.ImportAsync(Encoding.UTF8.GetBytes(svc.SerializeExport(doc)), "bob");
        result.Errors.Should().ContainSingle(e => e.Reason!.Contains("missing"));
    }

    [Fact]
    public async Task Import_RejectsZeroVersions()
    {
        var svc = Create();
        var doc = new TemplateExportDocument
        {
            Template = new TemplateExportTemplate
            {
                ExternalKey = Guid.NewGuid(), Name = "X", TemplateType = "Email",
                Versions = new()
            }
        };
        var result = await svc.ImportAsync(Encoding.UTF8.GetBytes(svc.SerializeExport(doc)), "bob");
        result.Errors.Should().ContainSingle(e => e.Reason!.Contains("missing"));
    }

    [Fact]
    public async Task Import_RejectsInvalidScribanBody()
    {
        var svc = Create();
        var doc = new TemplateExportDocument
        {
            Template = new TemplateExportTemplate
            {
                ExternalKey = Guid.NewGuid(), Name = "X", TemplateType = "Email",
                Versions = { new() { VersionNumber = 1, Body = "{{ end }}" } }
            }
        };
        var result = await svc.ImportAsync(Encoding.UTF8.GetBytes(svc.SerializeExport(doc)), "bob");
        result.Errors.Should().ContainSingle(e => e.Reason!.Contains("Version 1"));
    }
}
