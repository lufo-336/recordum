using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).IsRequired().HasMaxLength(200);
        builder.Property(g => g.TargetValue).HasPrecision(18, 6);
        builder.Property(g => g.PeriodType).HasConversion<string>().HasMaxLength(16);

        builder.HasOne(g => g.HabitNode)
            .WithMany(n => n.Goals)
            .HasForeignKey(g => g.HabitNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.MetricDefinition)
            .WithMany()
            .HasForeignKey(g => g.MetricDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(g => g.HabitNodeId);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_goal_period_type",
                "period_type IN ('Fixed','Rolling','Open')");
            t.HasCheckConstraint("ck_goal_period_consistency",
                "(period_type = 'Fixed' AND period_start IS NOT NULL AND period_end IS NOT NULL) OR (period_type = 'Rolling' AND rolling_days IS NOT NULL) OR (period_type = 'Open')");
        });
    }
}
