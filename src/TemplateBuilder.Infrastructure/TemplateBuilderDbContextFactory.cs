using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TemplateBuilder.Infrastructure.Data;

namespace TemplateBuilder.Infrastructure;

/// <summary>
/// Used by EF Core CLI tools (dotnet ef migrations) to instantiate the DbContext
/// without needing the Web startup project.
/// </summary>
public class TemplateBuilderDbContextFactory : IDesignTimeDbContextFactory<TemplateBuilderDbContext>
{
    public TemplateBuilderDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TemplateBuilderDbContext>()
            .UseSqlServer("Server=localhost\\SQLEXPRESS;Database=TemplateBuilder;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new TemplateBuilderDbContext(options);
    }
}
