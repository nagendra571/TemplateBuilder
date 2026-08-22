using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TemplateBuilder.Domain.Entities;

namespace TemplateBuilder.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.EntityType).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(40).IsRequired();
        builder.Property(a => a.Actor).HasMaxLength(200).IsRequired();
        builder.Property(a => a.BeforeState).HasMaxLength(4000);
        builder.Property(a => a.AfterState).HasMaxLength(4000);
        builder.Property(a => a.Comment).HasMaxLength(1000);
        builder.Property(a => a.OccurredAt).HasColumnType("datetime2");
        builder.HasIndex(a => new { a.EntityType, a.EntityId, a.OccurredAt });
        builder.HasIndex(a => a.OccurredAt);
    }
}
