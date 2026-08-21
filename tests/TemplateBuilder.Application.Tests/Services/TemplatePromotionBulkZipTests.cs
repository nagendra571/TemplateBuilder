using FluentAssertions;
using Moq;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Application.Tests.Services;

public class TemplatePromotionBulkZipTests
{
    [Fact]
    public async Task BulkZipAsync_ContainsPerTemplateFiles_AndSummaryManifest()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Template
        {
            Id = 1, ExternalKey = Guid.NewGuid(), Name = "Invoice v3", TemplateType = "Email"
        });
        repo.Setup(r => r.GetVersionHistoryAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TemplateVersion> { new() { VersionNumber = 1, Body = "<p>one</p>", IsActive = true } });
        repo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync((Template?)null);
        var svc = new TemplatePromotionService(repo.Object, new Mock<ITemplatePromotionRepository>().Object);

        var zip = await svc.BuildBulkZipAsync(new[] { 1, 2 });

        using var ms = new MemoryStream(zip);
        using var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        archive.Entries.Select(e => e.Name).Should().Contain("Invoice_v3.template.json");
        archive.Entries.Select(e => e.Name).Should().Contain("_summary.json");
        using var sr = new StreamReader(archive.GetEntry("_summary.json")!.Open());
        var summary = sr.ReadToEnd();
        summary.Should().Contain("\"schemaVersion\": 2");
        summary.Should().Contain("not found");
    }
}
