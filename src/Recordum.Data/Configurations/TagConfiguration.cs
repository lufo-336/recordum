using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(64).UseCollation("NOCASE");
        builder.Property(t => t.Color).HasMaxLength(7);

        builder.HasIndex(t => t.Name).IsUnique().HasDatabaseName("ix_tag_name");
    }
}
