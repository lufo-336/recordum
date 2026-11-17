using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.SeedData;

namespace Recordum.Data.Configurations;

public sealed class ScaleTemplateConfiguration : IEntityTypeConfiguration<ScaleTemplate>
{
    public void Configure(EntityTypeBuilder<ScaleTemplate> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Code).IsRequired().HasMaxLength(64);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Ignore(s => s.LabelsJson); // i nomi stanno negli scaglioni (GIO-23); la proprietà sparisce col Task 4
        builder.Property(s => s.Precision).HasConversion<string>().HasMaxLength(16).HasDefaultValue(ScalePrecision.Integer);

        builder.ToTable(t => t.HasCheckConstraint("ck_scale_templates_precision", "precision IN ('Integer','Decimal')"));

        builder.HasIndex(s => s.Code).IsUnique();

        builder.HasData(ScaleTemplateSeed.All);
    }
}
