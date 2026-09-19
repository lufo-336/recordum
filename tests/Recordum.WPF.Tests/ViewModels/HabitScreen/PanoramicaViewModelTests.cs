using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class PanoramicaViewModelTests
{
    private readonly FakeHabitTreeQueryService _tree = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero));

    private PanoramicaViewModel CreateSut() =>
        new(_tree, new FakeGoalService(), new FakeCompletionService(),
            new FakeMetricDefinitionService(), new FakeReferenceData(), _clock);

    private static HomeTreeRow Row(Guid id, string name, Guid? parent, HabitType? type = HabitType.Action, bool today = false) =>
        new(id, parent, name, "#112233", type, null, 0, today, PinScope.None, 0, false,
            ImmutableArray.Create(0, 1, 0, 1, 0, 1, 0), ImmutableArray<Guid>.Empty);

    private static HabitNode Group(Guid id) =>
        new() { Id = id, NodeType = NodeType.Group, Name = "G", Color = "#808080" };

    [Fact]
    public async Task Load_lists_children_hierarchically_including_subgroup_rows()
    {
        var g = Guid.NewGuid();
        var sub = Guid.NewGuid();
        var h1 = Guid.NewGuid();
        var h2 = Guid.NewGuid();
        var other = Guid.NewGuid();
        _tree.HomeRows.Add(Row(g, "G", null, type: null));
        _tree.HomeRows.Add(Row(h1, "diretto", g, today: true));
        _tree.HomeRows.Add(Row(sub, "Sub", g, type: null));
        _tree.HomeRows.Add(Row(h2, "annidato", sub));
        _tree.HomeRows.Add(Row(other, "estraneo", null));
        var sut = CreateSut();

        await sut.LoadAsync(Group(g), DayBoundary.Utc);

        sut.Children.Select(c => c.Name).Should().Equal("diretto", "Sub", "annidato");
        var subRow = sut.Children.Single(c => c.Name == "Sub");
        subRow.IsGroup.Should().BeTrue();
        subRow.Indent.Should().Be(0);
        sut.Children.Single(c => c.Name == "annidato").Indent.Should().Be(1);
        sut.Children.Single(c => c.Name == "diretto").IsGroup.Should().BeFalse();
        sut.TotalChildren.Should().Be(2);     // conta solo le habit
        sut.CompletedToday.Should().Be(1);
        sut.HasChildren.Should().BeTrue();
    }

    [Fact]
    public async Task Selecting_a_subgroup_row_raises_ChildSelected_with_its_id()
    {
        var g = Guid.NewGuid();
        var sub = Guid.NewGuid();
        _tree.HomeRows.Add(Row(g, "G", null, type: null));
        _tree.HomeRows.Add(Row(sub, "Sub", g, type: null));
        var sut = CreateSut();
        await sut.LoadAsync(Group(g), DayBoundary.Utc);
        Guid? selected = null;
        sut.ChildSelected += (_, id) => selected = id;

        sut.SelectChildCommand.Execute(sut.Children.Single());

        selected.Should().Be(sub);
    }

    [Fact]
    public async Task Load_with_no_descendants_reports_empty()
    {
        var g = Guid.NewGuid();
        _tree.HomeRows.Add(Row(g, "G", null, type: null));
        var sut = CreateSut();

        await sut.LoadAsync(Group(g), DayBoundary.Utc);

        sut.HasChildren.Should().BeFalse();
        sut.TotalChildren.Should().Be(0);
    }

    [Fact]
    public async Task Selecting_a_child_raises_ChildSelected()
    {
        var g = Guid.NewGuid();
        var h = Guid.NewGuid();
        _tree.HomeRows.Add(Row(g, "G", null, type: null));
        _tree.HomeRows.Add(Row(h, "figlio", g));
        var sut = CreateSut();
        await sut.LoadAsync(Group(g), DayBoundary.Utc);
        Guid? selected = null;
        sut.ChildSelected += (_, id) => selected = id;

        sut.SelectChildCommand.Execute(sut.Children.Single());

        selected.Should().Be(h);
    }
}
