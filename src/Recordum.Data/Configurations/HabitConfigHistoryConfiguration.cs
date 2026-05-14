using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class HabitConfigHistoryConfiguration : IEntityTypeConfiguration<HabitConfigHistory>
{
    public void Configure(EntityTypeBuilder<HabitConfigHistory> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.ScheduleType).HasConversion<string>().HasMaxLength(16);
        builder.Property(h => h.StreakMode).HasConversion<string>().HasMaxLength(16);
        builder.Property(h => h.StreakRecovery).HasConversion<string>().HasMaxLength(16);
        builder.Property(h => h.TimerMode).HasConversion<string>().HasMaxLength(16);

        builder.HasOne(h => h.HabitNode)
            .WithMany(n => n.ConfigHistory)
            .HasForeignKey(h => h.HabitNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.HabitNodeId, h.ValidFrom });
    }
}
