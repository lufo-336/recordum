using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures;

namespace Recordum.Data.Tests.Backup;

/// <summary>Small deterministic content of the reference .recbak fixtures. Written by the generator, asserted by the tests.</summary>
public static class RecbakReferenceSeed
{
    public const string Passphrase = "cross-platform-reference";
    public static readonly Guid RunId = Guid.Parse("5eed0000-0000-4000-8000-000000000001");
    public static readonly Guid WaterId = Guid.Parse("5eed0000-0000-4000-8000-000000000002");
    public static readonly Guid SmokeId = Guid.Parse("5eed0000-0000-4000-8000-000000000003");
    public static readonly Guid DiaryId = Guid.Parse("5eed0000-0000-4000-8000-000000000004");
    public static readonly Guid NoteId = Guid.Parse("5eed0000-0000-4000-8000-000000000005");
    public static readonly Guid PeriodId = Guid.Parse("5eed0000-0000-4000-8000-000000000006");
    public static readonly Guid RunDone1 = Guid.Parse("5eed0000-0000-4000-8000-000000000011");
    public static readonly Guid RunDone2 = Guid.Parse("5eed0000-0000-4000-8000-000000000012");
    public static readonly Guid WaterDone = Guid.Parse("5eed0000-0000-4000-8000-000000000013");

    public const string RunNotes = "Easy pace, è stato bello";   // non-ASCII on purpose
    public const string NoteBody = "Reference note for the cross-platform restore.";
    public static readonly DateTime Noon1 = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime Noon2 = new(2026, 3, 2, 12, 0, 0, DateTimeKind.Utc);
    public const long PeriodStartMs = 1_772_000_000_000;

    public static async Task WriteAsync(EncryptedDbFixture f)
    {
        f.Clock.Set(new DateTimeOffset(2026, 3, 3, 8, 0, 0, TimeSpan.Zero));
        await using var db = await f.DbFactory.CreateDbContextAsync();
        db.HabitNodes.AddRange(
            new HabitNode { Id = RunId, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Morning run", Color = "#2E7D32" },
            new HabitNode { Id = WaterId, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "Water", Color = "#1565C0" },
            new HabitNode { Id = SmokeId, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "No smoking", Color = "#6D4C41" },
            new HabitNode { Id = DiaryId, NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "Diary", Color = "#6A1B9A" });
        await db.SaveChangesAsync();
        db.Completions.AddRange(
            new Completion { Id = RunDone1, HabitNodeId = RunId, CompletedAt = Noon1, CompletedAtMs = new DateTimeOffset(Noon1).ToUnixTimeMilliseconds(), Notes = RunNotes },
            new Completion { Id = RunDone2, HabitNodeId = RunId, CompletedAt = Noon2, CompletedAtMs = new DateTimeOffset(Noon2).ToUnixTimeMilliseconds() },
            new Completion { Id = WaterDone, HabitNodeId = WaterId, CompletedAt = Noon2, CompletedAtMs = new DateTimeOffset(Noon2).ToUnixTimeMilliseconds(), DurationSeconds = 90 });
        db.Notes.Add(new Note { Id = NoteId, HabitNodeId = DiaryId, BodyMd = NoteBody });
        db.AbstinencePeriods.Add(new AbstinencePeriod { Id = PeriodId, HabitNodeId = SmokeId, StartedAtMs = PeriodStartMs });
        await db.SaveChangesAsync();
    }
}
