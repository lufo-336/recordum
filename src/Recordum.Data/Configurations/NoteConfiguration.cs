using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.HasKey(n => n.Id);
        // BodyMd has no max length (TEXT column)

        builder.HasOne(n => n.HabitNode)
            .WithMany(h => h.Notes)
            .HasForeignKey(n => n.HabitNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.HabitNodeId, n.CreatedAt });
    }
}
