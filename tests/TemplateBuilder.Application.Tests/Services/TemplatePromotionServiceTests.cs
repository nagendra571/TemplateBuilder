using FluentAssertions;
using Moq;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Application.Tests.Services;

public class TemplatePromotionServiceTests
{
    [Fact]
    public async Task BuildExportAsync_ShapesDocument_WithOrderedVersions_AndFlags()
    {
        var repo = new Mock<ITemplateRepository>();
        var promo = new Mock<ITemplatePromotionRepository>();
        repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new Template
        {
            Id = 7, ExternalKey = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Invoice", TemplateType = "Email", IsActive = true,
            SampleData = "{\"Name\":\"Jane Doe\"}"
        });
        repo.Setup(r => r.GetVersionHistoryAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TemplateVersion>
        {
            new() { VersionNumber = 2, Body = "<p>two</p>", IsActive = false },
            new() { VersionNumber = 1, Body = "<p>one</p>", IsActive = true }
        });
        var svc = new TemplatePromotionService(repo.Object, promo.Object);

        var doc = await svc.BuildExportAsync(7);

        doc.Should().NotBeNull();
        doc!.SchemaVersion.Should().Be(2);
        doc.Exporter.Name.Should().Be("TemplateBuilder.Editor");
        doc.Template.Versions.Select(v => v.VersionNumber).Should().Equal(1, 2);
        doc.Template.Versions.Select(v => v.IsActive).Should().Equal(true, false);
        doc.Template.SampleData.Should().Be("{\"Name\":\"Jane Doe\"}");
        var json = svc.SerializeExport(doc);
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"externalKey\"");
        json.Should().Contain("\"sampleData\"");
    }

    [Theory]
    [InlineData("Invoice v3", "Invoice_v3")]
    [InlineData("a/b\\c:d*e?f\"g<h>i|j", "a_b_c_d_e_f_g_h_i_j")]
    public void SanitizeFileName_StripsInvalidChars(string input, string expected)
        => new TemplatePromotionService(new Mock<ITemplateRepository>().Object, new Mock<ITemplatePromotionRepository>().Object)
            .SanitizeFileName(input).Should().Be(expected);
}
