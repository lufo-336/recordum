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

    private static readonly TimeZoneInfo Plus2 =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    [Fact]
    public async Task Hourly_window_follows_the_local_day_not_UTC_midnight()
    {
        // La distribuzione converte gia' l'ora in locale; era la FINESTRA a essere UTC.
        // Chiedendo [19, 19] la registrazione delle 22:30Z del 18 (= 19 alle 00:30 locali) deve
        // comparire, nel bucket 0. In UTC la finestra cominciava alle 00:00Z del 19 e la scartava.
        var fx = new DbFixture(new FakeClock(new DateTimeOffset(2026, 8, 20, 6, 0, 0, TimeSpan.Zero), Plus2));
        var habit = new HabitNode { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" };
        await using (var db = await fx.Factory.CreateDbContextAsync())
        {
            db.HabitNodes.Add(habit);
            await db.SaveChangesAsync();
        }
        await RecordAt(fx, habit.Id, new DateTime(2026, 8, 18, 22, 30, 0, DateTimeKind.Utc));

        var sut = new HourlyDistributionQueryService(fx.Factory, fx.ResetTimes);
        var rows = await sut.GetHourlyAsync(habit.Id, new DateOnly(2026, 8, 19), new DateOnly(2026, 8, 19));

        Assert.Equal(1, rows[0]);
        Assert.Equal(1, rows.Sum());
        await fx.DisposeAsync();
    }

    [Fact]
    public async Task Returns_24_buckets()
    {
        var (fx, habit) = await SeedHabit();
        var sut = new HourlyDistributionQueryService(fx.Factory, fx.ResetTimes);

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

        var sut = new HourlyDistributionQueryService(fx.Factory, fx.ResetTimes);
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

        var sut = new HourlyDistributionQueryService(fx.Factory, fx.ResetTimes);
        var rows = await sut.GetHourlyAsync(habit.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.All(rows, c => Assert.Equal(0, c));
        await fx.DisposeAsync();
    }
}
