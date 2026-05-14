using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;
using Recordum.Data.SeedData;

namespace Recordum.Data.Configurations;

public sealed class HabitTemplateConfiguration : IEntityTypeConfiguration<HabitTemplate>
{
    public void Configure(EntityTypeBuilder<HabitTemplate> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Category).HasMaxLength(100);
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.CreatedByPluginId).HasMaxLength(200);
        // PayloadJson has no max length (TEXT column)

        builder.HasIndex(t => t.CreatedByPluginId);

        builder.HasData(HabitTemplateSeed.All);
    }
}
