using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class AbstinencePeriodConfiguration : IEntityTypeConfiguration<AbstinencePeriod>
{
    public void Configure(EntityTypeBuilder<AbstinencePeriod> builder)
    {
        builder.ToTable("abstinence_period");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StartedAtMs).IsRequired();
        builder.Property(x => x.EndedAtMs);
        builder.Property(x => x.CreatedAt).HasColumnType("TEXT").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnType("TEXT").IsRequired();
        builder.Property(x => x.UpdatedAtMs).IsRequired();

        builder.HasOne(x => x.HabitNode)
               .WithMany()
               .HasForeignKey(x => x.HabitNodeId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.HabitNodeId, x.StartedAtMs })
               .HasDatabaseName("ix_abstinence_period_habit_started");
    }
}
