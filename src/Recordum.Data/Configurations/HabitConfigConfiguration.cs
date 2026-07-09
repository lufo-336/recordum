using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class HabitConfigConfiguration : IEntityTypeConfiguration<HabitConfig>
{
    public void Configure(EntityTypeBuilder<HabitConfig> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ScheduleDaysJson).HasMaxLength(200);
        builder.Property(c => c.ScheduleMonthlyDaysJson).HasMaxLength(200);
        builder.Property(c => c.NoteMarkerColor).HasMaxLength(7);

        builder.Property(c => c.ScheduleType).HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.StreakMode).HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.StreakRecovery).HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.TimerMode).HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.AiDataSharing).HasConversion<string>().HasMaxLength(16);

        builder.Property(c => c.Repeatable).HasDefaultValue(false).IsRequired();
        builder.Property(c => c.SeriesLabelsJson).HasMaxLength(1000);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_habit_config_repeatable", "repeatable IN (0,1)"));

        builder.HasOne(c => c.HabitNode)
            .WithOne(n => n.Config)
            .HasForeignKey<HabitConfig>(c => c.HabitNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.HabitNodeId).IsUnique();
    }
}
