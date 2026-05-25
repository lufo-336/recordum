using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services.Aggregators;
using Xunit;

namespace Recordum.Data.Tests.Integration.Aggregators;

public sealed class HabitConfigTimelineTests
{
    [Fact]
    public void Resolve_returns_current_when_no_history()
    {
        var cur = new HabitConfig { Id = Guid.NewGuid(), StreakMode = StreakMode.Daily,
                                     UpdatedAt = new DateTime(2026, 1, 1) };
        var tl = new HabitConfigTimeline(cur, Array.Empty<HabitConfigHistory>());
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
        var tl = new HabitConfigTimeline(cur, hist);
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
        var tl = new HabitConfigTimeline(cur, hist);
        Assert.Equal(StreakMode.Weekly, tl.ResolveFor(new DateOnly(2026, 1, 1)).StreakMode);
    }
}
