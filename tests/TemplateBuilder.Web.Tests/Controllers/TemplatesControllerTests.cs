using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Web.Controllers;
using TemplateBuilder.Web.Models;

namespace TemplateBuilder.Web.Tests.Controllers;

public class TemplatesControllerTests
{
    private static TemplatesController CreateController(
        ITemplateRepository? repo = null,
        ISqlViewDiscoveryService? discovery = null,
        ITemplateEngine? engine = null,
        IHtmlSanitizerService? sanitizer = null)
    {
        var mockRepo = repo ?? new Mock<ITemplateRepository>().Object;
        var mockDiscovery = discovery ?? new Mock<ISqlViewDiscoveryService>().Object;
        var mockEngine = engine ?? new Mock<ITemplateEngine>().Object;
        var mockSanitizer = sanitizer ?? new Mock<IHtmlSanitizerService>().Object;
        return new TemplatesController(mockRepo, mockDiscovery, mockEngine, mockSanitizer);
    }

    [Fact]
    public async Task Index_ReturnsViewWithTemplates()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Template>
            {
                new() { Id = 1, Name = "A", TemplateType = "Email" }
            });
        var controller = CreateController(mockRepo.Object);

        var result = await controller.Index(null, null);

        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task Edit_NonExistentTemplate_ReturnsNotFound()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Template?)null);
        var mockDiscovery = new Mock<ISqlViewDiscoveryService>();
        mockDiscovery.Setup(d => d.GetViewNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());
        var controller = CreateController(mockRepo.Object, mockDiscovery.Object);

        var result = await controller.Edit(99);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Duplicate_CreatesNewTemplateWithSameBodyAndRedirects()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        var source = new Template
        {
            Id = 1, Name = "Invoice Email", TemplateType = "Email",
            Description = "Monthly invoice",
            CurrentVersion = new TemplateVersion { Id = 10, Body = "<p>Hello</p>", VersionNumber = 1 },
            CurrentVersionId = 10
        };
        mockRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(source);
        mockRepo.Setup(r => r.CreateAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template t, CancellationToken _) => { t.Id = 99; return t; });
        mockRepo.Setup(r => r.PublishVersionAsync(99, It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, TemplateVersion v, CancellationToken _) => { v.Id = 200; return v; });
        var controller = CreateController(mockRepo.Object);

        var result = await controller.Duplicate(1, new DuplicateRequest("Copy of Invoice Email"));

        result.Should().BeOfType<OkObjectResult>();
        mockRepo.Verify(r => r.CreateAsync(
            It.Is<Template>(t => t.Name == "Copy of Invoice Email" && t.TemplateType == "Email"),
            It.IsAny<CancellationToken>()), Times.Once);
        mockRepo.Verify(r => r.PublishVersionAsync(99,
            It.Is<TemplateVersion>(v => v.Body == "<p>Hello</p>" && v.VersionNumber == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Duplicate_NonExistentSource_ReturnsNotFound()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Template?)null);
        var controller = CreateController(mockRepo.Object);

        var result = await controller.Duplicate(999, new DuplicateRequest("Copy"));

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Preview_OversizedPayload_ReturnsBadRequest()
    {
        var controller = CreateController();
        var bigJson = new string('x', 65 * 1024); // > 64 KB
        var result = await controller.Preview(1, new PreviewRequest("body", bigJson));
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Preview_InvalidJson_ReturnsBadRequest()
    {
        var controller = CreateController();
        var result = await controller.Preview(1, new PreviewRequest("body", "{invalid"));
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task SaveVersion_NullName_ReturnsBadRequest()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template { Id = 1, Name = "A", TemplateType = "Email" });
        var controller = CreateController(mockRepo.Object);

        var result = await controller.SaveVersion(1, new SaveVersionRequest(null!, "Email", null, "body", null));

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
