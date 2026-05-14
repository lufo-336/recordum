using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class ChartConfiguration : IEntityTypeConfiguration<Chart>
{
    public void Configure(EntityTypeBuilder<Chart> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.ChartType).IsRequired().HasMaxLength(32);
        builder.Property(c => c.CreatedByPluginId).HasMaxLength(200);
        // ConfigJson has no max length (TEXT column)

        builder.HasIndex(c => c.CreatedByPluginId);
    }
}
