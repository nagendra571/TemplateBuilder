using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TemplateBuilder.Domain.Entities;

namespace TemplateBuilder.Infrastructure.Data.Configurations;

public class TemplateConfiguration : IEntityTypeConfiguration<Template>
{
    public void Configure(EntityTypeBuilder<Template> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique();
        builder.Property(t => t.TemplateType).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.ExternalKey).IsRequired();
        builder.HasIndex(t => t.ExternalKey).IsUnique();
        builder.Property(t => t.SourceView).HasMaxLength(200);
        builder.Property(t => t.SourceViewSnapshot).HasColumnType("nvarchar(max)");
        builder.Property(t => t.CreatedAt).HasColumnType("datetime2");
        builder.Property(t => t.UpdatedAt).HasColumnType("datetime2");
        builder.Property(t => t.RowVersion).IsRowVersion();

        builder.HasMany(t => t.Versions)
               .WithOne(v => v.Template)
               .HasForeignKey(v => v.TemplateId)
               .OnDelete(DeleteBehavior.NoAction); // DB constraint enforces; delete Versions before Template

        builder.HasOne(t => t.CurrentVersion)
               .WithMany()
               .HasForeignKey(t => t.CurrentVersionId)
               .OnDelete(DeleteBehavior.SetNull)
               .IsRequired(false);
    }
}
