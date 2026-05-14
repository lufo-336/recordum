using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class MetricDefinitionConfiguration : IEntityTypeConfiguration<MetricDefinition>
{
    public void Configure(EntityTypeBuilder<MetricDefinition> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(m => m.LabelsJson).HasMaxLength(2000);

        builder.HasOne(m => m.HabitNode)
            .WithMany(n => n.Metrics)
            .HasForeignKey(m => m.HabitNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.UnitOfMeasure)
            .WithMany()
            .HasForeignKey(m => m.UnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.ScaleTemplate)
            .WithMany(s => s.Metrics)
            .HasForeignKey(m => m.ScaleTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.HabitNodeId);
    }
}
