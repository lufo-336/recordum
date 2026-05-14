using Recordum.Plugins.Contracts.Permissions;
using Recordum.Plugins.Permissions;
using Xunit;

namespace Recordum.Plugins.Tests.Permissions;

public class AlwaysAllowPermissionBrokerTests
{
    [Fact]
    public void Check_returns_allowed_for_any_request()
    {
        var sut = new AlwaysAllowPermissionBroker();
        var decision = sut.Check(new PermissionRequest("io.recordum.test", WellKnownPermissions.DataWriteHabitNodes));
        Assert.Equal(PermissionOutcome.Allowed, decision.Outcome);
    }

    [Fact]
    public void Check_returns_allowed_even_for_unknown_permission()
    {
        var sut = new AlwaysAllowPermissionBroker();
        var decision = sut.Check(new PermissionRequest("anything", new PermissionId("custom", "made-up")));
        Assert.Equal(PermissionOutcome.Allowed, decision.Outcome);
    }
}
