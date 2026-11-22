using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Data.Models;

namespace Recordum.Data.Configurations;

public sealed class SyncTombstoneConfiguration : IEntityTypeConfiguration<SyncTombstone>
{
    public void Configure(EntityTypeBuilder<SyncTombstone> builder)
    {
        builder.ToTable("sync_tombstone");
        builder.HasKey(t => new { t.Entity, t.EntityId });
        builder.Property(t => t.Entity).IsRequired().HasMaxLength(64);
        builder.Property(t => t.EntityId).IsRequired();
        builder.Property(t => t.DeviceId).HasMaxLength(100);
    }
}
