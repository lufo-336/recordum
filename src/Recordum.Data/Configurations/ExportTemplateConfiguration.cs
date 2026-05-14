using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class ExportTemplateConfiguration : IEntityTypeConfiguration<ExportTemplate>
{
    public void Configure(EntityTypeBuilder<ExportTemplate> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.CreatedByPluginId).HasMaxLength(200);
        // LayoutJson has no max length (TEXT column)

        builder.HasIndex(t => t.CreatedByPluginId);
    }
}
