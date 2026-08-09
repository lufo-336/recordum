using FluentAssertions;
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
    public async Task Set_stores_camelCase_json_keys()
    {
        var (service, fixture) = CreateService();
        var habitId = await SeedHabitAsync(fixture);
        await service.SetAsync(habitId, new HabitUiPrefs
        {
            KeyFigure = "streak",
            Strip = HomeStrip.Days14,
            OpenColumns = new[] { "Cronologia" },
            JournalGesture = JournalPrimaryGesture.QuickNote,
        });

        string? json;
        await using (var db = await fixture.Factory.CreateDbContextAsync())
        {
            var node = await db.HabitNodes.FindAsync(habitId);
            json = node!.UiPrefsJson;
        }

        Assert.NotNull(json);
        // Documented on-disk contract (docs/ARCHITECTURE.md) is camelCase.
        Assert.Contains("\"keyFigure\"", json, StringComparison.Ordinal);
        Assert.Contains("\"strip\"", json, StringComparison.Ordinal);
        Assert.Contains("\"openColumns\"", json, StringComparison.Ordinal);
        Assert.Contains("\"journalGesture\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Strip\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"KeyFigure\"", json, StringComparison.Ordinal);
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

    [Fact]
    public async Task UpdateAsync_merges_into_the_current_row_instead_of_a_caller_held_snapshot()
    {
        // C2: two callers each hold their own snapshot from an earlier GetAsync (as the Habit Editor's
        // Registration and HomeRow sections do). Both mutate through UpdateAsync — each call re-reads
        // the row fresh, so neither's write reverts the other's, unlike GetAsync+SetAsync by hand.
        var (service, fixture) = CreateService();
        var habitId = await SeedHabitAsync(fixture);
        var snapshotA = await service.GetAsync(habitId);
        var snapshotB = await service.GetAsync(habitId);
        _ = snapshotA; _ = snapshotB; // both callers loaded before either wrote — the stale-snapshot setup

        await service.UpdateAsync(habitId, p => p with { KeyFigure = "streak" });
        await service.UpdateAsync(habitId, p => p with { JournalGesture = JournalPrimaryGesture.QuickNote });

        var got = await service.GetAsync(habitId);
        got.KeyFigure.Should().Be("streak", "the first UpdateAsync's field must survive the second");
        got.JournalGesture.Should().Be(JournalPrimaryGesture.QuickNote);
        await fixture.DisposeAsync();
    }

    [Fact]
    public async Task UpdateAsync_throws_when_node_missing()
    {
        var (service, fixture) = CreateService();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(Guid.NewGuid(), p => p).AsTask());
        await fixture.DisposeAsync();
    }
}
