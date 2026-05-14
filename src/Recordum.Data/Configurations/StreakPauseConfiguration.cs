using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class StreakPauseConfiguration : IEntityTypeConfiguration<StreakPause>
{
    public void Configure(EntityTypeBuilder<StreakPause> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Reason).HasMaxLength(500);

        builder.HasOne(p => p.HabitNode)
            .WithMany(n => n.StreakPauses)
            .HasForeignKey(p => p.HabitNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.HabitNodeId, p.PauseFrom })
            .HasDatabaseName("ix_streak_pause_habit_node_id_pause_from");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_streak_pause_to_gte_from", "pause_to >= pause_from");
        });
    }
}
