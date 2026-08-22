using FluentAssertions;
using Moq;
using TemplateBuilder.Application.Services;
using TemplateBuilder.Domain.Entities;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Application.Tests.Services;

public class AuditServiceTests
{
    [Fact]
    public async Task RecordAsync_SetsOccurredAt_AndDelegates()
    {
        var repo = new Mock<IAuditRepository>();
        var svc = new AuditService(repo.Object);
        AuditLog? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback((AuditLog a, CancellationToken _) => captured = a);

        await svc.RecordAsync("Template", 7, AuditActions.Published, "bob", afterState: "{}", comment: "hi");

        captured.Should().NotBeNull();
        captured!.EntityType.Should().Be("Template");
        captured.EntityId.Should().Be(7);
        captured.Action.Should().Be("published");
        captured.Actor.Should().Be("bob");
        captured.AfterState.Should().Be("{}");
        captured.Comment.Should().Be("hi");
        captured.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
