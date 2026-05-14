using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Format).HasConversion<string>().HasMaxLength(32);
        builder.Property(j => j.FilePath).HasMaxLength(500);
        // FilterJson has no max length (TEXT column)

        builder.HasOne(j => j.ExportTemplate)
            .WithMany(t => t.Jobs)
            .HasForeignKey(j => j.ExportTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(j => j.CreatedAt).IsDescending(true);
    }
}
