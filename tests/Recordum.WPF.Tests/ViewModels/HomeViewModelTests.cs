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

    private static HomeTreeRow SampleRow(string name = "x", bool archived = false, HabitType? type = HabitType.Action, Guid? id = null, Guid? parent = null) =>
        new(id ?? Guid.NewGuid(), parent, name, "#000000", type, null, 0, false, PinScope.None, 0, archived, ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);
}
