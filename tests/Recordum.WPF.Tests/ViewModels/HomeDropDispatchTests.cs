using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

public class HomeDropDispatchTests
{
    private readonly FakeHabitTreeQueryService _query = new();
    private readonly FakeHabitNodeService _nodes = new();
    private readonly FakePurgeService _purge = new();

    private HomeViewModel CreateSut() => new(_query, _nodes, _purge);

    private static HomeTreeRow Row(string name, HabitType? type = HabitType.Action, Guid? parent = null) =>
        new(Guid.NewGuid(), parent, name, "#000000", type, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);

    [Fact]
    public async Task Reorder_drop_switches_sort_to_Manual_and_reorders()
    {
        _query.HomeRows.Add(Row("a"));
        _query.HomeRows.Add(Row("b"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SortOption = HomeSort.Name;

        var src = sut.Rows.Single(r => r.Name == "a");
        var target = sut.Rows.Single(r => r.Name == "b");
        var plan = new DropPlan(target.ParentId, target.HabitNodeId, true);

        await sut.ApplyDropAsync(src, plan);

        sut.SortOption.Should().Be(HomeSort.Manual);
        _nodes.ReorderCalls.Should().ContainSingle()
            .Which.Should().Be((src.HabitNodeId, target.ParentId, target.HabitNodeId, true));
    }

    [Fact]
    public async Task Reorder_drop_refetches_so_new_order_is_not_stale()
    {
        // Guards against sorting the cached _lastFetched snapshot after a reorder: switching to
        // Manual alone would Rebuild on stale rows (DB has the new SortOrder, the cache doesn't).
        // A reorder drop must re-fetch — assert a fresh GetHomeTreeAsync happened.
        _query.HomeRows.Add(Row("a"));
        _query.HomeRows.Add(Row("b"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SortOption = HomeSort.Name;
        var fetchesBefore = _query.HomeCallCount;

        var src = sut.Rows.Single(r => r.Name == "a");
        var target = sut.Rows.Single(r => r.Name == "b");
        await sut.ApplyDropAsync(src, new DropPlan(target.ParentId, target.HabitNodeId, true));

        _query.HomeCallCount.Should().BeGreaterThan(fetchesBefore);
    }

    [Fact]
    public async Task Onto_group_drop_appends_without_switching_sort()
    {
        _query.HomeRows.Add(Row("a"));
        _query.HomeRows.Add(Row("g", type: null));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SortOption = HomeSort.Name;

        var src = sut.Rows.Single(r => r.Name == "a");
        var group = sut.Rows.Single(r => r.Name == "g");
        var plan = new DropPlan(group.HabitNodeId, null, false);

        await sut.ApplyDropAsync(src, plan);

        sut.SortOption.Should().Be(HomeSort.Name);
        _nodes.ReorderCalls.Should().ContainSingle()
            .Which.Should().Be((src.HabitNodeId, group.HabitNodeId, (Guid?)null, false));
    }
}
