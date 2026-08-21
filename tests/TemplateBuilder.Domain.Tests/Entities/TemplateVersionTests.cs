using FluentAssertions;
using TemplateBuilder.Domain.Entities;

namespace TemplateBuilder.Domain.Tests.Entities;

public class TemplateVersionTests
{
    [Fact]
    public void TemplateVersion_DefaultsToActive()
    {
        var v = new TemplateVersion();
        v.IsActive.Should().BeTrue();
    }
}
