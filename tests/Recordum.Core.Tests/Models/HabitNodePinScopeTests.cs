using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Xunit;

namespace Recordum.Core.Tests.Models;

public class HabitNodePinScopeTests
{
    [Fact]
    public void Default_PinScope_is_None()
    {
        var node = new HabitNode();
        node.PinScope.Should().Be(PinScope.None);
    }

    [Theory]
    [InlineData(PinScope.None)]
    [InlineData(PinScope.Local)]
    [InlineData(PinScope.Global)]
    public void PinScope_round_trips(PinScope scope)
    {
        var node = new HabitNode { PinScope = scope };
        node.PinScope.Should().Be(scope);
    }
}
