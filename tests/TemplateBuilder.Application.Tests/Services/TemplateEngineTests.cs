using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using TemplateBuilder.Application.Options;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Exceptions;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Application.Tests.Services;

public class TemplateEngineTests
{
    private static TemplateEngine CreateEngine(ITemplateRepository repo, bool allowTopLevelModelAccess = false)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Microsoft.Extensions.Options.Options.Create(new TemplateBuilderOptions
        {
            EnableCaching = true,
            CacheDurationMinutes = 30,
            AllowTopLevelModelAccess = allowTopLevelModelAccess
        });
        return new TemplateEngine(repo, cache, options);
    }

    [Fact]
    public async Task RenderBodyAsync_ScalarField_ReplacesToken()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderBodyAsync(
            "<p>Dear {{ model.Name }},</p>",
            new { Name = "John" });

        html.Should().Be("<p>Dear John,</p>");
    }

    [Fact]
    public async Task RenderBodyAsync_LoopBlock_RendersEachItem()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderBodyAsync(
            "{{ for item in model.Items }}<li>{{ item.Name }}</li>{{ end }}",
            new { Items = new[] { new { Name = "Alpha" }, new { Name = "Beta" } } });

        html.Should().Contain("<li>Alpha</li>");
        html.Should().Contain("<li>Beta</li>");
    }

    [Fact]
    public async Task RenderBodyAsync_InvalidScriban_ThrowsTemplateRenderException()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var act = async () => await engine.RenderBodyAsync("{{ invalid syntax @@@ }}", new { });

        await act.Should().ThrowAsync<TemplateRenderException>();
    }

    [Fact]
    public async Task RenderAsync_UnknownTemplateId_ThrowsTemplateNotFoundException()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetCurrentVersionIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        var engine = CreateEngine(repo.Object);

        var act = async () => await engine.RenderAsync(999, new { });

        await act.Should().ThrowAsync<TemplateNotFoundException>();
    }

    [Fact]
    public async Task RenderAsync_ValidTemplate_FetchesBodyAndRenders()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetCurrentVersionIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(10);
        repo.Setup(r => r.GetVersionBodyAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync("<p>{{ model.Title }}</p>");
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderAsync(1, new { Title = "Hello World" });

        html.Should().Be("<p>Hello World</p>");
    }

    [Fact]
    public async Task RenderAsync_CalledTwice_SecondCallUsesCache_NoSecondBodyFetch()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetCurrentVersionIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(10);
        repo.Setup(r => r.GetVersionBodyAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync("<p>{{ model.X }}</p>");
        var engine = CreateEngine(repo.Object);

        await engine.RenderAsync(1, new { X = "first" });
        await engine.RenderAsync(1, new { X = "second" });

        repo.Verify(r => r.GetVersionBodyAsync(10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RenderAsync_VersionChanged_RefetchesBody()
    {
        var repo = new Mock<ITemplateRepository>();
        var versionIdSequence = new Queue<int?>(new int?[] { 10, 11 });
        repo.Setup(r => r.GetCurrentVersionIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => versionIdSequence.Dequeue());
        repo.Setup(r => r.GetVersionBodyAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync("<p>v1</p>");
        repo.Setup(r => r.GetVersionBodyAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync("<p>v2</p>");
        var engine = CreateEngine(repo.Object);

        var html1 = await engine.RenderAsync(1, new { });
        var html2 = await engine.RenderAsync(1, new { });

        html1.Should().Be("<p>v1</p>");
        html2.Should().Be("<p>v2</p>");
        repo.Verify(r => r.GetVersionBodyAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RenderByNameAsync_ResolvesTemplateByName()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByNameAsync("WelcomeEmail", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Entities.Template
            {
                Id = 5,
                Name = "WelcomeEmail",
                TemplateType = "Email",
                IsActive = true,
                CurrentVersionId = 20
            });
        repo.Setup(r => r.GetCurrentVersionIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(20);
        repo.Setup(r => r.GetVersionBodyAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync("<p>Welcome</p>");
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderByNameAsync("WelcomeEmail", new { });

        html.Should().Be("<p>Welcome</p>");
    }

    // FIXED: was referencing non-existent GetByIdWithActiveCheckAsync
    [Fact]
    public async Task RenderAsync_InactiveTemplate_ThrowsTemplateNotFoundException()
    {
        var repo = new Mock<ITemplateRepository>();
        // GetCurrentVersionIdAsync returns null for inactive templates (filtered by IsActive in repo)
        repo.Setup(r => r.GetCurrentVersionIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        var engine = CreateEngine(repo.Object);

        var act = async () => await engine.RenderAsync(42, new { });

        await act.Should().ThrowAsync<TemplateNotFoundException>();
    }

    [Fact]
    public async Task RenderBodyAsync_NullProperty_RendersEmptyString()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderBodyAsync("<p>{{ model.Name }}</p>", new { Name = (string?)null });

        html.Should().Be("<p></p>");
    }

    [Fact]
    public async Task RenderBodyAsync_MissingProperty_RendersEmptyString()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderBodyAsync("<p>{{ model.Missing }}</p>", new { });

        html.Should().Be("<p></p>");
    }

    [Fact]
    public async Task RenderBodyAsync_CaseInsensitivePropertyName_Renders()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderBodyAsync("<p>{{ model.CUSTOMERNAME }}</p>", new { CustomerName = "Alice" });

        html.Should().Be("<p>Alice</p>");
    }

    [Fact]
    public async Task RenderBodyAsync_ScriptTagInModel_EmittedVerbatim()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderBodyAsync("<p>{{ model.Val }}</p>", new { Val = "<script>alert(1)</script>" });

        html.Should().Contain("<script>alert(1)</script>");
    }

    [Fact]
    public async Task RenderByNameAsync_InactiveTemplate_ThrowsTemplateNotFoundException()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByNameAsync("Inactive", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Entities.Template
            {
                Id = 7,
                Name = "Inactive",
                TemplateType = "Email",
                IsActive = false
            });
        var engine = CreateEngine(repo.Object);

        var act = async () => await engine.RenderByNameAsync("Inactive", new { });

        await act.Should().ThrowAsync<TemplateNotFoundException>();
    }

    // F6: dual model access (top-level `X` and `model.X`).

    [Fact]
    public async Task RenderBodyAsync_TopLevelAccessDisabled_TopLevelTokenRendersEmpty()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object); // default: AllowTopLevelModelAccess = false

        var html = await engine.RenderBodyAsync("<p>{{ Name }}</p>", new { Name = "John" });

        html.Should().Be("<p></p>");
    }

    [Fact]
    public async Task RenderBodyAsync_TopLevelAccessEnabled_BothStylesRender()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object, allowTopLevelModelAccess: true);

        var html = await engine.RenderBodyAsync("<p>{{ Name }} / {{ model.Name }}</p>", new { Name = "John" });

        html.Should().Be("<p>John / John</p>");
    }

    [Fact]
    public async Task RenderBodyAsync_TopLevelAccessEnabled_CaseInsensitive()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object, allowTopLevelModelAccess: true);

        var html = await engine.RenderBodyAsync("<p>{{ name }}</p>", new { Name = "John" });

        html.Should().Be("<p>John</p>");
    }

    [Fact]
    public async Task RenderBodyAsync_TopLevelAccessEnabled_ModelKeyNamedModel_NotClobbered()
    {
        var repo = new Mock<ITemplateRepository>();
        var engine = CreateEngine(repo.Object, allowTopLevelModelAccess: true);

        // The model has its own member literally named "model" — the user's value must win
        // over the built-in `model.*` wrapper when the template accesses {{ model }}.
        var html = await engine.RenderBodyAsync("<p>{{ model }}</p>", new { model = "user-value" });

        html.Should().Be("<p>user-value</p>");
    }

    private static TemplateEngine CreateEngineNoCaching(ITemplateRepository repo)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Microsoft.Extensions.Options.Options.Create(new TemplateBuilderOptions { EnableCaching = false });
        return new TemplateEngine(repo, cache, options);
    }

    [Fact]
    public async Task RenderAsync_CachingDisabled_FetchesBodyOnEveryCall()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetCurrentVersionIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(10);
        repo.Setup(r => r.GetVersionBodyAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync("<p>hi</p>");
        var engine = CreateEngineNoCaching(repo.Object);

        await engine.RenderAsync(1, new { });
        await engine.RenderAsync(1, new { });

        repo.Verify(r => r.GetVersionBodyAsync(10, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
