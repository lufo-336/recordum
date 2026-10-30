using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Presentation.Tests.Fixtures;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.Figures;

public class HabitScreenFiguresWiringTests
{
    [Fact]
    public async Task Figures_load_with_the_subject_and_follow_completions()
    {
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero));
        var id = Guid.NewGuid();
        rig.Nodes.Store[id] = new HabitNode
        {
            Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "acqua",
            CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        };

        await rig.Vm.LoadAsync(id);
        rig.Vm.Figures.Items.Should().HaveCount(4);
        rig.Vm.Figures.Items[0].Value.Should().Be("0");

        rig.Completions.HeatmapByHabit[id] = [new HeatmapCell(new DateOnly(2026, 10, 28), 3)];
        rig.Bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), id, new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc)));
        await WaitUntilAsync(() => rig.Vm.Figures.Items[0].Value == "3");

        rig.Vm.Figures.Items[0].Value.Should().Be("3");
    }

    [Fact]
    public async Task A_group_has_no_figures()
    {
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero));
        var id = Guid.NewGuid();
        rig.Nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Group, Name = "g", CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc) };

        await rig.Vm.LoadAsync(id);

        rig.Vm.Figures.Items.Should().BeEmpty();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(10);
        condition().Should().BeTrue("the async event handler should have reloaded in time");
    }

    [Fact]
    public async Task A_group_loads_group_figures_and_follows_a_childs_completion()
    {
        var history = new FakeGroupHistoryQueryService();
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero), groupHistory: history);
        var id = Guid.NewGuid();
        rig.Nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Group, Name = "g", CreatedAt = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc) };

        await rig.Vm.LoadAsync(id);
        rig.Vm.GroupFigures!.Items.Select(f => f.Value).Should().Equal("0", "0%");

        var today = new DateOnly(2026, 10, 28);
        history.Children.Add(new GroupChildHistory(Guid.NewGuid(), "a", "#112233", HabitType.Action, 0, today,
            System.Collections.Immutable.ImmutableHashSet.Create(today), System.Collections.Immutable.ImmutableHashSet.Create(today)));
        rig.Bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc)));
        await WaitUntilAsync(() => rig.Vm.GroupFigures.Items[0].Value == "1");

        rig.Vm.GroupFigures.Items.Select(f => f.Value).Should().Equal("1", "100%");
    }

    [Fact]
    public async Task A_childs_completion_refreshes_the_group_count_and_goal()
    {
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero),
            groupHistory: new FakeGroupHistoryQueryService());
        var group = Guid.NewGuid();
        var child = Guid.NewGuid();
        rig.Nodes.Store[group] = new HabitNode { Id = group, NodeType = NodeType.Group, Name = "g", CreatedAt = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc) };
        rig.Goals.Store[Guid.NewGuid()] = new Goal { HabitNodeId = group, Name = "obiettivo", TargetValue = 5, PeriodType = GoalPeriodType.Rolling, RollingDays = 7 };
        rig.Tree.HomeRows.Add(Row(group, null, null, false));
        rig.Tree.HomeRows.Add(Row(child, group, HabitType.Action, false));

        await rig.Vm.LoadAsync(group);
        rig.Vm.Panoramica.TodayLabel.Should().Contain("0/1");
        rig.Vm.GroupFigures!.Items.Last().Value.Should().Contain("0");

        rig.Tree.HomeRows[1] = Row(child, group, HabitType.Action, true);
        rig.Completions.Store[Guid.NewGuid()] = new Completion
        {
            HabitNodeId = group, CompletedAtMs = new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        };
        rig.Bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), child, new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc)));
        await WaitUntilAsync(() => rig.Vm.Panoramica.TodayLabel.Contains("1/1"));

        rig.Vm.Panoramica.TodayFraction.Should().Be(1);
        rig.Vm.GroupFigures.Items.Last().Value.Should().MatchRegex("^1 \\S+ 5$");
    }

    private static HomeTreeRow Row(Guid id, Guid? parent, HabitType? type, bool today)
        => new(id, parent, "n", "#112233", type, null, 0, today, PinScope.None, 0, false,
            System.Collections.Immutable.ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), System.Collections.Immutable.ImmutableArray<Guid>.Empty);
}
