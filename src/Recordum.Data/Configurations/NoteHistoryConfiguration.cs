using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class NoteHistoryConfiguration : IEntityTypeConfiguration<NoteHistory>
{
    public void Configure(EntityTypeBuilder<NoteHistory> builder)
    {
        builder.HasKey(h => h.Id);
        // BodyMd has no max length (TEXT column)

        builder.HasOne(h => h.Note)
            .WithMany(n => n.History)
            .HasForeignKey(h => h.NoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => new { h.NoteId, h.CreatedAt });
    }
}
