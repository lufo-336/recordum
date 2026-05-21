using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class HabitScreenViewModelTests
{
    private static HabitScreenViewModel CreateSut(
        out FakeHabitNodeService nodes,
        out FakeCompletionService completions,
        out FakeActiveTimerService timers,
        out FakeMetricDefinitionService metrics,
        out FakeClock clock)
    {
        nodes = new FakeHabitNodeService();
        completions = new FakeCompletionService();
        timers = new FakeActiveTimerService();
        metrics = new FakeMetricDefinitionService();
        clock = new FakeClock(new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));
        return new HabitScreenViewModel(nodes, completions, timers, metrics, clock);
    }

    [Fact]
    public async Task LoadAsync_null_id_clears_state()
    {
        var sut = CreateSut(out _, out _, out _, out _, out _);

        await sut.LoadAsync(null);

        sut.HasNode.Should().BeFalse();
        sut.Name.Should().BeEmpty();
        sut.Node.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_loads_node_and_propagates_to_sub_vms()
    {
        var sut = CreateSut(out var nodes, out var completions, out _, out _, out _);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Yoga", Color = "#000000" };
        completions.HeatmapByHabit[id] = [new HeatmapCell(new DateOnly(2026, 5, 20), 1)];

        await sut.LoadAsync(id);

        sut.HasNode.Should().BeTrue();
        sut.Name.Should().Be("Yoga");
        sut.Heatmap.Cells.Should().NotBeEmpty();
        sut.Heatmap.NodeId.Should().Be(id);
        sut.CompletionList.NodeId.Should().Be(id);
    }

    [Fact]
    public async Task LoadAsync_unknown_id_clears_node()
    {
        var sut = CreateSut(out _, out _, out _, out _, out _);

        await sut.LoadAsync(Guid.NewGuid());

        sut.HasNode.Should().BeFalse();
    }

    [Fact]
    public async Task OpenConfigureCommand_raises_event()
    {
        var sut = CreateSut(out var nodes, out _, out _, out _, out _);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x" };
        await sut.LoadAsync(id);
        var fired = 0;
        sut.ConfigureRequested += (_, _) => fired++;

        sut.OpenConfigureCommand.Execute(null);

        fired.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_populates_CompletionList_rows_from_service()
    {
        var sut = CreateSut(out var nodes, out var completions, out _, out _, out _);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "x" };
        completions.Store[Guid.NewGuid()] = new Completion { Id = Guid.NewGuid(), HabitNodeId = id, CompletedAt = new DateTime(2026, 5, 21, 10, 0, 0, DateTimeKind.Utc), CompletedAtMs = 1 };
        completions.Store[Guid.NewGuid()] = new Completion { Id = Guid.NewGuid(), HabitNodeId = id, CompletedAt = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc), CompletedAtMs = 2 };

        await sut.LoadAsync(id);

        sut.CompletionList.Rows.Should().HaveCount(2);
    }
}
