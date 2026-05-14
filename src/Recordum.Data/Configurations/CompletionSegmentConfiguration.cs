using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class CompletionSegmentConfiguration : IEntityTypeConfiguration<CompletionSegment>
{
    public void Configure(EntityTypeBuilder<CompletionSegment> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Note).HasMaxLength(1000);

        builder.HasOne(s => s.Completion)
            .WithMany(c => c.Segments)
            .HasForeignKey(s => s.CompletionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.SegmentDefinition)
            .WithMany()
            .HasForeignKey(s => s.SegmentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.CompletionId, s.SegmentIndex });
    }
}
