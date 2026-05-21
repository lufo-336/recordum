using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

public class HomeViewModelTests
{
    private readonly FakeHabitTreeQueryService _query = new();
    private readonly FakeHabitNodeService _nodes = new();
    private readonly FakePurgeService _purge = new();

    private HomeViewModel CreateSut() => new(_query, _nodes, _purge);

    [Fact]
    public async Task LoadAsync_populates_rows_for_Active_tab_excluding_archived()
    {
        _query.HomeRows.Add(SampleRow(name: "Live"));
        _query.HomeRows.Add(SampleRow(name: "Archived", archived: true));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.State.Should().Be(HomeState.Loaded);
        sut.Rows.Should().ContainSingle(r => r.Name == "Live");
    }

    [Fact]
    public async Task SwitchTab_to_Archived_re_queries_and_filters()
    {
        _query.HomeRows.Add(SampleRow(name: "Live"));
        _query.HomeRows.Add(SampleRow(name: "Old", archived: true));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Archived);

        sut.Rows.Select(r => r.Name).Should().ContainSingle().Which.Should().Be("Old");
    }

    [Fact]
    public async Task SwitchTab_to_Trash_uses_GetTrashTreeAsync()
    {
        _query.TrashRows.Add(SampleRow(name: "Dead"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Trash);

        sut.Rows.Should().ContainSingle().Which.Name.Should().Be("Dead");
        _query.TrashCallCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_sets_Error_on_service_exception()
    {
        _query.NextException = new InvalidOperationException("db down");
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.State.Should().Be(HomeState.Error);
    }

    [Fact]
    public async Task RetryCommand_after_Error_loads_again()
    {
        _query.NextException = new InvalidOperationException("first");
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        _query.HomeRows.Add(SampleRow(name: "OK"));
        await sut.RetryCommand.ExecuteAsync(null);

        sut.State.Should().Be(HomeState.Loaded);
        sut.Rows.Should().ContainSingle().Which.Name.Should().Be("OK");
    }

    [Fact]
    public async Task LoadAsync_with_no_rows_sets_state_to_Empty()
    {
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.State.Should().Be(HomeState.Empty);
        sut.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task SwitchTab_to_same_tab_does_not_re_query()
    {
        _query.HomeRows.Add(SampleRow(name: "x"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var firstCallCount = _query.HomeCallCount;

        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Active);

        _query.HomeCallCount.Should().Be(firstCallCount);
    }

    [Fact]
    public async Task TypeFilter_excludes_other_types()
    {
        _query.HomeRows.Add(SampleRow(name: "a", type: HabitType.Action));
        _query.HomeRows.Add(SampleRow(name: "c", type: HabitType.Counter));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.ToggleTypeFilterCommand.Execute(HabitType.Action);

        sut.Rows.Select(r => r.Name).Should().ContainSingle().Which.Should().Be("a");
    }

    [Fact]
    public async Task SearchText_filters_by_name_case_insensitive_contains()
    {
        _query.HomeRows.Add(SampleRow(name: "Morning yoga"));
        _query.HomeRows.Add(SampleRow(name: "Read book"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.SearchText = "YOGA";

        sut.Rows.Select(r => r.Name).Should().ContainSingle().Which.Should().Be("Morning yoga");
    }

    [Fact]
    public async Task HideCompletedToday_filters_completed_rows()
    {
        var done = SampleRow(name: "done") with { IsCompletedToday = true };
        var pending = SampleRow(name: "pending");
        _query.HomeRows.Add(done);
        _query.HomeRows.Add(pending);
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.HideCompletedToday = true;

        sut.Rows.Select(r => r.Name).Should().ContainSingle().Which.Should().Be("pending");
    }

    [Fact]
    public async Task TagFilter_keeps_only_rows_with_matching_tag()
    {
        var tagA = Guid.NewGuid();
        _query.HomeRows.Add(SampleRow(name: "tagged") with { TagIds = ImmutableArray.Create(tagA) });
        _query.HomeRows.Add(SampleRow(name: "untagged"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.TagFilter = ImmutableHashSet.Create(tagA);

        sut.Rows.Select(r => r.Name).Should().ContainSingle().Which.Should().Be("tagged");
    }

    [Fact]
    public async Task TabSwitch_resets_all_filters()
    {
        _query.HomeRows.Add(SampleRow(name: "a"));
        _query.HomeRows.Add(SampleRow(name: "b", archived: true));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.SearchText = "a";
        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Archived);

        sut.SearchText.Should().BeNullOrEmpty();
        sut.Rows.Should().ContainSingle().Which.Name.Should().Be("b");
    }

    [Fact]
    public async Task Rebuild_sets_CurrentTab_on_each_row()
    {
        _query.HomeRows.Add(SampleRow(name: "a"));
        _query.HomeRows.Add(SampleRow(name: "b", archived: true));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.Rows.Should().OnlyContain(r => r.CurrentTab == HomeTab.Active);

        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Archived);
        sut.Rows.Should().OnlyContain(r => r.CurrentTab == HomeTab.Archived);
    }

    [Fact]
    public async Task PinScope_Global_appears_first_then_Local_then_others()
    {
        _query.HomeRows.Add(SampleRow(name: "n", id: NewId("n")));
        _query.HomeRows.Add(SampleRow(name: "l", id: NewId("l")) with { PinScope = PinScope.Local });
        _query.HomeRows.Add(SampleRow(name: "g", id: NewId("g")) with { PinScope = PinScope.Global });
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.Rows.Select(r => r.Name).Should().Equal("g", "l", "n");
    }

    [Fact]
    public async Task Sort_by_Name_orders_alphabetically_within_pin_layer()
    {
        _query.HomeRows.Add(SampleRow(name: "Charlie"));
        _query.HomeRows.Add(SampleRow(name: "alpha"));
        _query.HomeRows.Add(SampleRow(name: "bravo"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SortOption = HomeSort.Name;
        sut.Rows.Select(r => r.Name).Should().Equal("alpha", "bravo", "Charlie");
    }

    [Fact]
    public async Task Hierarchy_nests_children_under_parent_in_Active_tab()
    {
        var groupId = NewId("group");
        _query.HomeRows.Add(SampleRow(name: "G", id: groupId, type: null));
        _query.HomeRows.Add(SampleRow(name: "child1", parent: groupId));
        _query.HomeRows.Add(SampleRow(name: "child2", parent: groupId));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.Rows.Select(r => (r.Name, r.IndentLevel)).Should().Equal(
            ("G", 0), ("child1", 1), ("child2", 1));
    }

    [Fact]
    public async Task SelectRow_sets_SelectedRow()
    {
        _query.HomeRows.Add(SampleRow(name: "a"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        var row = sut.Rows.First();

        sut.SelectRowCommand.Execute(row);

        sut.SelectedRow.Should().BeSameAs(row);
    }

    [Fact]
    public async Task SelectRow_with_null_clears_selection()
    {
        _query.HomeRows.Add(SampleRow(name: "a"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectRowCommand.Execute(sut.Rows.First());

        sut.SelectRowCommand.Execute(null);

        sut.SelectedRow.Should().BeNull();
    }

    [Fact]
    public async Task SwitchTab_clears_SelectedRow()
    {
        _query.HomeRows.Add(SampleRow(name: "a"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectRowCommand.Execute(sut.Rows.First());

        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Archived);

        sut.SelectedRow.Should().BeNull();
    }

    [Fact]
    public async Task OpenNewHabit_launches_picker_and_selects_created_row()
    {
        var launcher = new FakeHabitTemplatePickerLauncher();
        var newId = Guid.NewGuid();
        launcher.NextResult = newId;
        _query.HomeRows.Add(SampleRow(name: "fresh", id: newId));
        var sut = new HomeViewModel(_query, _nodes, _purge, launcher);

        await sut.LoadCommand.ExecuteAsync(null);
        await sut.OpenNewHabitCommand.ExecuteAsync(null);

        launcher.PickCallCount.Should().Be(1);
        sut.SelectedRow.Should().NotBeNull();
        sut.SelectedRow!.HabitNodeId.Should().Be(newId);
    }

    [Fact]
    public async Task OpenNewHabit_with_no_launcher_is_a_noop()
    {
        var sut = CreateSut();
        await sut.OpenNewHabitCommand.ExecuteAsync(null);
        sut.SelectedRow.Should().BeNull();
    }

    [Fact]
    public async Task Hierarchy_is_flat_in_Archived_and_Trash()
    {
        var groupId = NewId("g");
        _query.HomeRows.Add(SampleRow(name: "G", id: groupId, type: null, archived: true));
        _query.HomeRows.Add(SampleRow(name: "c", parent: groupId, archived: true));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Archived);

        sut.Rows.All(r => r.IndentLevel == 0).Should().BeTrue();
    }

    private static HomeTreeRow SampleRow(string name = "x", bool archived = false, HabitType? type = HabitType.Action, Guid? id = null, Guid? parent = null) =>
        new(id ?? Guid.NewGuid(), parent, name, "#000000", type, null, 0, false, PinScope.None, 0, archived, ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);

    private static Guid NewId(string seed)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(seed.PadRight(16)[..16]);
        return new Guid(bytes);
    }
}
