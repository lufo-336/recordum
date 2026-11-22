using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Data.Models;

namespace Recordum.Data.Configurations;

public sealed class SyncBaseConfiguration : IEntityTypeConfiguration<SyncBase>
{
    public void Configure(EntityTypeBuilder<SyncBase> builder)
    {
        builder.ToTable("sync_base");
        builder.HasKey(b => new { b.PeerDeviceId, b.Entity, b.EntityId });
        builder.Property(b => b.PeerDeviceId).IsRequired().HasMaxLength(100);
        builder.Property(b => b.Entity).IsRequired().HasMaxLength(64);
        builder.Property(b => b.EntityId).IsRequired();
        builder.Property(b => b.RowJson).IsRequired();
    }
}
