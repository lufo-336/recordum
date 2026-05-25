using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Xunit;

namespace Recordum.Core.Tests.Models;

public sealed class StreakRunTests
{
    [Fact]
    public void ThreeArg_construction_keeps_defaults()
    {
        var run = new StreakRun(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), 5);
        Assert.False(run.IsCurrent);
        Assert.Equal(0, run.RecoveriesUsed);
        Assert.Null(run.BreakReason);
    }

    [Fact]
    public void Full_construction_round_trips()
    {
        var run = new StreakRun(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 5), 5,
            IsCurrent: true, RecoveriesUsed: 2, BreakReason: StreakBreakReason.Missed);
        Assert.True(run.IsCurrent);
        Assert.Equal(2, run.RecoveriesUsed);
        Assert.Equal(StreakBreakReason.Missed, run.BreakReason);
    }
}
