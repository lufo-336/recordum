using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
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
    public async Task Manual_sort_orders_rows_by_SortOrder()
    {
        _query.HomeRows.Add(Row("second", 1));
        _query.HomeRows.Add(Row("first", 0));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.SortOption = HomeSort.Manual;

        sut.Rows.Select(r => r.Name).Should().Equal("first", "second");
    }
}
