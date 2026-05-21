using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

internal sealed class ActiveTimerConfiguration : IEntityTypeConfiguration<ActiveTimer>
{
    public void Configure(EntityTypeBuilder<ActiveTimer> b)
    {
        b.ToTable("active_timer");
        b.HasKey(t => t.Id);

        b.Property(t => t.TimerMode)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        b.Property(t => t.StartedAtMs).IsRequired();
        b.Property(t => t.AccumulatedPausedMs).IsRequired().HasDefaultValue(0L);

        b.HasOne(t => t.HabitNode)
            .WithMany()
            .HasForeignKey(t => t.HabitNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(t => t.HabitNodeId).IsUnique().HasDatabaseName("ux_active_timer_habit_node_id");

        b.ToTable(tb => tb.HasCheckConstraint(
            "ck_active_timer_mode",
            "timer_mode IN ('Stopwatch','Countdown','Primary','Session')"));
    }
}
