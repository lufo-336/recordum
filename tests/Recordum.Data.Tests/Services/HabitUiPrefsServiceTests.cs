using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Data.Services;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Services;

public class HabitUiPrefsServiceTests
{
    private static (HabitUiPrefsService service, DbFixture fixture) CreateService()
    {
        var fixture = new DbFixture();
        var service = new HabitUiPrefsService(fixture.Factory);
        return (service, fixture);
    }

    private static async Task<Guid> SeedHabitAsync(DbFixture fixture)
    {
        await using var db = await fixture.Factory.CreateDbContextAsync();
        var habit = new HabitNode
        {
            Id = Guid.NewGuid(),
            NodeType = NodeType.Habit,
            HabitType = HabitType.Counter,
            Name = "x",
            Color = "#abc123",
        };
        db.HabitNodes.Add(habit);
        await db.SaveChangesAsync();
        return habit.Id;
    }

    [Fact]
    public async Task Get_returns_empty_prefs_when_column_is_null()
    {
        var (service, fixture) = CreateService();
        var habitId = await SeedHabitAsync(fixture);
        var prefs = await service.GetAsync(habitId);
        Assert.Null(prefs.KeyFigure);
        Assert.Null(prefs.Strip);
        Assert.Null(prefs.OpenColumns);
        Assert.Null(prefs.JournalGesture);
        await fixture.DisposeAsync();
    }

    [Fact]
    public async Task Set_then_Get_round_trips_all_fields()
    {
        var (service, fixture) = CreateService();
        var habitId = await SeedHabitAsync(fixture);
        var prefs = new HabitUiPrefs
        {
            KeyFigure = "streak",
            Strip = HomeStrip.Days14,
            OpenColumns = new[] { "Cronologia", "Diario" },
            JournalGesture = JournalPrimaryGesture.QuickNote,
        };
        await service.SetAsync(habitId, prefs);

        var got = await service.GetAsync(habitId);
        Assert.Equal("streak", got.KeyFigure);
        Assert.Equal(HomeStrip.Days14, got.Strip);
        Assert.Equal(new[] { "Cronologia", "Diario" }, got.OpenColumns);
        Assert.Equal(JournalPrimaryGesture.QuickNote, got.JournalGesture);
        await fixture.DisposeAsync();
    }

    [Fact]
    public async Task Set_throws_when_node_missing()
    {
        var (service, fixture) = CreateService();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetAsync(Guid.NewGuid(), new HabitUiPrefs()).AsTask());
        await fixture.DisposeAsync();
    }
}
