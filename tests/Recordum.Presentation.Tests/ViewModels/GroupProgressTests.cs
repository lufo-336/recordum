using System.Collections.Immutable;
using System.Globalization;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels;

/// <summary>Il "x di y" del gruppo è uno solo, in Home e nella Panoramica.</summary>
public sealed class GroupProgressTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture, _uiCulture = CultureInfo.CurrentUICulture;
    private readonly Guid _group = Guid.NewGuid();
    private readonly FakeHabitTreeQueryService _tree = new();

    public GroupProgressTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("it-IT");
        _tree.HomeRows.Add(Row(_group, null, null));
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    private static HomeTreeRow Row(Guid id, Guid? parent, HabitType? type, bool today = false,
        AbstinenceStatus status = AbstinenceStatus.NotStarted)
        => new(id, parent, "n", "#112233", type, null, 0, today, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty, AbstinenceStatus: status);

    private void Add(HabitType type, bool today = false, AbstinenceStatus status = AbstinenceStatus.NotStarted)
        => _tree.HomeRows.Add(Row(Guid.NewGuid(), _group, type, today, status));

    private async Task<(string Home, string Group)> BothAsync()
    {
        var home = new HomeViewModel(_tree, new FakeHabitNodeService(), new FakePurgeService());
        await home.LoadCommand.ExecuteAsync(null);
        var vm = new PanoramicaViewModel(_tree, new FakeGoalService(), new FakeCompletionService(),
            new FakeMetricDefinitionService(), new FakeReferenceData(), new FakeClock());
        await vm.LoadAsync(new HabitNode { Id = _group, NodeType = NodeType.Group, Name = "G", Color = "#808080" }, DayBoundary.Utc);
        return (home.Rows.Single(r => r.HabitNodeId == _group).GroupProgressText, vm.TodayLabel);
    }

    [Fact]
    public async Task Abstaining_child_counts_as_done_in_home_and_group()
    {
        Add(HabitType.Action, today: true);
        Add(HabitType.Resistance, status: AbstinenceStatus.Abstaining);
        Add(HabitType.Action);

        var (home, group) = await BothAsync();

        home.Should().Be("2 di 3");
        group.Should().Be("Oggi 2/3");
    }

    [Fact]
    public async Task Relapsed_child_is_not_done()
    {
        Add(HabitType.Action, today: true);
        Add(HabitType.Resistance, status: AbstinenceStatus.Relapsed);

        var (home, group) = await BothAsync();

        home.Should().Be("1 di 2");
        group.Should().Be("Oggi 1/2");
    }

    [Fact]
    public async Task Never_started_abstinence_is_out_of_the_total()
    {
        Add(HabitType.Action, today: true);
        Add(HabitType.Resistance, status: AbstinenceStatus.NotStarted);

        var (home, group) = await BothAsync();

        home.Should().Be("1 di 1");
        group.Should().Be("Oggi 1/1");
    }

    [Fact]
    public async Task Today_fraction_follows_the_same_count()
    {
        Add(HabitType.Resistance, status: AbstinenceStatus.Abstaining);
        Add(HabitType.Action);
        var vm = new PanoramicaViewModel(_tree, new FakeGoalService(), new FakeCompletionService(),
            new FakeMetricDefinitionService(), new FakeReferenceData(), new FakeClock());
        await vm.LoadAsync(new HabitNode { Id = _group, NodeType = NodeType.Group, Name = "G", Color = "#808080" }, DayBoundary.Utc);
        vm.TodayFraction.Should().Be(0.5);
    }
}
