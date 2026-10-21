using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Data.Models;

namespace Recordum.Data.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachment", t => t.HasCheckConstraint("ck_attachment_size", "file_size_bytes >= 0"));

        builder.HasKey(a => a.Id).HasName("pk_attachment");

        builder.Property(a => a.Id).HasColumnName("id").HasColumnType("TEXT");
        builder.Property(a => a.NoteId).HasColumnName("note_id").HasColumnType("TEXT");
        builder.Property(a => a.Filename).HasColumnName("filename").HasColumnType("TEXT");
        builder.Property(a => a.MimeType).HasColumnName("mime_type").HasColumnType("TEXT");
        builder.Property(a => a.FileSizeBytes).HasColumnName("file_size_bytes").HasColumnType("INTEGER");
        builder.Property(a => a.Content).HasColumnName("content").HasColumnType("BLOB");
        builder.Property(a => a.Thumbnail).HasColumnName("thumbnail").HasColumnType("BLOB");
        builder.Property(a => a.IsMissing).HasColumnName("is_missing").HasColumnType("INTEGER").HasDefaultValue(false);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").HasColumnType("TEXT");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at").HasColumnType("TEXT");
        builder.Property(a => a.UpdatedAtMs).HasColumnName("updated_at_ms").HasColumnType("INTEGER");

        builder.HasOne(a => a.Note)
            .WithMany()
            .HasForeignKey(a => a.NoteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_attachment_note_id");

        builder.HasIndex(a => a.NoteId).HasDatabaseName("ix_attachment_note_id");
    }
}
