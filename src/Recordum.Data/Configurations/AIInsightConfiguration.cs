using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recordum.Core.Models;

namespace Recordum.Data.Configurations;

public sealed class AIInsightConfiguration : IEntityTypeConfiguration<AIInsight>
{
    public void Configure(EntityTypeBuilder<AIInsight> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.InsightType).IsRequired().HasMaxLength(100);
        // BodyMd and PayloadJson have no max length (TEXT columns)

        builder.HasOne(a => a.HabitNode)
            .WithMany()
            .HasForeignKey(a => a.HabitNodeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => new { a.HabitNodeId, a.ValidUntil });

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_ai_insight_user_rating",
                "user_rating IS NULL OR (user_rating BETWEEN 1 AND 5)");
        });
    }
}
