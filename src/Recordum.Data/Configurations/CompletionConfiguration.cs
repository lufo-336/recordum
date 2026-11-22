using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class CompletionConfiguration : IEntityTypeConfiguration<Completion>
{
    public void Configure(EntityTypeBuilder<Completion> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Notes).HasMaxLength(4000);
        builder.Property(c => c.LastEditedDeviceName).HasMaxLength(200);

        builder.HasOne(c => c.HabitNode)
            .WithMany(n => n.Completions)
            .HasForeignKey(c => c.HabitNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.HabitNodeId, c.CompletedAt });
        builder.HasIndex(c => new { c.HabitNodeId, c.CompletedAtMs });
    }
}
