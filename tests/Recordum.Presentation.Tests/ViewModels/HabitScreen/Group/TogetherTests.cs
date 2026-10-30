using System.Collections.Immutable;
using System.Globalization;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Group;
using Recordum.Presentation.ViewModels.HabitScreen.History;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.Group;

/// <summary>"Insieme" e la mappa del gruppo. Oggi = 28/10/2026.</summary>
public sealed class TogetherTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 28);
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture, _uiCulture = CultureInfo.CurrentUICulture;
    private readonly FakeGroupHistoryQueryService _history = new();

    public TogetherTests() => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("it-IT");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    private static DateOnly D(int day) => new(2026, 10, day);

    private static ImmutableHashSet<DateOnly> Set(params int[] days) => days.Select(D).ToImmutableHashSet();

    private void Child(string name, int startsOn, ImmutableHashSet<DateOnly> scheduled, ImmutableHashSet<DateOnly> done)
        => _history.Children.Add(new GroupChildHistory(Guid.NewGuid(), name, "#112233", HabitType.Action, 1, D(startsOn), scheduled, done));

    [Fact]
    public async Task One_cell_per_day_of_the_period()
    {
        Child("a", 1, Set(26, 27, 28), Set(27));
        Child("b", 1, Set(26, 27, 28), Set());
        var vm = new TogetherViewModel(_history);

        await vm.LoadAsync(Guid.NewGuid(), D(26), new DateOnly(2026, 11, 1), Today, default);

        vm.Rows.Should().HaveCount(2);
        vm.Rows.Should().OnlyContain(r => r.Cells.Count == 7);
        var a = vm.Rows[0];
        a.Name.Should().Be("a");
        a.Depth.Should().Be(1);
        a.Cells.Select(c => c.Step).Should().Equal(0, 4, 0, 0, 0, 0, 0);   // 26 non fatta, 27 fatta
    }

    [Fact]
    public async Task Not_scheduled_is_out_of_range()
    {
        // Nata il 27 (niente prima), e senza il 28 previsto (giorno libero del programma).
        Child("a", 27, Set(27), Set());
        var vm = new TogetherViewModel(_history);

        await vm.LoadAsync(Guid.NewGuid(), D(26), D(28), Today, default);

        vm.Rows[0].Cells.Select(c => c.InRange).Should().Equal(false, true, false);
    }

    [Fact]
    public async Task Future_days_are_future()
    {
        Child("a", 1, Set(28), Set());
        var vm = new TogetherViewModel(_history);

        await vm.LoadAsync(Guid.NewGuid(), D(27), D(30), Today, default);

        vm.Rows[0].Cells.Select(c => c.IsFuture).Should().Equal(false, false, true, true);
        vm.Rows[0].Cells.Select(c => c.IsToday).Should().Equal(false, true, false, false);
    }

    [Fact]
    public async Task Together_today_is_the_groups_today()
    {
        // Alle 04:30 del 28 il gruppo (reset 00:00) è al 28; la figlia (reset 05:00) è ancora al 27 e la query
        // le dà il 27 come ultimo giorno previsto, con la registrazione delle 04:30 dentro.
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 4, 30, 0, TimeSpan.Zero), groupHistory: _history);
        var group = Guid.NewGuid();
        rig.Nodes.Store[group] = new HabitNode { Id = group, NodeType = NodeType.Group, Name = "G", CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc) };
        Child("a", 1, Set(26, 27), Set(27));

        await rig.Vm.LoadAsync(group);

        var cells = rig.Vm.Panoramica.Together.Rows.Single().Cells;
        var lastNotFuture = cells.Last(c => !c.IsFuture);
        lastNotFuture.Date.Should().Be(D(28));
        lastNotFuture.IsToday.Should().BeTrue();
        cells.Single(c => c.Date == D(27)).Step.Should().Be(4);
        cells.Single(c => c.Date == D(28)).InRange.Should().BeFalse();
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    public async Task Group_map_steps_count_children(int done, int step)
    {
        // Quattro previste il 27: 2 di 4 -> 2, 4 di 4 -> 4, nessuna -> 0 (1 -> 1, 3 -> 3: mai 4 finché ne manca una).
        for (var i = 0; i < 4; i++)
            Child("c" + i, 1, Set(27), i < done ? Set(27) : Set());
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero), groupHistory: _history);
        var group = Guid.NewGuid();
        rig.Nodes.Store[group] = new HabitNode { Id = group, NodeType = NodeType.Group, Name = "G", CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc) };

        await rig.Vm.LoadAsync(group);

        var map = rig.Vm.Panoramica.Map;
        map.Shape.Should().Be(MapShape.Heatmap);
        map.Weeks.SelectMany(w => w).Single(c => c.Date == D(27)).Step.Should().Be(step);
    }

    [Fact]
    public async Task A_day_without_scheduled_children_is_zero()
    {
        Child("a", 1, Set(27), Set(27));
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero), groupHistory: _history);
        var group = Guid.NewGuid();
        rig.Nodes.Store[group] = new HabitNode { Id = group, NodeType = NodeType.Group, Name = "G", CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc) };

        await rig.Vm.LoadAsync(group);

        rig.Vm.Panoramica.Map.Weeks.SelectMany(w => w).Single(c => c.Date == D(26)).Step.Should().Be(0);
    }

    [Fact]
    public async Task Default_period_of_a_group_is_year_and_a_habits_stays_month()
    {
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero), groupHistory: _history);
        var group = Guid.NewGuid();
        var habit = Guid.NewGuid();
        rig.Nodes.Store[group] = new HabitNode { Id = group, NodeType = NodeType.Group, Name = "G", CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc) };
        rig.Nodes.Store[habit] = new HabitNode { Id = habit, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h", CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc) };

        await rig.Vm.LoadAsync(group);
        rig.Vm.Period.Period.Should().Be(HistoryPeriod.Year);
        rig.Vm.Period.From.Should().Be(new DateOnly(2026, 1, 1));

        await rig.Vm.LoadAsync(habit);
        rig.Vm.Period.Period.Should().Be(HistoryPeriod.Month);

        // Cambiare periodo ricarica "Insieme" per il gruppo.
        Child("a", 1, Set(27), Set(27));
        await rig.Vm.LoadAsync(group);
        rig.Vm.Period.Period = HistoryPeriod.Week;
        await WaitUntilAsync(() => rig.Vm.Panoramica.Together.Rows.Count == 1 && rig.Vm.Panoramica.Together.Rows[0].Cells.Count == 7);
    }

    [Fact]
    public async Task A_completion_on_an_unscheduled_day_shows_as_done_and_in_range()
    {
        Child("a", 1, Set(27), Set(26));
        var vm = new TogetherViewModel(_history);

        await vm.LoadAsync(Guid.NewGuid(), D(26), D(27), Today, default);

        vm.Rows[0].Cells[0].Step.Should().Be(4);
        vm.Rows[0].Cells[0].InRange.Should().BeTrue();
    }

    [Fact]
    public async Task Together_follows_the_order_of_the_rows_with_a_pin_and_equal_sort_orders()
    {
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero), groupHistory: _history);
        var group = Guid.NewGuid();
        rig.Nodes.Store[group] = new HabitNode { Id = group, NodeType = NodeType.Group, Name = "G", CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc) };
        rig.Tree.HomeRows.Add(Tree(group, null, "G", null, PinScope.None));
        var ids = new Dictionary<string, Guid>();
        // La query le dà per SortOrder (qui in ordine a, b, c); la Home mette prima la fissata e rompe i pari per nome.
        foreach (var (name, pin) in new[] { ("zeta", PinScope.None), ("beta", PinScope.None), ("pinned", PinScope.Local) })
        {
            ids[name] = Guid.NewGuid();
            rig.Tree.HomeRows.Add(Tree(ids[name], group, name, HabitType.Action, pin));
            _history.Children.Add(new GroupChildHistory(ids[name], name, "#112233", HabitType.Action, 0, D(1), Set(27), Set()));
        }

        await rig.Vm.LoadAsync(group);

        var expected = rig.Vm.Panoramica.Rows.Select(r => r.Name).ToList();
        expected.Should().Equal("pinned", "beta", "zeta");
        rig.Vm.Panoramica.Together.Rows.Select(r => r.Name).Should().Equal(expected);
    }

    private static HomeTreeRow Tree(Guid id, Guid? parent, string name, HabitType? type, PinScope pin)
        => new(id, parent, name, "#112233", type, null, 0, false, pin, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(10);
        condition().Should().BeTrue("the period change should have reloaded in time");
    }
}
