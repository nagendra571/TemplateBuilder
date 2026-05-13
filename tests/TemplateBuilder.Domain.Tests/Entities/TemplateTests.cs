using FluentAssertions;
using TemplateBuilder.Domain.Entities;

namespace TemplateBuilder.Domain.Tests.Entities;

public class TemplateTests
{
    [Fact]
    public void Template_NewInstance_HasCorrectDefaults()
    {
        var template = new Template { Name = "Test", TemplateType = "Email" };

        template.IsActive.Should().BeTrue();
        template.CurrentVersionId.Should().BeNull();
        template.Versions.Should().BeEmpty();
    }

    [Fact]
    public void TemplateVersion_NewInstance_HasBody()
    {
        var version = new TemplateVersion
        {
            TemplateId = 1,
            VersionNumber = 1,
            Body = "<p>Hello {{ model.Name }}</p>"
        };

        version.Body.Should().Contain("{{ model.Name }}");
        version.ChangeComment.Should().BeNull();
        version.CreatedBy.Should().BeNull();
    }
}
