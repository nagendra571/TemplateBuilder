using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Editor.Controllers;
using TemplateBuilder.Editor.Models;

namespace TemplateBuilder.Editor.Tests.Controllers;

public class AuditControllerTests
{
    private static AuditController CreateController(IAuditRepository? auditRepo = null, IAuditStatsRepository? stats = null)
        => new(auditRepo ?? new Mock<IAuditRepository>().Object, stats ?? new Mock<IAuditStatsRepository>().Object);

    [Fact]
    public async Task Index_ReturnsViewWithRowsAndStats()
    {
        var audit = new Mock<IAuditRepository>();
        audit.Setup(r => r.QueryAsync(It.IsAny<AuditQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLog> { new() { Id = 1, EntityType = "Template", EntityId = 1, Action = "published", Actor = "bob", OccurredAt = DateTime.UtcNow } });
        audit.Setup(r => r.CountAsync(It.IsAny<AuditQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var stats = new Mock<IAuditStatsRepository>();
        stats.Setup(s => s.GetStatsAsync(It.IsAny<AuditQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditStats { Total = 1 });
        var controller = CreateController(auditRepo: audit.Object, stats: stats.Object);

        var result = await controller.Index(null, null, null, null, null, null);

        result.Should().BeOfType<ViewResult>();
        var model = ((ViewResult)result).Model as AuditIndexViewModel;
        model!.Rows.Should().HaveCount(1);
        model.Total.Should().Be(1);
        model.Stats.Total.Should().Be(1);
        model.KnownActions.Should().Contain(AuditActions.Published);
    }

    [Fact]
    public async Task Stats_ReturnsOkObjectResult()
    {
        var stats = new Mock<IAuditStatsRepository>();
        stats.Setup(s => s.GetStatsAsync(It.IsAny<AuditQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditStats { Total = 3 });
        var controller = CreateController(stats: stats.Object);

        var result = await controller.Stats(null, null, null, null, null, null);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Export_ReturnsCsvWithBomAndColumns()
    {
        var audit = new Mock<IAuditRepository>();
        audit.Setup(r => r.QueryAsync(It.IsAny<AuditQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLog>
            {
                new() { Id = 1, EntityType = "Template", EntityId = 1, Action = "published", Actor = "bob", Comment = "a,b", OccurredAt = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc) }
            });
        var controller = CreateController(auditRepo: audit.Object);

        var result = await controller.Export(null, null, null, null, null, null);

        var file = (FileContentResult)result;
        file.ContentType.Should().Be("text/csv");
        file.FileContents.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);   // UTF-8 BOM
        var text = Encoding.UTF8.GetString(file.FileContents, 3, file.FileContents.Length - 3);
        text.Should().StartWith("OccurredAt,EntityType,EntityId,Action,Actor,Comment,BeforeState,AfterState");
        text.Should().Contain("\"a,b\"");   // quoted comma
    }
}
