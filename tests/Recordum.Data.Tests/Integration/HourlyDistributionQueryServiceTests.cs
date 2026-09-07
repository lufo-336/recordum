using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Services.Queries;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Events;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public sealed class HourlyDistributionQueryServiceTests
{
    private static async Task<(DbFixture fx, HabitNode habit)> SeedHabit()
    {
        var fx = new DbFixture(clock: new FakeClock(localTimeZone: TimeZoneInfo.Utc));
        var habit = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" };
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(habit);
            await db.SaveChangesAsync();
        }
        return (fx, habit);
    }

    private static async Task RecordAt(DbFixture fx, Guid habitId, DateTime utcWhen, bool isRetroactive = false)
    {
        var svc = new CompletionService(fx.Factory, fx.Clock, NullLogger<CompletionService>.Instance, new RecordingEventBus(), fx.ResetTimes);
        await svc.RecordAsync(new Completion
        {
            HabitNodeId = habitId,
            CompletedAt = utcWhen,
            CompletedAtMs = new DateTimeOffset(utcWhen, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            IsRetroactive = isRetroactive,
        });
    }

    [Fact]
    public async Task Returns_24_buckets()
    {
        var (fx, habit) = await SeedHabit();
        var sut = new HourlyDistributionQueryService(fx.Factory, fx.Clock);

        var rows = await sut.GetHourlyAsync(habit.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(24, rows.Count);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Bucketizes_completion_by_local_hour()
    {
        var (fx, habit) = await SeedHabit();
        await RecordAt(fx, habit.Id, new DateTime(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc));
        await RecordAt(fx, habit.Id, new DateTime(2026, 1, 5, 9, 30, 0, DateTimeKind.Utc));
        await RecordAt(fx, habit.Id, new DateTime(2026, 1, 5, 9, 45, 0, DateTimeKind.Utc));
        await RecordAt(fx, habit.Id, new DateTime(2026, 1, 6, 14, 0, 0, DateTimeKind.Utc));
        await RecordAt(fx, habit.Id, new DateTime(2026, 1, 6, 14, 30, 0, DateTimeKind.Utc));

        var sut = new HourlyDistributionQueryService(fx.Factory, fx.Clock);
        var rows = await sut.GetHourlyAsync(habit.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(3, rows[9]);
        Assert.Equal(2, rows[14]);
        Assert.Equal(0, rows[0]);
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Excludes_retroactive_completions()
    {
        var (fx, habit) = await SeedHabit();
        await RecordAt(fx, habit.Id, new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc), isRetroactive: true);

        var sut = new HourlyDistributionQueryService(fx.Factory, fx.Clock);
        var rows = await sut.GetHourlyAsync(habit.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.All(rows, c => Assert.Equal(0, c));
        await fx.DisposeAsync();
    }
}
