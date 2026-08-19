using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Editor.Controllers;
using TemplateBuilder.Editor.Models;

namespace TemplateBuilder.Editor.Tests.Controllers;

public class TemplatesControllerTests
{
    private static TemplatesController CreateController(
        ITemplateRepository? repo = null,
        ISqlViewDiscoveryService? discovery = null,
        ITemplateEngine? engine = null,
        IHtmlSanitizerService? sanitizer = null,
        ISampleDataGenerator? sampleDataGenerator = null)
    {
        var mockRepo = repo ?? new Mock<ITemplateRepository>().Object;
        var mockDiscovery = discovery ?? new Mock<ISqlViewDiscoveryService>().Object;
        var mockEngine = engine ?? new Mock<ITemplateEngine>().Object;
        var mockSanitizer = sanitizer ?? new Mock<IHtmlSanitizerService>().Object;
        var mockSampleDataGenerator = sampleDataGenerator ?? new Mock<ISampleDataGenerator>().Object;
        return new TemplatesController(mockRepo, mockDiscovery, mockEngine, mockSanitizer, mockSampleDataGenerator);
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

    [Fact]
    public async Task Validate_EmptyBody_ReturnsBadRequest()
    {
        var controller = CreateController();
        var result = await controller.Validate(1, new ValidateRequest(""), CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Validate_ValidTemplate_ReturnsOkWithValidTrue()
    {
        var mockEngine = new Mock<ITemplateEngine>();
        mockEngine.Setup(e => e.RenderBodyAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("rendered");
        var controller = CreateController(engine: mockEngine.Object);
        var result = await controller.Validate(1, new ValidateRequest("<p>Hello</p>"), CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(new { valid = true });
    }

    [Fact]
    public async Task Validate_InvalidTemplate_ReturnsOkWithValidFalse()
    {
        var mockEngine = new Mock<ITemplateEngine>();
        mockEngine.Setup(e => e.RenderBodyAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected token at line 2"));
        var controller = CreateController(engine: mockEngine.Object);
        var result = await controller.Validate(1, new ValidateRequest("{{ invalid"), CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(new { valid = false, message = "Unexpected token at line 2" });
    }

    [Fact]
    public async Task GenerateSampleData_ReturnsGeneratedDictionary()
    {
        var mockGen = new Mock<ISampleDataGenerator>();
        mockGen.Setup(g => g.GenerateAsync("v_Test", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?> { ["Name"] = "Jane Doe" });
        var controller = CreateController(sampleDataGenerator: mockGen.Object);

        var result = await controller.GenerateSampleData(new GenerateSampleDataRequest("v_Test", null));

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(new { sampleData = new Dictionary<string, object?> { ["Name"] = "Jane Doe" } });
    }

    [Fact]
    public async Task GenerateSampleData_NoViewOrBody_ReturnsEmptyDictionary()
    {
        var mockGen = new Mock<ISampleDataGenerator>();
        mockGen.Setup(g => g.GenerateAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, object?>());
        var controller = CreateController(sampleDataGenerator: mockGen.Object);

        var result = await controller.GenerateSampleData(new GenerateSampleDataRequest(null, null));

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(new { sampleData = new Dictionary<string, object?>() });
    }

    [Fact]
    public async Task SaveSampleData_ExistingTemplate_SavesAndReturnsOk()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        var template = new Template { Id = 1, Name = "A", TemplateType = "Email" };
        mockRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var controller = CreateController(mockRepo.Object);

        var result = await controller.SaveSampleData(1, new SaveSampleDataRequest("{\"Name\":\"Jane\"}"));

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(new { saved = true });
        template.SampleData.Should().Be("{\"Name\":\"Jane\"}");
        mockRepo.Verify(r => r.UpdateTemplateAsync(template, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveSampleData_BlankValue_ClearsSampleData()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        var template = new Template { Id = 1, Name = "A", TemplateType = "Email", SampleData = "{\"Old\":1}" };
        mockRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        var controller = CreateController(mockRepo.Object);

        var result = await controller.SaveSampleData(1, new SaveSampleDataRequest("   "));

        result.Should().BeOfType<OkObjectResult>();
        template.SampleData.Should().BeNull();
    }

    [Fact]
    public async Task SaveSampleData_NonExistentTemplate_ReturnsNotFound()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Template?)null);
        var controller = CreateController(mockRepo.Object);

        var result = await controller.SaveSampleData(99, new SaveSampleDataRequest("{}"));

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetVersionBody_ExistingVersion_ReturnsBodyJson()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetVersionBodyAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync("<p>Hello {{ model.Name }}</p>");
        var controller = CreateController(mockRepo.Object);

        var result = await controller.GetVersionBody(1, 42);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(new { body = "<p>Hello {{ model.Name }}</p>" });
    }

    [Fact]
    public async Task GetVersionBody_NonExistentVersion_ReturnsNotFound()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetVersionBodyAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        var controller = CreateController(mockRepo.Object);

        var result = await controller.GetVersionBody(1, 99);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
