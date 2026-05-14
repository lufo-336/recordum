using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class SegmentDefinitionConfiguration : IEntityTypeConfiguration<SegmentDefinition>
{
    public void Configure(EntityTypeBuilder<SegmentDefinition> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Color).HasMaxLength(7);

        builder.HasOne(s => s.HabitNode)
            .WithMany(n => n.Segments)
            .HasForeignKey(s => s.HabitNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.HabitNodeId);
    }
}
