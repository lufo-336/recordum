using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FilePath).IsRequired().HasMaxLength(500);
        builder.Property(a => a.MimeType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Caption).HasMaxLength(500);

        builder.HasOne(a => a.Completion)
            .WithMany(c => c.Attachments)
            .HasForeignKey(a => a.CompletionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Note)
            .WithMany(n => n.Attachments)
            .HasForeignKey(a => a.NoteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.CompletionId);
        builder.HasIndex(a => a.NoteId);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_attachment_exactly_one_parent",
                "(completion_id IS NOT NULL OR note_id IS NOT NULL) AND NOT (completion_id IS NOT NULL AND note_id IS NOT NULL)");
        });
    }
}
