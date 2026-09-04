using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TemplateBuilder.Domain.Entities;

namespace TemplateBuilder.Infrastructure.Data.Configurations;

public class TemplateVersionConfiguration : IEntityTypeConfiguration<TemplateVersion>
{
    public void Configure(EntityTypeBuilder<TemplateVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.HasIndex(v => new { v.TemplateId, v.VersionNumber }).IsUnique();
        builder.Property(v => v.Body).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(v => v.Subject).HasMaxLength(500);
        builder.Property(v => v.ChangeComment).HasMaxLength(500);
        builder.Property(v => v.CreatedAt).HasColumnType("datetime2");
        builder.Property(v => v.CreatedBy).HasMaxLength(100);
    }
}
