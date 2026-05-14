using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class SyncMetaConfiguration : IEntityTypeConfiguration<SyncMeta>
{
    public void Configure(EntityTypeBuilder<SyncMeta> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.DeviceId).IsRequired().HasMaxLength(100);
        builder.Property(s => s.DeviceName).HasMaxLength(200);

        builder.HasIndex(s => s.DeviceId).IsUnique();
    }
}
