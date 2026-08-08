using Recordum.Core.Models;

namespace Recordum.Data.SeedData;

/// <summary>
/// Official seed data for HabitTemplate. GUIDs and PayloadJson are stable constants required by EF Core HasData.
/// PayloadJson encodes a simple snapshot of how the habit (or group of habits) would be created.
/// Timestamps are literal constants — TimestampInterceptor does not run during HasData INSERT generation.
/// </summary>
public static class HabitTemplateSeed
{
    private static readonly DateTime SeedDate = new(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc);
    private static readonly long SeedDateMs = new DateTimeOffset(2026, 5, 14, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    // Pre-computed JSON payloads as string literals for HasData determinism.

    private const string MorningRoutinePayload =
        "{\"node\":{\"name\":\"Morning routine\",\"node_type\":\"Habit\",\"habit_type\":\"Action\",\"color\":\"#f5a623\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[],\"segments\":[]}";

    private const string WorkoutLogPayload =
        "{\"node\":{\"name\":\"Workout log\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#e85d4a\"},\"config\":{\"schedule_type\":\"SpecificDays\",\"streak_mode\":\"Weekly\",\"streak_recovery\":\"None\",\"has_integrated_timer\":true,\"timer_mode\":\"Session\"},\"metrics\":[{\"name\":\"Duration\",\"type\":\"Seconds\",\"unit\":\"minutes\"},{\"name\":\"Reps\",\"type\":\"Integer\"},{\"name\":\"Weight\",\"type\":\"Decimal\"}],\"segments\":[{\"name\":\"Work\",\"is_rest\":false,\"sort_order\":0},{\"name\":\"Rest\",\"is_rest\":true,\"sort_order\":1}]}";

    private const string DailyJournalPayload =
        "{\"node\":{\"name\":\"Daily journal\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#4a90e2\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"ui_prefs\":{\"journal_gesture\":\"Diary\"},\"metrics\":[],\"segments\":[]}";

    private const string WaterIntakePayload =
        "{\"node\":{\"name\":\"Water intake\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#5bc8f5\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[{\"name\":\"Amount\",\"type\":\"Decimal\",\"unit\":\"l\"}],\"segments\":[],\"goal\":{\"target\":2,\"unit\":\"l\",\"period_type\":\"Open\"}}";

    private const string SleepTrackerPayload =
        "{\"node\":{\"name\":\"Sleep tracker\",\"node_type\":\"Habit\",\"habit_type\":\"Counter\",\"color\":\"#7b68ee\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\",\"repeatable\":true,\"series_labels\":[\"Night\",\"Nap\"]},\"metrics\":[{\"name\":\"Hours slept\",\"type\":\"Decimal\",\"unit\":\"hours\"},{\"name\":\"Sleep quality\",\"type\":\"Integer\",\"scale_code\":\"sleep-quality\"}],\"segments\":[]}";

    private const string StopSmokingPayload =
        "{\"node\":{\"name\":\"Stop smoking\",\"node_type\":\"Habit\",\"habit_type\":\"Resistance\",\"color\":\"#27ae60\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"None\"},\"metrics\":[],\"segments\":[]}";

    private const string MoodTrackerPayload =
        "{\"node\":{\"name\":\"Mood tracker\",\"node_type\":\"Habit\",\"habit_type\":\"State\",\"color\":\"#f39c12\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"metrics\":[{\"name\":\"Mood\",\"type\":\"Integer\",\"scale_code\":\"mood-5\"}],\"segments\":[]}";

    private const string FoodDiaryPayload =
        "{\"node\":{\"name\":\"Food diary\",\"node_type\":\"Habit\",\"habit_type\":\"Journal\",\"color\":\"#e67e22\"},\"config\":{\"schedule_type\":\"Daily\",\"streak_mode\":\"Daily\",\"streak_recovery\":\"GraceDay\"},\"ui_prefs\":{\"journal_gesture\":\"QuickNote\"},\"metrics\":[],\"segments\":[]}";

    public static IReadOnlyList<HabitTemplate> All { get; } =
    [
        new HabitTemplate
        {
            Id = Guid.Parse("00000003-0000-0000-0000-000000000001"),
            Name = "Morning routine",
            Description = "A daily action habit to anchor your morning routine.",
            Category = "Daily routines",
            PayloadJson = MorningRoutinePayload,
            IsOfficial = true,
            CreatedByPluginId = null,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
            UpdatedAtMs = SeedDateMs,
        },
        new HabitTemplate
        {
            Id = Guid.Parse("00000003-0000-0000-0000-000000000002"),
            Name = "Workout log",
            Description = "Session-mode habit for logging gym workouts with Work/Rest segments.",
            Category = "Fitness",
            PayloadJson = WorkoutLogPayload,
            IsOfficial = true,
            CreatedByPluginId = null,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
            UpdatedAtMs = SeedDateMs,
        },
        new HabitTemplate
        {
            Id = Guid.Parse("00000003-0000-0000-0000-000000000003"),
            Name = "Daily journal",
            Description = "A free-form Markdown journal entry, one per day.",
            Category = "Daily routines",
            PayloadJson = DailyJournalPayload,
            IsOfficial = true,
            CreatedByPluginId = null,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
            UpdatedAtMs = SeedDateMs,
        },
        new HabitTemplate
        {
            Id = Guid.Parse("00000003-0000-0000-0000-000000000004"),
            Name = "Water intake",
            Description = "Track daily water consumption in litres with a 2L goal.",
            Category = "Health",
            PayloadJson = WaterIntakePayload,
            IsOfficial = true,
            CreatedByPluginId = null,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
            UpdatedAtMs = SeedDateMs,
        },
        new HabitTemplate
        {
            Id = Guid.Parse("00000003-0000-0000-0000-000000000005"),
            Name = "Sleep tracker",
            Description = "Log hours slept and subjective sleep quality each morning.",
            Category = "Health",
            PayloadJson = SleepTrackerPayload,
            IsOfficial = true,
            CreatedByPluginId = null,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
            UpdatedAtMs = SeedDateMs,
        },
        new HabitTemplate
        {
            Id = Guid.Parse("00000003-0000-0000-0000-000000000006"),
            Name = "Stop smoking",
            Description = "Resistance habit that tracks abstinence time since last cigarette.",
            Category = "Health",
            PayloadJson = StopSmokingPayload,
            IsOfficial = true,
            CreatedByPluginId = null,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
            UpdatedAtMs = SeedDateMs,
        },
        new HabitTemplate
        {
            Id = Guid.Parse("00000003-0000-0000-0000-000000000007"),
            Name = "Mood tracker",
            Description = "Daily mood state on a 1–5 labelled scale.",
            Category = "Wellbeing",
            PayloadJson = MoodTrackerPayload,
            IsOfficial = true,
            CreatedByPluginId = null,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
            UpdatedAtMs = SeedDateMs,
        },
        new HabitTemplate
        {
            Id = Guid.Parse("00000003-0000-0000-0000-000000000008"),
            Name = "Food diary",
            Description = "Log each meal in seconds: a short note and a photo, as many times a day as you eat.",
            Category = "Health",
            PayloadJson = FoodDiaryPayload,
            IsOfficial = true,
            CreatedByPluginId = null,
            CreatedAt = SeedDate,
            UpdatedAt = SeedDate,
            UpdatedAtMs = SeedDateMs,
        },
    ];
}
