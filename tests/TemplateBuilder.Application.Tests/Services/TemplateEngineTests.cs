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
        repo.Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>())).ReturnsAsync((Domain.Entities.Template?)null);
        var engine = CreateEngine(repo.Object);

        var act = async () => await engine.RenderAsync(999, new { });

        await act.Should().ThrowAsync<TemplateNotFoundException>();
    }

    [Fact]
    public async Task RenderAsync_ValidTemplate_FetchesLastActiveVersionBodyAndRenders()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.Template { Id = 1, Name = "A", IsActive = true });
        repo.Setup(r => r.GetLastActiveVersionAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.TemplateVersion { Id = 10, Body = "<p>{{ model.Title }}</p>" });
        repo.Setup(r => r.GetVersionBodyAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync("<p>{{ model.Title }}</p>");
        var engine = CreateEngine(repo.Object);

        var result = await engine.RenderAsync(1, new { Title = "Hi" });

        result.Should().Be("<p>Hi</p>");
    }

    [Fact]
    public async Task RenderAsync_CalledTwice_SecondCallUsesCache_NoSecondBodyFetch()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.Template { Id = 1, IsActive = true });
        repo.Setup(r => r.GetLastActiveVersionAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Entities.TemplateVersion { Id = 10, Body = "<p>{{ model.X }}</p>" });
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
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.Template { Id = 1, IsActive = true });
        var versionSequence = new Queue<Domain.Entities.TemplateVersion?>(new Domain.Entities.TemplateVersion?[]
        {
            new Domain.Entities.TemplateVersion { Id = 10 },
            new Domain.Entities.TemplateVersion { Id = 11 }
        });
        repo.Setup(r => r.GetLastActiveVersionAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => versionSequence.Dequeue());
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
        repo.Setup(r => r.GetLastActiveVersionAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Entities.TemplateVersion { Id = 20 });
        repo.Setup(r => r.GetVersionBodyAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync("<p>Welcome</p>");
        var engine = CreateEngine(repo.Object);

        var html = await engine.RenderByNameAsync("WelcomeEmail", new { });

        html.Should().Be("<p>Welcome</p>");
    }

    [Fact]
    public async Task RenderAsync_InactiveTemplate_ThrowsTemplateInactiveException()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.Template { Id = 7, IsActive = false });
        var engine = CreateEngine(repo.Object);

        var act = async () => await engine.RenderAsync(7, new { });

        await act.Should().ThrowAsync<TemplateInactiveException>();
    }

    [Fact]
    public async Task RenderAsync_NoActiveVersion_ThrowsNoActiveVersionException()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.Template { Id = 7, IsActive = true });
        repo.Setup(r => r.GetLastActiveVersionAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync((Domain.Entities.TemplateVersion?)null);
        var engine = CreateEngine(repo.Object);

        var act = async () => await engine.RenderAsync(7, new { });

        await act.Should().ThrowAsync<NoActiveVersionException>();
    }

    [Fact]
    public async Task RenderAsync_LatestVersionIsDraft_ServesOlderActiveBody()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.Template
        {
            Id = 1,
            IsActive = true,
            CurrentVersion = new Domain.Entities.TemplateVersion { Id = 12, VersionNumber = 3, Body = "draft", IsActive = false }
        });
        repo.Setup(r => r.GetLastActiveVersionAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Entities.TemplateVersion { Id = 11, VersionNumber = 2, Body = "<p>{{ model.Title }}</p>", IsActive = true });
        repo.Setup(r => r.GetVersionBodyAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync("<p>{{ model.Title }}</p>");
        var engine = CreateEngine(repo.Object);

        var result = await engine.RenderAsync(1, new { Title = "Active" });

        result.Should().Be("<p>Active</p>");
    }

    [Fact]
    public async Task RenderByNameAsync_InactiveTemplate_ThrowsTemplateInactiveException()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByNameAsync("Off", It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.Template { Id = 9, IsActive = false });
        var engine = CreateEngine(repo.Object);

        var act = async () => await engine.RenderByNameAsync("Off", new { });

        await act.Should().ThrowAsync<TemplateInactiveException>();
    }

    [Fact]
    public async Task DraftSave_DoesNotEvictCachedActiveBody()
    {
        var repo = new Mock<ITemplateRepository>();
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.Template { Id = 1, IsActive = true });
        repo.Setup(r => r.GetLastActiveVersionAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Entities.TemplateVersion { Id = 10, Body = "<p>{{ model.Title }}</p>", IsActive = true });
        repo.Setup(r => r.GetVersionBodyAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync("<p>{{ model.Title }}</p>");
        var engine = CreateEngine(repo.Object);

        await engine.RenderAsync(1, new { Title = "One" });   // warms the cache
        await engine.RenderAsync(1, new { Title = "Two" });   // same active version id — must hit the cache

        repo.Verify(r => r.GetVersionBodyAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActiveSave_RefetchesBody()
    {
        var repo = new Mock<ITemplateRepository>();
        var activeId = 10;
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.Template { Id = 1, IsActive = true });
        repo.Setup(r => r.GetLastActiveVersionAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Domain.Entities.TemplateVersion { Id = activeId, Body = "<p>{{ model.Title }}</p>", IsActive = true });
        repo.Setup(r => r.GetVersionBodyAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => $"<p>{{{{ model.Title }}}} {id}</p>");
        var engine = CreateEngine(repo.Object);

        await engine.RenderAsync(1, new { Title = "One" });   // caches version 10
        activeId = 11;                                         // an active save happened
        var result = await engine.RenderAsync(1, new { Title = "Two" });

        result.Should().Contain("11");
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
        repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Domain.Entities.Template { Id = 1, IsActive = true });
        repo.Setup(r => r.GetLastActiveVersionAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Domain.Entities.TemplateVersion { Id = 10 });
        repo.Setup(r => r.GetVersionBodyAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync("<p>hi</p>");
        var engine = CreateEngineNoCaching(repo.Object);

        await engine.RenderAsync(1, new { });
        await engine.RenderAsync(1, new { });

        repo.Verify(r => r.GetVersionBodyAsync(10, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
