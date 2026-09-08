using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Data.Services.Aggregators;
using Xunit;

namespace Recordum.Data.Tests.Integration.Aggregators;

public sealed class HabitConfigTimelineTests
{
    private static readonly TimeZoneInfo Plus2 =
        TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");

    [Fact]
    public void Resolve_returns_current_when_no_history()
    {
        var cur = new HabitConfig { Id = Guid.NewGuid(), StreakMode = StreakMode.Daily,
                                     UpdatedAt = new DateTime(2026, 1, 1) };
        var tl = new HabitConfigTimeline(cur, Array.Empty<HabitConfigHistory>(), DayBoundary.Utc);
        Assert.Equal(StreakMode.Daily, tl.ResolveFor(new DateOnly(2026, 5, 1)).StreakMode);
        Assert.Equal(StreakMode.Daily, tl.ResolveFor(new DateOnly(2025, 1, 1)).StreakMode);
    }

    [Fact]
    public void Resolve_picks_history_for_day_in_range()
    {
        var cur = new HabitConfig { Id = Guid.NewGuid(), StreakMode = StreakMode.Weekly,
                                     UpdatedAt = new DateTime(2026, 3, 1) };
        var hist = new[]
        {
            new HabitConfigHistory { Id = Guid.NewGuid(), StreakMode = StreakMode.Daily,
                                      ValidFrom = new DateTime(2026, 1, 1), ValidUntil = new DateTime(2026, 3, 1) }
        };
        var tl = new HabitConfigTimeline(cur, hist, DayBoundary.Utc);
        Assert.Equal(StreakMode.Daily, tl.ResolveFor(new DateOnly(2026, 2, 1)).StreakMode);
        Assert.Equal(StreakMode.Weekly, tl.ResolveFor(new DateOnly(2026, 5, 1)).StreakMode);
    }

    [Fact]
    public void Resolve_before_first_history_returns_current()
    {
        var cur = new HabitConfig { Id = Guid.NewGuid(), StreakMode = StreakMode.Weekly,
                                     UpdatedAt = new DateTime(2026, 3, 1) };
        var hist = new[]
        {
            new HabitConfigHistory { Id = Guid.NewGuid(), StreakMode = StreakMode.Daily,
                                      ValidFrom = new DateTime(2026, 2, 1), ValidUntil = new DateTime(2026, 3, 1) }
        };
        var tl = new HabitConfigTimeline(cur, hist, DayBoundary.Utc);
        Assert.Equal(StreakMode.Weekly, tl.ResolveFor(new DateOnly(2026, 1, 1)).StreakMode);
    }

    [Fact]
    public void The_config_of_a_day_is_resolved_at_the_end_of_that_local_day()
    {
        // Il cambio config avviene alle 22:30Z (00:30 locale del 19, fuso +2): è già il giorno
        // successivo localmente, quindi il 18 resta sulla vecchia config e solo il 19 vede la nuova.
        // Un confine calcolato in UTC (23:59:59 del 18, come faceva il vecchio ResolveFor) avrebbe
        // invece promosso la nuova config al 18 in anticipo — l'esatto bug che B5 chiude.
        var changedAt = new DateTime(2026, 8, 18, 22, 30, 0, DateTimeKind.Utc);
        var cur = new HabitConfig { Id = Guid.NewGuid(), StreakMode = StreakMode.Weekly,
                                     UpdatedAt = changedAt };
        var hist = new[]
        {
            new HabitConfigHistory { Id = Guid.NewGuid(), StreakMode = StreakMode.Daily,
                                      ValidFrom = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                                      ValidUntil = changedAt }
        };
        var day = new DayBoundary(Plus2, TimeOnly.MinValue);
        var tl = new HabitConfigTimeline(cur, hist, day);

        Assert.Equal(StreakMode.Daily, tl.ResolveFor(new DateOnly(2026, 8, 18)).StreakMode);
        Assert.Equal(StreakMode.Weekly, tl.ResolveFor(new DateOnly(2026, 8, 19)).StreakMode);
    }
}
