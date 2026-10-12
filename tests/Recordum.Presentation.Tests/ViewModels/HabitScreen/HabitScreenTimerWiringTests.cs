using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

/// <summary>Parametro muto (CLAUDE.md 7): la schermata deve passare il bus ai quick-add a serie, altrimenti il
/// cronometro di cella resta "in corso" quando il timer si ferma altrove (la barra di stato).</summary>
public class HabitScreenTimerWiringTests
{
    [Theory]
    [InlineData(HabitType.Counter)]
    [InlineData(HabitType.State)]
    public async Task The_screen_wires_the_bus_into_the_series_quick_add(HabitType type)
    {
        var sut = HabitScreenViewModelTests.CreateSut(out var nodes, out _, out var timers, out var metrics, out _, out var bus);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = type, Name = "x" };
        var reps = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = id, Name = "Reps", Type = MetricType.Integer, SortOrder = 0 };
        var tempo = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = id, Name = "Tempo", Type = MetricType.Seconds, SortOrder = 1 };
        metrics.Store[reps.Id] = reps;
        metrics.Store[tempo.Id] = tempo;
        timers.StopWithoutRecordingFactory = _ => 95;
        await sut.LoadAsync(id);

        var quickAdd = sut.QuickAdd.Should().BeAssignableTo<SeriesQuickAddViewModelBase>().Subject;
        var row = quickAdd is MeasuredQuickAddViewModel m ? m.Row : ((CounterSeriesQuickAddViewModel)quickAdd).Series[0];
        var cell = row.Cells.First(c => c.IsDuration);
        await quickAdd.ToggleChronoCommand.ExecuteAsync(cell);
        quickAdd.ChronoRunning.Should().BeTrue();

        timers.Active.Clear();   // la barra di stato l'ha registrato e rimosso
        bus.Publish(new TimerChangedPayload(id, WellKnownEvents.TimerStopped));
        for (var i = 0; i < 1000 && quickAdd.ChronoRunning; i++) await Task.Delay(1);

        quickAdd.ChronoRunning.Should().BeFalse("senza bus il cronometro di cella non sente lo stop");
        cell.IsChronoRunning.Should().BeFalse();
    }
}
