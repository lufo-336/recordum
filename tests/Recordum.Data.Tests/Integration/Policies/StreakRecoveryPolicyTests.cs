using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Data.Services.Policies;
using Xunit;

namespace Recordum.Data.Tests.Integration.Policies;

public sealed class StreakRecoveryPolicyTests
{
    private static RecoveryContext Ctx(int absorbed = 0, DateTime? broken = null) =>
        new(new DateOnly(2026, 5, 1), absorbed, broken, new DateOnly(2026, 4, 1));

    [Fact] public void None_never_absorbs() { var p = new NoneStreakRecoveryPolicy(); Assert.False(p.TryAbsorb(Ctx())); Assert.Equal(StreakRecovery.None, p.Recovery); }
    [Fact] public void Retroactive_never_absorbs() { var p = new RetroactiveStreakRecoveryPolicy(); Assert.False(p.TryAbsorb(Ctx())); Assert.Equal(StreakRecovery.Retroactive, p.Recovery); }
    [Fact] public void Manual_never_absorbs() { var p = new ManualStreakRecoveryPolicy(); Assert.False(p.TryAbsorb(Ctx())); Assert.Equal(StreakRecovery.Manual, p.Recovery); }

    [Fact]
    public void GraceDay_absorbs_first_miss_only()
    {
        var p = new GraceDayStreakRecoveryPolicy();
        Assert.Equal(StreakRecovery.GraceDay, p.Recovery);
        Assert.True(p.TryAbsorb(Ctx(absorbed: 0)));
        Assert.False(p.TryAbsorb(Ctx(absorbed: 1)));
    }
}
