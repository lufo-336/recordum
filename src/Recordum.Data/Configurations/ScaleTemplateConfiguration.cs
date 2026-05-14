using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class ScaleTemplateConfiguration : IEntityTypeConfiguration<ScaleTemplate>
{
    public void Configure(EntityTypeBuilder<ScaleTemplate> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Code).IsRequired().HasMaxLength(64);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.LabelsJson).IsRequired().HasMaxLength(2000);

        builder.HasIndex(s => s.Code).IsUnique();
    }
}
