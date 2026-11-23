using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Data.Models;

namespace Recordum.Data.Configurations;

public sealed class SyncFieldMsConfiguration : IEntityTypeConfiguration<SyncFieldMs>
{
    public void Configure(EntityTypeBuilder<SyncFieldMs> builder)
    {
        builder.ToTable("sync_field_ms");
        builder.HasKey(f => new { f.Entity, f.EntityId, f.Field });
        builder.Property(f => f.Entity).IsRequired().HasMaxLength(64);
        builder.Property(f => f.EntityId).IsRequired();
        builder.Property(f => f.Field).IsRequired().HasMaxLength(64);
        builder.Property(f => f.Ms).IsRequired();
    }
}
