using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TemplateBuilder.Domain.Entities;

namespace TemplateBuilder.Infrastructure.Data.Configurations;

public class SnippetConfiguration : IEntityTypeConfiguration<Snippet>
{
    public void Configure(EntityTypeBuilder<Snippet> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => s.Name).IsUnique();
        builder.Property(s => s.Description).HasMaxLength(300);
        builder.Property(s => s.Body).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(s => s.CreatedAt).HasColumnType("datetime2");
        builder.Property(s => s.UpdatedAt).HasColumnType("datetime2");
    }
}
