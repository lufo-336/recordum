using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.NotifyType).HasConversion<string>().HasMaxLength(32);

        builder.HasOne(n => n.HabitNode)
            .WithMany(h => h.Notifications)
            .HasForeignKey(n => n.HabitNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => n.HabitNodeId);
    }
}
