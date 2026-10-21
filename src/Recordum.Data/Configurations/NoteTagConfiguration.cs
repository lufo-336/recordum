using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class NoteTagConfiguration : IEntityTypeConfiguration<NoteTag>
{
    public void Configure(EntityTypeBuilder<NoteTag> builder)
    {
        builder.ToTable("note_tag", t => t.HasCheckConstraint("ck_note_tag_source", "source IN ('Inline','Property')"));

        builder.HasKey(nt => new { nt.NoteId, nt.TagId, nt.Source }).HasName("pk_note_tag");

        builder.Property(nt => nt.Source).HasConversion<string>().HasMaxLength(16);

        builder.HasOne<Note>()
            .WithMany()
            .HasForeignKey(nt => nt.NoteId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_note_tag_note_id");

        builder.HasOne<Tag>()
            .WithMany()
            .HasForeignKey(nt => nt.TagId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_note_tag_tag_id");

        builder.HasIndex(nt => nt.TagId).HasDatabaseName("ix_note_tag_tag_id");
    }
}
