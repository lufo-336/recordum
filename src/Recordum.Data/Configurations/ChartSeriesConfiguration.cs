using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class ChartSeriesConfiguration : IEntityTypeConfiguration<ChartSeries>
{
    public void Configure(EntityTypeBuilder<ChartSeries> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Label).HasMaxLength(200);
        builder.Property(s => s.Color).HasMaxLength(7);

        builder.HasOne(s => s.Chart)
            .WithMany(c => c.Series)
            .HasForeignKey(s => s.ChartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.HabitNode)
            .WithMany()
            .HasForeignKey(s => s.HabitNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.MetricDefinition)
            .WithMany()
            .HasForeignKey(s => s.MetricDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.ChartId, s.SortOrder });
    }
}
