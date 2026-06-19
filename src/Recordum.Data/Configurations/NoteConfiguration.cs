using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.HasKey(n => n.Id);
        // BodyMd has no max length (TEXT column)

        builder.Property(n => n.Category)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(NoteCategory.Veloce)
            .IsRequired();

        builder.HasOne(n => n.HabitNode)
            .WithMany(h => h.Notes)
            .HasForeignKey(n => n.HabitNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.HabitNodeId, n.CreatedAt });
        builder.HasIndex(n => new { n.HabitNodeId, n.TargetDate });
        // Soft FK to the paired Completion (managed by DiaryService); indexed for the Cronologia join.
        builder.HasIndex(n => n.CompletionId);

        builder.ToTable(t =>
            t.HasCheckConstraint("ck_note_category", "category IN ('Diario','Veloce')"));
    }
}
