using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;
using Recordum.Data.SeedData;

namespace Recordum.Data.Configurations;

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).IsRequired().HasMaxLength(100);
        // Value has no max length (TEXT column)

        builder.HasData(AppSettingSeed.All);
    }
}
