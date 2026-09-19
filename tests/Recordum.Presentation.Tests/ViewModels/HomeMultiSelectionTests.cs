using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels;

public class HomeMultiSelectionTests
{
    private readonly FakeHabitTreeQueryService _query = new();
    private readonly FakeHabitNodeService _nodes = new();
    private readonly FakePurgeService _purge = new();

    private HomeViewModel CreateSut() => new(_query, _nodes, _purge);

    private static HomeTreeRow SampleRow(string name, Guid? id = null, Guid? parent = null, HabitType? type = HabitType.Action) =>
        new(id ?? Guid.NewGuid(), parent, name, "#000000", type, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);

    private async Task<HomeViewModel> LoadedSutAsync(params string[] names)
    {
        foreach (var n in names) _query.HomeRows.Add(SampleRow(n));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        return sut;
    }

    [Fact]
    public async Task Plain_click_single_selects()
    {
        var sut = await LoadedSutAsync("a", "b");

        sut.SelectInteractive(sut.Rows[0], ctrl: false, shift: false);

        sut.SelectedRow.Should().Be(sut.Rows[0]);
        sut.Rows[0].IsSelected.Should().BeTrue();
        sut.Rows[1].IsSelected.Should().BeFalse();
        sut.HasMultiSelection.Should().BeFalse();
    }

    [Fact]
    public async Task Ctrl_click_toggles_membership_and_nulls_SelectedRow_at_two()
    {
        var sut = await LoadedSutAsync("a", "b");
        sut.SelectInteractive(sut.Rows[0], ctrl: false, shift: false);

        sut.SelectInteractive(sut.Rows[1], ctrl: true, shift: false);

        sut.HasMultiSelection.Should().BeTrue();
        sut.SelectedRow.Should().BeNull();           // 2+ → workspace chiuso (§C.2)
        sut.Rows[0].IsSelected.Should().BeTrue();
        sut.Rows[1].IsSelected.Should().BeTrue();

        sut.SelectInteractive(sut.Rows[0], ctrl: true, shift: false);   // toggle off
        sut.HasMultiSelection.Should().BeFalse();
        sut.SelectedRow.Should().Be(sut.Rows[1]);    // tornati a 1 → workspace riapre
    }

    [Fact]
    public async Task Shift_click_selects_the_range_from_the_anchor()
    {
        var sut = await LoadedSutAsync("a", "b", "c", "d");
        sut.SelectInteractive(sut.Rows[1], ctrl: false, shift: false);  // anchor = b

        sut.SelectInteractive(sut.Rows[3], ctrl: false, shift: true);

        sut.Rows.Where(r => r.IsSelected).Should().BeEquivalentTo(new[] { sut.Rows[1], sut.Rows[2], sut.Rows[3] });
        sut.SelectedRow.Should().BeNull();
    }

    [Fact]
    public async Task ClearSelection_and_plain_SelectedRow_assignment_reset_the_set()
    {
        var sut = await LoadedSutAsync("a", "b");
        sut.SelectInteractive(sut.Rows[0], ctrl: false, shift: false);
        sut.SelectInteractive(sut.Rows[1], ctrl: true, shift: false);

        sut.ClearSelection();
        sut.HasMultiSelection.Should().BeFalse();
        sut.Rows.Should().OnlyContain(r => !r.IsSelected);

        sut.SelectInteractive(sut.Rows[0], ctrl: true, shift: false);
        sut.SelectInteractive(sut.Rows[1], ctrl: true, shift: false);
        sut.SelectedRow = sut.Rows[0];               // assegnazione diretta = selezione singola
        sut.HasMultiSelection.Should().BeFalse();
        sut.Rows[1].IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task Row_archive_on_a_multi_selected_row_archives_all_selected()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow("a", a));
        _query.HomeRows.Add(SampleRow("b", b));
        _query.HomeRows.Add(SampleRow("c"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectInteractive(sut.Rows.Single(r => r.HabitNodeId == a), ctrl: false, shift: false);
        sut.SelectInteractive(sut.Rows.Single(r => r.HabitNodeId == b), ctrl: true, shift: false);
        var reloadsBefore = _query.HomeCallCount;

        await sut.Rows.Single(r => r.HabitNodeId == a).ArchiveCommand.ExecuteAsync(null);

        _nodes.ArchivedCalls.Should().BeEquivalentTo(new[] { (a, true), (b, true) });
        _query.HomeCallCount.Should().Be(reloadsBefore + 1);   // un solo reload
        sut.HasMultiSelection.Should().BeFalse();              // selezione azzerata dopo il bulk
    }

    [Fact]
    public async Task Row_archive_on_a_row_outside_the_selection_stays_single()
    {
        var a = Guid.NewGuid();
        var c = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow("a", a));
        _query.HomeRows.Add(SampleRow("b"));
        _query.HomeRows.Add(SampleRow("c", c));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectInteractive(sut.Rows[0], ctrl: false, shift: false);
        sut.SelectInteractive(sut.Rows[1], ctrl: true, shift: false);

        await sut.Rows.Single(r => r.HabitNodeId == c).ArchiveCommand.ExecuteAsync(null);

        _nodes.ArchivedCalls.Should().BeEquivalentTo(new[] { (c, true) });
    }

    [Fact]
    public async Task Bulk_move_moves_every_selected_row_and_skips_cycles()
    {
        var g = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow("G", g, type: null));
        _query.HomeRows.Add(SampleRow("a", a));
        _query.HomeRows.Add(SampleRow("b", b));
        _nodes.Store[a] = new Recordum.Core.Models.HabitNode { Id = a, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "a", Color = "#000000" };
        _nodes.Store[b] = new Recordum.Core.Models.HabitNode { Id = b, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "b", Color = "#000000" };
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var rowA = sut.Rows.Single(r => r.HabitNodeId == a);
        sut.SelectInteractive(rowA, ctrl: false, shift: false);
        sut.SelectInteractive(sut.Rows.Single(r => r.HabitNodeId == b), ctrl: true, shift: false);

        var handled = await sut.TryBulkMoveAsync(rowA, g);

        handled.Should().BeTrue();
        _nodes.MoveCalls.Should().BeEquivalentTo(new[] { (a, (Guid?)g), (b, (Guid?)g) });
    }

    [Fact]
    public async Task Reload_preserves_the_multi_selection_set()
    {
        var sut = await LoadedSutAsync("a", "b", "c");
        sut.SelectInteractive(sut.Rows[0], ctrl: false, shift: false);
        sut.SelectInteractive(sut.Rows[2], ctrl: true, shift: false);

        await sut.LoadCommand.ExecuteAsync(null);    // reload ricrea i row VM

        sut.Rows.Count(r => r.IsSelected).Should().Be(2);
        sut.HasMultiSelection.Should().BeTrue();
    }
}
