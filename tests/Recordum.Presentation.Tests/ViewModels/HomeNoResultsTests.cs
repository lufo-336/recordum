using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels;

/// <summary>GIO-5: quando un filtro svuota la lista la Home entra in <see cref="HomeState.NoResults"/>,
/// tenendo visibili gli strumenti e offrendo <see cref="HomeViewModel.ClearFiltersCommand"/>.</summary>
public class HomeNoResultsTests
{
    private readonly FakeHabitTreeQueryService _query = new();

    private HomeViewModel CreateSut() =>
        new(_query, new FakeHabitNodeService(), new FakePurgeService());

    private static HomeTreeRow Row(string name, bool doneToday = false, bool archived = false) =>
        new(Guid.NewGuid(), null, name, "#000000", HabitType.Action, null, 0, doneToday, PinScope.None, 0, archived,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, doneToday ? 1 : 0), ImmutableArray<Guid>.Empty);

    [Fact]
    public async Task Two_active_rows_both_done_today_with_HideCompletedToday_enters_NoResults_and_ShowsTools()
    {
        _query.HomeRows.Add(Row("Corsa", doneToday: true));
        _query.HomeRows.Add(Row("Lettura", doneToday: true));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.HideCompletedToday = true;

        sut.Rows.Should().BeEmpty();
        sut.State.Should().Be(HomeState.NoResults);
        sut.ShowsTools.Should().BeTrue();
    }

    [Fact]
    public async Task ClearFilters_restores_Loaded_and_rows_without_touching_sort_tab_or_days_back()
    {
        _query.HomeRows.Add(Row("Corsa", doneToday: true));
        _query.HomeRows.Add(Row("Lettura", doneToday: true));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SortOption = HomeSort.Name;
        sut.DaysBack = 2;
        // Su un giorno passato le righe non sono "fatte": il filtro che svuota la lista qui è la ricerca.
        sut.SearchText = "inesistente";
        sut.State.Should().Be(HomeState.NoResults);

        sut.ClearFiltersCommand.Execute(null);

        sut.State.Should().Be(HomeState.Loaded);
        sut.ShowsTools.Should().BeTrue();
        sut.Rows.Should().HaveCount(2);
        sut.SearchText.Should().BeNullOrEmpty();
        sut.SortOption.Should().Be(HomeSort.Name);
        sut.ActiveTab.Should().Be(HomeTab.Active);
        sut.DaysBack.Should().Be(2);
    }

    [Fact]
    public async Task Search_with_no_matches_enters_NoResults()
    {
        _query.HomeRows.Add(Row("Corsa"));
        _query.HomeRows.Add(Row("Lettura"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.SearchText = "inesistente";

        sut.Rows.Should().BeEmpty();
        sut.State.Should().Be(HomeState.NoResults);
        sut.ShowsTools.Should().BeTrue();
    }

    [Fact]
    public async Task No_habits_and_no_filters_enters_Empty()
    {
        var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        sut.State.Should().Be(HomeState.Empty);
        sut.ShowsTools.Should().BeFalse();
    }

    [Fact]
    public async Task Empty_archived_tab_without_filters_enters_Empty()
    {
        _query.HomeRows.Add(Row("Corsa", archived: false));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        await sut.SwitchTabCommand.ExecuteAsync(HomeTab.Archived);

        sut.State.Should().Be(HomeState.Empty);
        sut.ShowsTools.Should().BeFalse();
    }
}
