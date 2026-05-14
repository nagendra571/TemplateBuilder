using FluentAssertions;
using TemplateBuilder.Application.Options;
using TemplateBuilder.Application.Services;

namespace TemplateBuilder.Application.Tests.Services;

public class SqlViewDiscoveryServiceTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("vw_", "vw[_]")]
    [InlineData("50%", "50[%]")]
    [InlineData("[special]", "[[]special]")]
    [InlineData("a[%]_b", "a[[][%]][_]b")]
    public void EscapeLikePattern_EscapesMetacharacters(string input, string expected)
    {
        var result = SqlViewDiscoveryService.EscapeLikePattern(input);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task GetViewNamesAsync_WhenAllowlistSet_ReturnAllowlistWithoutDbCall()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new TemplateBuilderOptions
        {
            ViewAllowlist = new[] { "vw_Foo", "vw_Bar" }
        });
        var svc = new SqlViewDiscoveryService("Server=.;Database=Fake", options);

        var result = await svc.GetViewNamesAsync();

        result.Should().BeEquivalentTo(new[] { "vw_Foo", "vw_Bar" });
    }
}
