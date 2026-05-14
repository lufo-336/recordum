using Recordum.Core.Models;

namespace Recordum.Data.SeedData;

/// <summary>
/// Official seed data for ScaleTemplate. GUIDs and LabelsJson are compile-time constants required by EF Core HasData.
/// </summary>
public static class ScaleTemplateSeed
{
    private static readonly DateTime SeedDate = new(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc);

    // Pre-computed JSON label arrays — must be string literals for HasData determinism.
    private const string BristolLabels =
        "[\"Hard pellets\",\"Lumpy sausage\",\"Cracked sausage\",\"Smooth sausage\",\"Soft blobs\",\"Mushy\",\"Liquid\"]";

    private const string MayoLabels =
        "[\"No bleeding\",\"Streaks on tissue\",\"Visible blood with stool\",\"Bleeding without stool\"]";

    private const string NrsPainLabels =
        "[\"No pain\",\"1\",\"2\",\"3\",\"4\",\"5\",\"6\",\"7\",\"8\",\"9\",\"Worst pain\"]";

    private const string LikertLabels =
        "[\"Strongly disagree\",\"Disagree\",\"Neutral\",\"Agree\",\"Strongly agree\"]";

    private const string SleepQualityLabels =
        "[\"Very poor\",\"Poor\",\"Fair\",\"Good\",\"Excellent\"]";

    private const string StressLabels =
        "[\"Calm\",\"2\",\"3\",\"4\",\"5\",\"6\",\"7\",\"8\",\"9\",\"Overwhelmed\"]";

    private const string MoodLabels =
        "[\"Very low\",\"Low\",\"Neutral\",\"Good\",\"Very good\"]";

    public static IReadOnlyList<ScaleTemplate> All { get; } =
    [
        new ScaleTemplate
        {
            Id = Guid.Parse("00000002-0000-0000-0000-000000000001"),
            Code = "bristol",
            Name = "Bristol Stool Scale",
            MinValue = 1,
            MaxValue = 7,
            LabelsJson = BristolLabels,
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new ScaleTemplate
        {
            Id = Guid.Parse("00000002-0000-0000-0000-000000000002"),
            Code = "mayo-rectal-bleeding",
            Name = "Mayo Rectal Bleeding Score",
            MinValue = 0,
            MaxValue = 3,
            LabelsJson = MayoLabels,
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new ScaleTemplate
        {
            Id = Guid.Parse("00000002-0000-0000-0000-000000000003"),
            Code = "nrs-pain",
            Name = "NRS Pain Scale",
            MinValue = 0,
            MaxValue = 10,
            LabelsJson = NrsPainLabels,
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new ScaleTemplate
        {
            Id = Guid.Parse("00000002-0000-0000-0000-000000000004"),
            Code = "likert-5",
            Name = "Likert Scale (1–5)",
            MinValue = 1,
            MaxValue = 5,
            LabelsJson = LikertLabels,
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new ScaleTemplate
        {
            Id = Guid.Parse("00000002-0000-0000-0000-000000000005"),
            Code = "sleep-quality",
            Name = "Sleep Quality (1–5)",
            MinValue = 1,
            MaxValue = 5,
            LabelsJson = SleepQualityLabels,
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new ScaleTemplate
        {
            Id = Guid.Parse("00000002-0000-0000-0000-000000000006"),
            Code = "stress-10",
            Name = "Stress Level (1–10)",
            MinValue = 1,
            MaxValue = 10,
            LabelsJson = StressLabels,
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
        new ScaleTemplate
        {
            Id = Guid.Parse("00000002-0000-0000-0000-000000000007"),
            Code = "mood-5",
            Name = "Mood Scale (1–5)",
            MinValue = 1,
            MaxValue = 5,
            LabelsJson = MoodLabels,
            IsOfficial = true,
            CreatedAt = SeedDate,
        },
    ];
}
