using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.Data.Tests.Performance;

public static class PerfDataSeeder
{
    public static async Task SeedAsync(
        IDbContextFactory<RecordumDbContext> factory,
        int seed = 42,
        CancellationToken ct = default)
    {
        var rnd = new Random(seed);
        await using var db = await factory.CreateDbContextAsync(ct);

        var groups = new List<HabitNode>();
        for (int i = 0; i < 30; i++)
        {
            Guid? parent = groups.Count > 0 && rnd.NextDouble() < 0.5
                ? groups[rnd.Next(groups.Count)].Id
                : null;
            groups.Add(new HabitNode
            {
                Id = Guid.NewGuid(),
                NodeType = NodeType.Group,
                Name = $"g{i}",
                ParentId = parent,
            });
        }
        db.HabitNodes.AddRange(groups);

        var habitTypeMix = new (HabitType type, int count)[]
        {
            (HabitType.Action, 80),
            (HabitType.Counter, 60),
            (HabitType.Resistance, 30),
            (HabitType.Journal, 20),
            (HabitType.State, 10),
        };
        var habits = new List<HabitNode>();
        int hi = 0;
        foreach (var (type, count) in habitTypeMix)
        {
            for (int j = 0; j < count; j++)
            {
                habits.Add(new HabitNode
                {
                    Id = Guid.NewGuid(),
                    NodeType = NodeType.Habit,
                    HabitType = type,
                    Name = $"h{hi++}",
                    ParentId = groups[rnd.Next(groups.Count)].Id,
                });
            }
        }
        db.HabitNodes.AddRange(habits);
        await db.SaveChangesAsync(ct);

        var horizonStart = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var horizonMs = (long)(DateTime.UtcNow - horizonStart).TotalMilliseconds;
        var batch = new List<Completion>(5000);
        for (int i = 0; i < 50_000; i++)
        {
            var habit = habits[rnd.Next(habits.Count)];
            var offsetMs = (long)(rnd.NextDouble() * horizonMs);
            var when = horizonStart.AddMilliseconds(offsetMs);
            batch.Add(new Completion
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habit.Id,
                CompletedAt = when,
                CompletedAtMs = ((DateTimeOffset)when).ToUnixTimeMilliseconds(),
                Notes = habit.HabitType == HabitType.Journal ? "x" : null,
            });
            if (batch.Count == 5000)
            {
                db.Completions.AddRange(batch);
                await db.SaveChangesAsync(ct);
                foreach (var c in batch) db.Entry(c).State = EntityState.Detached;
                batch.Clear();
            }
        }
        if (batch.Count > 0)
        {
            db.Completions.AddRange(batch);
            await db.SaveChangesAsync(ct);
        }
    }
}
