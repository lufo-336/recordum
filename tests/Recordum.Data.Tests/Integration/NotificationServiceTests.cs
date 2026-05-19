using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Integration;

public class NotificationServiceTests
{
    private static async Task<(DbFixture fx, Guid hid)> SeedAsync()
    {
        var fx = new DbFixture();
        var hid = Guid.NewGuid();
        await using var db = await fx.Factory.CreateDbContextAsync();
        db.HabitNodes.Add(new HabitNode { Id = hid, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" });
        await db.SaveChangesAsync();
        return (fx, hid);
    }

    [Fact]
    public async Task Disable_persists_IsEnabled_false()
    {
        var (fx, hid) = await SeedAsync();
        var svc = new NotificationService(fx.Factory, fx.Clock, NullLogger<NotificationService>.Instance);
        var id = await svc.CreateAsync(new Notification
        {
            HabitNodeId = hid,
            NotifyType = NotifyType.Reminder,
            NotifyTime = new TimeOnly(9, 0),
        });
        await svc.UpdateAsync(id, n => n.IsEnabled = false);
        await using var db = await fx.Factory.CreateDbContextAsync();
        Assert.False((await db.Notifications.FindAsync(id))!.IsEnabled);
        await fx.DisposeAsync();
    }
}
