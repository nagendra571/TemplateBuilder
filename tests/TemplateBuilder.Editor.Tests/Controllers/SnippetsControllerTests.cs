using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;
using TemplateBuilder.Editor;
using TemplateBuilder.Editor.Controllers;
using TemplateBuilder.Editor.Models;

namespace TemplateBuilder.Editor.Tests.Controllers;

public class SnippetsControllerTests
{
    private static SnippetsController CreateController(
        ISnippetRepository? snippets = null,
        Mock<IAuditService>? audit = null,
        ActorResolverAccessor? actorResolver = null)
    {
        var mockSnippets = snippets ?? new Mock<ISnippetRepository>().Object;
        var mockAudit = audit?.Object ?? new Mock<IAuditService>().Object;
        var resolvedActorResolver = actorResolver ?? new ActorResolverAccessor(null);
        return new SnippetsController(mockSnippets, mockAudit, resolvedActorResolver);
    }

    [Fact]
    public async Task Create_ValidRequest_RecordsSnippetCreated()
    {
        var repo = new Mock<ISnippetRepository>();
        repo.Setup(r => r.CreateAsync(It.IsAny<Snippet>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Snippet s, CancellationToken _) => { s.Id = 3; return s; });
        var audit = new Mock<IAuditService>();
        var controller = CreateController(repo.Object, audit);

        var result = await controller.Create(new CreateSnippetRequest("Greeting", "desc", "<p>Hi</p>"));

        result.Should().BeOfType<OkObjectResult>();
        audit.Verify(a => a.RecordAsync("Snippet", 3, AuditActions.SnippetCreated, "anonymous",
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_InvalidRequest_DoesNotRecordAudit()
    {
        var audit = new Mock<IAuditService>();
        var controller = CreateController(audit: audit);

        var result = await controller.Create(new CreateSnippetRequest("", "desc", "<p>Hi</p>"));

        result.Should().BeOfType<BadRequestObjectResult>();
        audit.Verify(a => a.RecordAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ExistingSnippet_RecordsSnippetDeleted()
    {
        var repo = new Mock<ISnippetRepository>();
        repo.Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Snippet { Id = 4, Name = "Greeting", Body = "<p>Hi</p>" });
        var audit = new Mock<IAuditService>();
        var controller = CreateController(repo.Object, audit);

        var result = await controller.Delete(4);

        result.Should().BeOfType<NoContentResult>();
        audit.Verify(a => a.RecordAsync("Snippet", 4, AuditActions.SnippetDeleted, "anonymous",
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_NonExistentSnippet_DoesNotRecordAudit()
    {
        var repo = new Mock<ISnippetRepository>();
        repo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Snippet?)null);
        var audit = new Mock<IAuditService>();
        var controller = CreateController(repo.Object, audit);

        var result = await controller.Delete(99);

        result.Should().BeOfType<NotFoundObjectResult>();
        audit.Verify(a => a.RecordAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
