using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

public class HomeSortManualTests
{
    private readonly FakeHabitTreeQueryService _query = new();
    private readonly FakeHabitNodeService _nodes = new();
    private readonly FakePurgeService _purge = new();

    private HomeViewModel CreateSut() => new(_query, _nodes, _purge);

    private static HomeTreeRow Row(string name, int sortOrder) =>
        new(Guid.NewGuid(), null, name, "#000000", HabitType.Action, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty,
            SortOrder: sortOrder);

    [Fact]
    public async Task Switching_to_Manual_orders_rows_by_SortOrder()
    {
        // Names chosen so alphabetical order (Name sort) differs from SortOrder order:
        // "zeta" sits first by SortOrder, last alphabetically.
        _query.HomeRows.Add(Row("zeta", 0));
        _query.HomeRows.Add(Row("alpha", 1));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        // Start in a non-Manual sort, then flip to Manual to prove the transition re-sorts.
        sut.SortOption = HomeSort.Name;
        sut.Rows.Select(r => r.Name).Should().Equal("alpha", "zeta");

        sut.SortOption = HomeSort.Manual;
        sut.Rows.Select(r => r.Name).Should().Equal("zeta", "alpha");
    }
}
