using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TemplateBuilder.Editor;
using Xunit;

namespace TemplateBuilder.Editor.Tests;

// Regression tests for options.ApplyMigrations (DBA-managed database support): when disabled,
// the package must never register the migration hosted service, so it never attempts DDL at
// startup — the app's SQL login can be DML-only, with the schema provisioned out-of-band via
// the shipped Scripts/TemplateBuilder.schema.<version>.sql script.
public class ApplyMigrationsOptionTests
{
    private static bool HasMigrationHostedService(IServiceCollection services) =>
        services.Any(d => d.ServiceType == typeof(IHostedService)
            && d.ImplementationType == typeof(MigrationHostedService));

    [Fact]
    public void ApplyMigrations_true_by_default_registers_the_migration_hosted_service()
    {
        var services = new ServiceCollection();
        services.AddTemplateBuilderEditor(o => o.ConnectionString = "Server=.;Database=x;Trusted_Connection=True;");

        HasMigrationHostedService(services).Should().BeTrue();
    }

    [Fact]
    public void ApplyMigrations_false_never_registers_the_migration_hosted_service()
    {
        var services = new ServiceCollection();
        services.AddTemplateBuilderEditor(o =>
        {
            o.ConnectionString = "Server=.;Database=x;Trusted_Connection=True;";
            o.ApplyMigrations = false;
        });

        HasMigrationHostedService(services).Should().BeFalse();
    }
}
