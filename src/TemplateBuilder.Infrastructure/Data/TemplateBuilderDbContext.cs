using Microsoft.EntityFrameworkCore;
using TemplateBuilder.Domain.Entities;

namespace TemplateBuilder.Infrastructure.Data;

public class TemplateBuilderDbContext : DbContext
{
    public TemplateBuilderDbContext(DbContextOptions<TemplateBuilderDbContext> options) : base(options) { }

    public DbSet<Template> Templates => Set<Template>();
    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TemplateBuilderDbContext).Assembly);
    }

    /// <summary>
    /// Ensures RowVersion is populated before saving when using the InMemory provider
    /// (which does not generate ROWVERSION byte values). For SQL Server, the database
    /// engine overwrites this value with the actual ROWVERSION after the insert/update.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Template>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified
                && (entry.Entity.RowVersion == null || entry.Entity.RowVersion.Length == 0))
            {
                entry.Entity.RowVersion = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
