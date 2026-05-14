using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class CompletionValueConfiguration : IEntityTypeConfiguration<CompletionValue>
{
    public void Configure(EntityTypeBuilder<CompletionValue> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.ValueText).HasMaxLength(2000);
        builder.Property(v => v.ValueDecimal).HasPrecision(18, 6);

        builder.HasOne(v => v.Completion)
            .WithMany(c => c.Values)
            .HasForeignKey(v => v.CompletionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.MetricDefinition)
            .WithMany()
            .HasForeignKey(v => v.MetricDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.CompletionSegment)
            .WithMany(s => s.Values)
            .HasForeignKey(v => v.CompletionSegmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(v => v.CompletionId);
        builder.HasIndex(v => v.MetricDefinitionId);
    }
}
