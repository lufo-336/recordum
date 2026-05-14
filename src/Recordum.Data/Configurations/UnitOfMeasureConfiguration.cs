using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;
using Recordum.Data.SeedData;

namespace Recordum.Data.Configurations;

public sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Code).IsRequired().HasMaxLength(32);
        builder.Property(u => u.Name).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Symbol).HasMaxLength(16);

        builder.HasIndex(u => u.Code).IsUnique();

        builder.HasData(UnitOfMeasureSeed.All);
    }
}
