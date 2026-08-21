using System.Text.Json;
using FluentAssertions;
using Moq;
using TemplateBuilder.Application.DTOs;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Application.Tests.Services;

public class TemplateHealthServiceTests
{
    [Fact]
    public async Task Extract_HandlesNestedLoopsConditionals_AndIgnoresLiterals()
    {
        var svc = new TemplateHealthService(new Mock<ITemplateRepository>().Object, new Mock<ISqlViewDiscoveryService>().Object);
        const string body = """
<p>{{ model.FirstName }} {{ model.User.Name }}</p>
{{ for item in model.Items }}{{ item.Qty }}{{ end }}
{{ if model.HasDiscount }}yes{{ end }}
{{ "literal model.Nope" }} {{ 'model.Single' }}
""";
        var paths = await svc.ExtractModelPathsAsync(body);
        paths.Should().BeEquivalentTo("FirstName", "User.Name", "Items", "HasDiscount");
    }

    [Fact]
    public async Task Check_ReportsMissingView_AndMissingColumn()
    {
        var repo = new Mock<ITemplateRepository>();
        var discovery = new Mock<ISqlViewDiscoveryService>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Template
        {
            Id = 1, Name = "T", SourceView = "v_Gone",
            SourceViewSnapshot = JsonSerializer.Serialize(new { takenAt = DateTime.UtcNow, columns = new List<SqlColumnInfo> { new("FirstName", "nvarchar", MaxLength: 100, IsNullable: false) } }),
            CurrentVersion = new TemplateVersion { Body = "<p>{{ model.FirstName }}</p><p>{{ model.Nope }}</p>" }
        });
        discovery.Setup(d => d.GetViewColumnsAsync("v_Gone", It.IsAny<CancellationToken>())).ReturnsAsync(new List<SqlColumnInfo>());
        var svc = new TemplateHealthService(repo.Object, discovery.Object);

        var report = await svc.CheckAsync(1);

        report.ViewMissing.Should().BeTrue();
        report.Findings.Should().Contain(f => f.Code == "view_missing" && f.Severity == HealthSeverity.Critical);
    }

    [Fact]
    public async Task Check_ReportsTypeAndLengthDrift_FromSnapshot()
    {
        var repo = new Mock<ITemplateRepository>();
        var discovery = new Mock<ISqlViewDiscoveryService>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Template
        {
            Id = 1, Name = "T", SourceView = "v_Cust",
            SourceViewSnapshot = JsonSerializer.Serialize(new { takenAt = DateTime.UtcNow, columns = new List<SqlColumnInfo> { new("CustomerName", "nvarchar", MaxLength: 100, IsNullable: true) } }),
            CurrentVersion = new TemplateVersion { Body = "<p>{{ model.CustomerName }}</p>" }
        });
        discovery.Setup(d => d.GetViewColumnsAsync("v_Cust", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SqlColumnInfo> { new("CustomerName", "nvarchar", MaxLength: 500, IsNullable: false) });
        var svc = new TemplateHealthService(repo.Object, discovery.Object);

        var report = await svc.CheckAsync(1);

        report.Findings.Should().Contain(f => f.Code == "column_length_changed" && f.Severity == HealthSeverity.Warning);
        report.Findings.Should().Contain(f => f.Code == "column_nullability_changed" && f.Severity == HealthSeverity.Warning);
    }

    [Fact]
    public async Task Check_ReportsTypeChange_WithoutRedundantLengthFinding()
    {
        var repo = new Mock<ITemplateRepository>();
        var discovery = new Mock<ISqlViewDiscoveryService>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Template
        {
            Id = 1, Name = "T", SourceView = "v_C",
            SourceViewSnapshot = JsonSerializer.Serialize(new { takenAt = DateTime.UtcNow, columns = new List<SqlColumnInfo> { new("Amount", "nvarchar", MaxLength: 100, IsNullable: true) } }),
            CurrentVersion = new TemplateVersion { Body = "<p>{{ model.Amount }}</p>" }
        });
        discovery.Setup(d => d.GetViewColumnsAsync("v_C", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SqlColumnInfo> { new("Amount", "int", MaxLength: null, IsNullable: false) });
        var report = await new TemplateHealthService(repo.Object, discovery.Object).CheckAsync(1);

        report.Findings.Should().Contain(f => f.Code == "column_type_changed");
        report.Findings.Should().NotContain(f => f.Code == "column_length_changed");
    }

    [Fact]
    public async Task Check_DottedToken_IsExcludedFromColumnDriftComparison()
    {
        var repo = new Mock<ITemplateRepository>();
        var discovery = new Mock<ISqlViewDiscoveryService>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Template
        {
            Id = 1, Name = "T", SourceView = "v_Nested",
            SourceViewSnapshot = JsonSerializer.Serialize(new { takenAt = DateTime.UtcNow, columns = new List<SqlColumnInfo> { new("Amount", "int", MaxLength: null, IsNullable: false) } }),
            CurrentVersion = new TemplateVersion { Body = "<p>{{ model.User.Name }} {{ model.Amount }}</p>" }
        });
        // The live view has neither a "User" column nor a "User.Name" column — only "Amount".
        discovery.Setup(d => d.GetViewColumnsAsync("v_Nested", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SqlColumnInfo> { new("Total", "int", MaxLength: null, IsNullable: false) });
        var svc = new TemplateHealthService(repo.Object, discovery.Object);

        var report = await svc.CheckAsync(1);

        report.Tokens.Should().Contain("User.Name");
        report.Findings.Should().NotContain(f => f.Code == "column_missing" && f.Message.Contains("User"));
        report.Findings.Should().Contain(f => f.Code == "column_missing" && f.Message.Contains("Amount"));
    }

    [Fact]
    public async Task Check_UnboundTemplateWithTokens_ReportsWarning()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Template
        {
            Id = 1, Name = "T", SourceView = null, CurrentVersion = new TemplateVersion { Body = "<p>{{ model.FirstName }}</p>" }
        });
        var report = await new TemplateHealthService(repo.Object, new Mock<ISqlViewDiscoveryService>().Object).CheckAsync(1);
        report.Findings.Should().Contain(f => f.Code == "unbound_tokens" && f.Severity == HealthSeverity.Warning);
    }
}
