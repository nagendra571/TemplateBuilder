using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        ISampleDataGenerator? sampleDataGenerator = null,
        Mock<ITemplatePromotionService>? promo = null,
        ITemplateHealthService? health = null)
    {
        var mockRepo = repo ?? new Mock<ITemplateRepository>().Object;
        var mockDiscovery = discovery ?? new Mock<ISqlViewDiscoveryService>().Object;
        var mockEngine = engine ?? new Mock<ITemplateEngine>().Object;
        var mockSanitizer = sanitizer ?? new Mock<IHtmlSanitizerService>().Object;
        var mockSampleDataGenerator = sampleDataGenerator ?? new Mock<ISampleDataGenerator>().Object;
        var mockPromo = promo?.Object ?? new Mock<ITemplatePromotionService>().Object;
        var mockHealth = health ?? new Mock<ITemplateHealthService>().Object;
        return new TemplatesController(mockRepo, mockDiscovery, mockEngine, mockSanitizer, mockSampleDataGenerator, mockPromo, mockHealth);
    }

    [Fact]
    public async Task Index_ReturnsViewWithTemplates()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetAllIncludingInactiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Template>
            {
                new() { Id = 1, Name = "A", TemplateType = "Email" }
            });
        var controller = CreateController(mockRepo.Object);

        var result = await controller.Index(null, null);

        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task Index_IncludesInactiveTemplates_ForBulkActivateReachability()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetAllIncludingInactiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Template>
            {
                new() { Id = 1, Name = "A", TemplateType = "Email", IsActive = true },
                new() { Id = 2, Name = "B", TemplateType = "Email", IsActive = false }
            });
        var controller = CreateController(mockRepo.Object);

        var result = await controller.Index(null, null);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<TemplateListViewModel>().Subject;
        model.Templates.Should().Contain(t => t.Id == 2 && !t.IsActive);
        mockRepo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task CreateTemplateJson_ValidRequest_CreatesTemplateAndReturnsId()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.CreateAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template t, CancellationToken _) => { t.Id = 42; return t; });
        var controller = CreateController(mockRepo.Object);

        var result = await controller.CreateTemplateJson(new TemplateEditorViewModel
        {
            Name = "Welcome Email",
            TemplateType = "Email",
            Body = "<p>Hi {{ model.Name }}</p>"
        });

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(new { templateId = 42 });
        mockRepo.Verify(r => r.PublishVersionAsync(42, It.Is<TemplateVersion>(v =>
            v.VersionNumber == 1 && v.Body == "<p>Hi {{ model.Name }}</p>" && v.ChangeComment == "Initial version"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTemplateJson_EmptyBody_DoesNotPublishVersion()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.CreateAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template t, CancellationToken _) => { t.Id = 1; return t; });
        var controller = CreateController(mockRepo.Object);

        await controller.CreateTemplateJson(new TemplateEditorViewModel { Name = "A", TemplateType = "Email", Body = "" });

        mockRepo.Verify(r => r.PublishVersionAsync(It.IsAny<int>(), It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateTemplateJson_MissingName_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.CreateTemplateJson(new TemplateEditorViewModel { Name = "", TemplateType = "Email" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateTemplateJson_DuplicateName_ReturnsBadRequestWithMessage()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.CreateAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException());
        var controller = CreateController(mockRepo.Object);

        var result = await controller.CreateTemplateJson(new TemplateEditorViewModel { Name = "Dup", TemplateType = "Email" });

        result.Should().BeOfType<BadRequestObjectResult>();
        var bad = (BadRequestObjectResult)result;
        bad.Value.Should().BeEquivalentTo(new ErrorResult("VALIDATION_ERROR", "A template named 'Dup' already exists."));
    }

    [Fact]
    public async Task Edit_ExistingTemplate_PopulatesSampleDataFromEntity()
    {
        var mockRepo = new Mock<ITemplateRepository>();
        mockRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Template
        {
            Id = 1,
            Name = "Invoice",
            TemplateType = "Email",
            SampleData = "{\"Name\":\"Jane\"}"
        });
        var mockDiscovery = new Mock<ISqlViewDiscoveryService>();
        mockDiscovery.Setup(d => d.GetViewNamesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>());
        var controller = CreateController(mockRepo.Object, mockDiscovery.Object);

        var result = await controller.Edit(1);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<TemplateEditorViewModel>().Subject;
        model.SampleData.Should().Be("{\"Name\":\"Jane\"}");
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

    [Fact]
    public async Task SaveVersion_WithoutIsActive_DefaultsToActive()
    {
        var repo = new Mock<ITemplateRepository>();
        var template = new Template { Id = 1, Name = "A", TemplateType = "Email" };
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        TemplateVersion? captured = null;
        repo.Setup(r => r.PublishVersionAsync(1, It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, TemplateVersion v, CancellationToken _) => { captured = v; return v; });
        repo.Setup(r => r.GetNextVersionNumberAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        var controller = CreateController(repo.Object);

        var result = await controller.SaveVersion(1, new SaveVersionRequest("A", "Email", null, "<p>x</p>", null));

        result.Should().BeOfType<OkObjectResult>();
        captured!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SaveVersion_IsActiveFalse_CreatesDraftVersion()
    {
        var repo = new Mock<ITemplateRepository>();
        var template = new Template { Id = 1, Name = "A", TemplateType = "Email" };
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        TemplateVersion? captured = null;
        repo.Setup(r => r.PublishVersionAsync(1, It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, TemplateVersion v, CancellationToken _) => { captured = v; return v; });
        repo.Setup(r => r.GetNextVersionNumberAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        var controller = CreateController(repo.Object);

        var result = await controller.SaveVersion(1, new SaveVersionRequest("A", "Email", null, "<p>x</p>", null, IsActive: false));

        var ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(new { versionId = 0, versionNumber = 2, isActive = false });
        captured!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SaveVersion_SourceViewChanged_RebuildsSnapshot()
    {
        var repo = new Mock<ITemplateRepository>();
        var template = new Template { Id = 1, Name = "A", TemplateType = "Email", SourceView = "v_Old" };
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        repo.Setup(r => r.PublishVersionAsync(1, It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, TemplateVersion v, CancellationToken _) => v);
        repo.Setup(r => r.GetNextVersionNumberAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        var health = new Mock<ITemplateHealthService>();
        health.Setup(h => h.BuildSnapshotJsonAsync("v_New", It.IsAny<CancellationToken>())).ReturnsAsync("{\"columns\":[]}");
        var controller = CreateController(repo.Object, health: health.Object);

        var result = await controller.SaveVersion(1, new SaveVersionRequest("A", "Email", null, "<p>x</p>", null, SourceView: "v_New"));

        result.Should().BeOfType<OkObjectResult>();
        health.Verify(h => h.BuildSnapshotJsonAsync("v_New", It.IsAny<CancellationToken>()), Times.Once);
        template.SourceView.Should().Be("v_New");
        template.SourceViewSnapshot.Should().Be("{\"columns\":[]}");
    }

    [Fact]
    public async Task SaveVersion_SourceViewUnchanged_DoesNotRebuildSnapshot()
    {
        var repo = new Mock<ITemplateRepository>();
        var template = new Template { Id = 1, Name = "A", TemplateType = "Email", SourceView = "v_Same", SourceViewSnapshot = "{\"columns\":[\"X\"]}" };
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        repo.Setup(r => r.PublishVersionAsync(1, It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, TemplateVersion v, CancellationToken _) => v);
        repo.Setup(r => r.GetNextVersionNumberAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        var health = new Mock<ITemplateHealthService>();
        var controller = CreateController(repo.Object, health: health.Object);

        var result = await controller.SaveVersion(1, new SaveVersionRequest("A", "Email", null, "<p>x</p>", null, SourceView: "v_Same"));

        result.Should().BeOfType<OkObjectResult>();
        health.Verify(h => h.BuildSnapshotJsonAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        template.SourceViewSnapshot.Should().Be("{\"columns\":[\"X\"]}");
    }

    [Fact]
    public async Task RestoreVersion_InheritsSourceIsActive()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetVersionAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersion { Id = 5, VersionNumber = 1, Body = "<p>old</p>", IsActive = false });
        repo.Setup(r => r.GetNextVersionNumberAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        TemplateVersion? captured = null;
        repo.Setup(r => r.PublishVersionAsync(1, It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, TemplateVersion v, CancellationToken _) => { captured = v; return v; });
        var controller = CreateController(repo.Object);

        var result = await controller.RestoreVersion(1, 5, 1);

        result.Should().BeOfType<OkObjectResult>();
        captured!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Duplicate_InheritsLatestVersionIsActive()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new Template
        {
            Id = 3, Name = "Src", TemplateType = "Email",
            CurrentVersion = new TemplateVersion { Body = "<p>x</p>", IsActive = false }
        });
        repo.Setup(r => r.CreateAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template { Id = 9, Name = "Copy", TemplateType = "Email" });
        TemplateVersion? captured = null;
        repo.Setup(r => r.PublishVersionAsync(9, It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, TemplateVersion v, CancellationToken _) => { captured = v; return v; });
        var controller = CreateController(repo.Object);

        var result = await controller.Duplicate(3, new DuplicateRequest("Copy"));

        result.Should().BeOfType<OkObjectResult>();
        captured!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Edit_SetsLatestVersionIsActive()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Template
        {
            Id = 1, Name = "A", TemplateType = "Email",
            CurrentVersion = new TemplateVersion { VersionNumber = 2, Body = "<p>d</p>", IsActive = false }
        });
        var discovery = new Mock<ISqlViewDiscoveryService>();
        discovery.Setup(d => d.GetViewNamesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<string>());
        var controller = CreateController(repo.Object, discovery.Object);

        var result = await controller.Edit(1);

        var view = (ViewResult)result;
        var model = (TemplateEditorViewModel)view.Model!;
        model.LatestVersionIsActive.Should().BeFalse();
    }

    [Fact]
    public void SaveVersionRequest_JsonWithoutIsActive_BindsNull()
    {
        var json = """{"name":"A","templateType":"Email","body":"<p>x</p>"}""";
        var request = System.Text.Json.JsonSerializer.Deserialize<SaveVersionRequest>(
            json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        request.Should().NotBeNull();
        request!.IsActive.Should().BeNull();
    }

    [Fact]
    public async Task ExportTemplate_ReturnsFileAttachment()
    {
        var promo = new Mock<ITemplatePromotionService>();
        promo.Setup(p => p.BuildExportAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateExportDocument { Template = new TemplateExportTemplate { Name = "Inv", TemplateType = "Email" } });
        promo.Setup(p => p.SerializeExport(It.IsAny<TemplateExportDocument>())).Returns("{}");
        promo.Setup(p => p.SanitizeFileName("Inv")).Returns("Inv");
        var controller = CreateController(promo: promo);

        var result = await controller.ExportTemplate(1);

        result.Should().BeOfType<FileContentResult>();
        var file = (FileContentResult)result;
        file.ContentType.Should().Be("application/json");
    }

    [Fact]
    public async Task BulkDelete_ReturnsSucceededAndFailed()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Template { Id = 1, Name = "A", TemplateType = "Email" });
        repo.Setup(r => r.DeleteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync((Template?)null);
        var controller = CreateController(repo.Object);

        var result = await controller.BulkDelete(new BulkIdsRequest { Ids = new List<int> { 1, 2 } });

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        ok.Value.Should().BeEquivalentTo(new { succeeded = new[] { 1 }, failed = new[] { new { id = 2, reason = "NOT_FOUND" } } });
    }
}
