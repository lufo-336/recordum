using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class HabitNodeTagConfiguration : IEntityTypeConfiguration<HabitNodeTag>
{
    public void Configure(EntityTypeBuilder<HabitNodeTag> builder)
    {
        builder.HasKey(ht => new { ht.HabitNodeId, ht.TagId });

        builder.HasOne(ht => ht.HabitNode)
            .WithMany(n => n.Tags)
            .HasForeignKey(ht => ht.HabitNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ht => ht.Tag)
            .WithMany(t => t.HabitNodeTags)
            .HasForeignKey(ht => ht.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
