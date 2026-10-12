using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Recordum.Plugins.Contracts.Events;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class HabitNodeUpdateEventTests
{
    [Fact]
    public async Task UpdateAsync_publishes_HabitUpdated_once_after_the_commit()
    {
        await using var fx = new DbFixture();
        var bus = new RecordingEventBus();
        var svc = new HabitNodeService(fx.Factory, fx.Clock, fx.PluginContext, NullLogger<HabitNodeService>.Instance, bus);
        var id = await svc.CreateAsync(new HabitNode { NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "A" });

        await svc.UpdateAsync(id, n => n.Name = "B");

        var published = bus.Published.OfType<HabitUpdatedPayload>().ToList();
        Assert.Single(published);
        Assert.Equal(id, published[0].HabitNodeId);
    }
}
