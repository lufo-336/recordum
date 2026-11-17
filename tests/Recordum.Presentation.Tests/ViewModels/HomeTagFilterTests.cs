using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels;

/// <summary>GIO-22: i tag di un'abitudine si scelgono dal menu ⋯ della Home e filtrano le righe; la ricerca li legge.</summary>
public class HomeTagFilterTests
{
    private readonly FakeHabitTreeQueryService _query = new();
    private readonly FakeTagService _tags = new();
    private readonly Guid _health = Guid.NewGuid(), _home = Guid.NewGuid(), _unused = Guid.NewGuid();

    public HomeTagFilterTests()
    {
        _tags.Tags[_health] = new Tag { Id = _health, Name = "Salute" };
        _tags.Tags[_home] = new Tag { Id = _home, Name = "Casa" };
        _tags.Tags[_unused] = new Tag { Id = _unused, Name = "Vuoto" };
        _query.HomeRows.Add(Row("Corsa", _health));
        _query.HomeRows.Add(Row("Piatti", _home));
        _query.HomeRows.Add(Row("Lettura"));
    }

    private HomeViewModel CreateSut() =>
        new(_query, new FakeHabitNodeService(), new FakePurgeService(), tags: _tags);

    [Fact]
    public async Task The_menu_offers_all_and_the_tags_on_the_rows()
    {
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.HasTagOptions.Should().BeTrue();
        sut.TagOptions.Select(o => o.Name).Should().Equal(Strings.Home_TagMenu_All, "Casa", "Salute");
        sut.TagOptions[0].Id.Should().BeNull("la prima voce toglie il filtro");
        sut.TagOptions.Single(o => o.Id is null).IsSelected.Should().BeTrue("senza filtro è scelta \"tutti\"");
    }

    [Fact]
    public async Task Without_a_tag_service_or_without_tags_on_the_rows_there_is_no_menu()
    {
        var sut = new HomeViewModel(_query, new FakeHabitNodeService(), new FakePurgeService());
        await sut.LoadCommand.ExecuteAsync(null);
        sut.HasTagOptions.Should().BeFalse();

        _query.HomeRows.Clear();
        _query.HomeRows.Add(Row("Lettura"));
        var untagged = CreateSut();
        await untagged.LoadCommand.ExecuteAsync(null);
        untagged.HasTagOptions.Should().BeFalse("un tag che nessuna riga ha lascerebbe la lista vuota");
    }

    [Fact]
    public async Task Choosing_a_tag_filters_and_names_it_and_all_clears_it()
    {
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.ActiveTagName.Should().BeNull();

        sut.FilterByTagCommand.Execute(_health);

        sut.Rows.Select(r => r.Name).Should().Equal("Corsa");
        sut.ActiveTagName.Should().Be("Salute");
        sut.IsTagFilterOn.Should().BeTrue();
        sut.TagOptions.Single(o => o.IsSelected).Id.Should().Be(_health);

        sut.FilterByTagCommand.Execute(null);

        sut.Rows.Should().HaveCount(3);
        sut.ActiveTagName.Should().BeNull();
        sut.IsTagFilterOn.Should().BeFalse();
        sut.TagOptions.Single(o => o.IsSelected).Id.Should().BeNull();
    }

    [Fact]
    public async Task Search_finds_a_habit_by_the_name_of_its_tag()
    {
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.SearchText = "casa";

        sut.Rows.Select(r => r.Name).Should().Equal("Piatti");
    }

    [Fact]
    public async Task A_failing_tag_service_leaves_the_home_loaded()
    {
        _tags.NextException = new InvalidOperationException("boom");
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.State.Should().Be(HomeState.Loaded);
        sut.Rows.Should().HaveCount(3);
        sut.HasTagOptions.Should().BeFalse();
    }

    [Fact]
    public async Task A_filter_on_a_tag_gone_from_the_rows_is_cleared_on_reload()
    {
        // Altrimenti il filtro resta acceso senza un nome da mostrare né una voce per toglierlo.
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.FilterByTagCommand.Execute(_health);

        _query.HomeRows.RemoveAll(r => r.Name == "Corsa");
        await sut.LoadCommand.ExecuteAsync(null);

        sut.IsTagFilterOn.Should().BeFalse();
        sut.ActiveTagName.Should().BeNull();
        sut.Rows.Should().HaveCount(2);
    }

    private static HomeTreeRow Row(string name, params Guid[] tags) =>
        new(Guid.NewGuid(), null, name, "#000000", HabitType.Action, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray.Create(tags));
}
