using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Plugins.Contracts.Events;
using Recordum.Presentation.Tests.Fixtures;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

/// <summary>GIO-23: modificare una scala in Configura (nomi, estremi) ricostruisce il quick-add della schermata aperta.</summary>
public sealed class ScaleEditRebuildsQuickAddTests
{
    private static readonly DayBoundary Boundary = new(TimeZoneInfo.Utc, new TimeOnly(5, 0));

    [Fact]
    public async Task An_open_screen_rebuilds_its_quick_add_when_the_scale_it_reads_changes()
    {
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero), Boundary);
        var scale = new ScaleTemplate
        {
            Id = Guid.NewGuid(), Code = "mia", Name = "Mia", MinValue = 1, MaxValue = 3,
            Anchors = [new ScaleAnchor { Value = 1, Label = "a" }, new ScaleAnchor { Value = 3, Label = "c" }],
        };
        rig.RefData.Scales.Add(scale);
        var id = Guid.NewGuid();
        rig.Nodes.Store[id] = new HabitNode
        {
            Id = id, NodeType = NodeType.Habit, HabitType = HabitType.State, Name = "h",
            CreatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        };
        rig.Metrics.Store[Guid.NewGuid()] = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = id, Name = "m", Type = MetricType.Decimal, ScaleTemplateId = scale.Id };
        await rig.Vm.LoadAsync(id);
        var before = rig.Vm.QuickAdd;
        before.Should().NotBeNull();

        // Configura salva la scala (qui: lo stesso oggetto cambia sul posto, come fa il servizio nel DB) e il servizio lo dice.
        scale.Anchors[1].Label = "tre";
        rig.Bus.Publish(new HabitUpdatedPayload(id));
        await Task.Delay(150);

        rig.Vm.QuickAdd.Should().NotBeSameAs(before);
    }

    [Fact]
    public async Task A_rename_of_the_habit_alone_keeps_the_same_quick_add()
    {
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero), Boundary);
        var scale = new ScaleTemplate { Id = Guid.NewGuid(), Code = "mia", Name = "Mia", MinValue = 1, MaxValue = 3 };
        rig.RefData.Scales.Add(scale);
        var id = Guid.NewGuid();
        rig.Nodes.Store[id] = new HabitNode
        {
            Id = id, NodeType = NodeType.Habit, HabitType = HabitType.State, Name = "h",
            CreatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        };
        rig.Metrics.Store[Guid.NewGuid()] = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = id, Name = "m", Type = MetricType.Decimal, ScaleTemplateId = scale.Id };
        await rig.Vm.LoadAsync(id);
        var before = rig.Vm.QuickAdd;

        rig.Bus.Publish(new HabitUpdatedPayload(id));
        await Task.Delay(150);

        rig.Vm.QuickAdd.Should().BeSameAs(before);
    }
}
