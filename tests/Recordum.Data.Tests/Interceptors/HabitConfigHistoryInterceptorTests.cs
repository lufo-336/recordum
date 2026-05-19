using Microsoft.EntityFrameworkCore;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Interceptors;

public class HabitConfigHistoryInterceptorTests
{
    private static async Task<(DbFixture fx, Guid habitId, Guid configId)> SeedAsync(FakeClock clock)
    {
        var fx = new DbFixture(clock);
        await using var db = await fx.Factory.CreateDbContextAsync();
        var node = new HabitNode
        {
            Id = Guid.NewGuid(),
            NodeType = NodeType.Habit,
            HabitType = HabitType.Action,
            Name = "n",
        };
        var cfg = new HabitConfig
        {
            Id = Guid.NewGuid(),
            HabitNodeId = node.Id,
            ScheduleType = ScheduleType.Daily,
            StreakMode = StreakMode.Daily,
            StreakRecovery = StreakRecovery.None,
        };
        db.HabitNodes.Add(node);
        db.HabitConfigs.Add(cfg);
        await db.SaveChangesAsync();
        return (fx, node.Id, cfg.Id);
    }

    [Fact]
    public async Task Update_versioned_field_produces_one_snapshot_with_prior_values()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var (fx, _, configId) = await SeedAsync(clock);
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            Assert.Empty(db.HabitConfigHistories);
        }
        clock.Advance(TimeSpan.FromHours(1));
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var cfg = await db.HabitConfigs.FirstAsync(c => c.Id == configId);
            cfg.ScheduleType = ScheduleType.WeeklyX;
            await db.SaveChangesAsync();
        }
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var hist = await db.HabitConfigHistories.SingleAsync();
            Assert.Equal(ScheduleType.Daily, hist.ScheduleType);
            Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), hist.ValidFrom);
            Assert.Equal(new DateTime(2026, 1, 1, 1, 0, 0, DateTimeKind.Utc), hist.ValidUntil);
        }
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Updating_only_excluded_columns_writes_no_history()
    {
        var clock = new FakeClock();
        var (fx, _, configId) = await SeedAsync(clock);
        clock.Advance(TimeSpan.FromHours(1));
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var cfg = await db.HabitConfigs.FirstAsync(c => c.Id == configId);
            cfg.QuickComplete = !cfg.QuickComplete;
            cfg.AiDataSharing = AiDataSharing.LocalOnly;
            await db.SaveChangesAsync();
        }
        await using (var db = await fx.Factory.CreateDbContextAsync())
            Assert.Empty(db.HabitConfigHistories);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Two_sequential_updates_yield_two_adjacent_intervals()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var (fx, _, configId) = await SeedAsync(clock);

        clock.Advance(TimeSpan.FromHours(1));
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var cfg = await db.HabitConfigs.FirstAsync(c => c.Id == configId);
            cfg.ScheduleType = ScheduleType.WeeklyX;
            await db.SaveChangesAsync();
        }
        clock.Advance(TimeSpan.FromHours(2));
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var cfg = await db.HabitConfigs.FirstAsync(c => c.Id == configId);
            cfg.ScheduleType = ScheduleType.EveryXDays;
            await db.SaveChangesAsync();
        }

        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var rows = await db.HabitConfigHistories.OrderBy(h => h.ValidFrom).ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.Equal(rows[0].ValidUntil, rows[1].ValidFrom);
        }
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task History_ValidFrom_equals_pre_modification_UpdatedAt()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var (fx, _, configId) = await SeedAsync(clock);
        var preUpdatedAt = clock.UtcNow.UtcDateTime;
        clock.Advance(TimeSpan.FromMinutes(5));
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var cfg = await db.HabitConfigs.FirstAsync(c => c.Id == configId);
            cfg.StreakMode = StreakMode.Weekly;
            await db.SaveChangesAsync();
        }
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var hist = await db.HabitConfigHistories.SingleAsync();
            Assert.Equal(preUpdatedAt, hist.ValidFrom);
        }
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Hard_delete_writes_final_snapshot()
    {
        var clock = new FakeClock();
        var (fx, _, configId) = await SeedAsync(clock);
        clock.Advance(TimeSpan.FromMinutes(30));
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var cfg = await db.HabitConfigs.FirstAsync(c => c.Id == configId);
            db.HabitConfigs.Remove(cfg);
            await db.SaveChangesAsync();
        }
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            var hist = await db.HabitConfigHistories.SingleAsync();
            Assert.Equal(ScheduleType.Daily, hist.ScheduleType);
        }
        await fx.DisposeAsync();
    }
}
