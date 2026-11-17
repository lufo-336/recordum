using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;
using Recordum.Data.SeedData;

namespace Recordum.Data.Configurations;

public sealed class ScaleAnchorConfiguration : IEntityTypeConfiguration<ScaleAnchor>
{
    public void Configure(EntityTypeBuilder<ScaleAnchor> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Label).HasMaxLength(100);

        builder.HasOne(a => a.ScaleTemplate)
            .WithMany(s => s.Anchors)
            .HasForeignKey(a => a.ScaleTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Tag)
            .WithMany()
            .HasForeignKey(a => a.TagId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => new { a.ScaleTemplateId, a.Value }).IsUnique().HasDatabaseName("ix_scale_anchors_scale_value");
        builder.HasIndex(a => a.TagId);

        builder.HasData(ScaleTemplateSeed.Anchors);
    }
}
