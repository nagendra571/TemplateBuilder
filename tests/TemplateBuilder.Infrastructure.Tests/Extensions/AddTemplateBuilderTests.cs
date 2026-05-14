using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TemplateBuilder.Core.Extensions;
using TemplateBuilder.Domain.Interfaces;

namespace TemplateBuilder.Infrastructure.Tests.Extensions;

public class AddTemplateBuilderTests
{
    [Fact]
    public void AddTemplateBuilder_ValidConnectionString_ResolvesITemplateEngine()
    {
        var services = new ServiceCollection();
        services.AddTemplateBuilder(o =>
            o.ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=TemplateBuilderSmoke;Trusted_Connection=True;");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var engine = scope.ServiceProvider.GetRequiredService<ITemplateEngine>();
        engine.Should().NotBeNull();
    }

    [Fact]
    public void AddTemplateBuilder_EmptyConnectionString_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddTemplateBuilder(o => o.ConnectionString = "");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionString*");
    }
}
