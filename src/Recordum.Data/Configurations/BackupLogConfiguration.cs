using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class BackupLogConfiguration : IEntityTypeConfiguration<BackupLog>
{
    public void Configure(EntityTypeBuilder<BackupLog> builder)
    {
        builder.ToTable("backup_log", t => t.HasCheckConstraint("ck_backup_log_kind", "kind IN ('Manual','Auto','PreRestore')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedAt).HasColumnType("TEXT").IsRequired();
        builder.Property(x => x.Kind).HasColumnType("TEXT").IsRequired();
        builder.Property(x => x.Path).HasColumnType("TEXT").IsRequired();
        builder.Property(x => x.SizeBytes).IsRequired();
        builder.Property(x => x.SchemaVersion).HasColumnType("TEXT").IsRequired();
        builder.Property(x => x.AppVersion).HasColumnType("TEXT").IsRequired();
        builder.Property(x => x.Notes).HasColumnType("TEXT");
        builder.HasIndex(x => x.CreatedAt).IsDescending().HasDatabaseName("ix_backup_log_created_at");
    }
}
