using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class HabitNodeConfiguration : IEntityTypeConfiguration<HabitNode>
{
    public void Configure(EntityTypeBuilder<HabitNode> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Name).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Color).IsRequired().HasMaxLength(7);
        builder.Property(n => n.Description).HasMaxLength(2000);
        builder.Property(n => n.Icon).HasMaxLength(64);
        builder.Property(n => n.CreatedByPluginId).HasMaxLength(200);

        builder.Property(n => n.NodeType).HasConversion<string>().HasMaxLength(16);
        builder.Property(n => n.HabitType).HasConversion<string>().HasMaxLength(16);
        builder.Property(n => n.AiDataSharing).HasConversion<string>().HasMaxLength(16);

        builder.Property(n => n.PinScope)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(PinScope.None)
            .IsRequired();

        builder.HasOne(n => n.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(n => n.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => n.ParentId);
        builder.HasIndex(n => n.NodeType);
        builder.HasIndex(n => n.DeletedAt);
        builder.HasIndex(n => n.CreatedByPluginId);
        builder.HasIndex(n => n.PinScope);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_habit_node_node_type",
                "node_type IN ('Habit','Group')");
            t.HasCheckConstraint("ck_habit_node_habit_type",
                "habit_type IS NULL OR habit_type IN ('Action','Counter','Resistance','Journal','State')");
            t.HasCheckConstraint("ck_habit_node_ai_data_sharing",
                "ai_data_sharing IN ('None','LocalOnly','ApiAllowed')");
            t.HasCheckConstraint("ck_habit_node_group_has_no_habit_type",
                "(node_type = 'Habit' AND habit_type IS NOT NULL) OR (node_type = 'Group' AND habit_type IS NULL)");
            t.HasCheckConstraint("ck_habit_node_pin_scope",
                "pin_scope IN ('None','Local','Global')");
        });

        builder.HasQueryFilter(n => n.DeletedAt == null);
    }
}
