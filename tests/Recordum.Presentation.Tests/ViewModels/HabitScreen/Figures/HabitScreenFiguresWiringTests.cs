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
        await Task.Delay(50);

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
        await Task.Delay(50);

        rig.Vm.GroupFigures.Items.Select(f => f.Value).Should().Equal("1", "100%");
    }
}
