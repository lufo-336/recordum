using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.QuickAdd;

/// <summary>AsyncRelayCommand.ExecuteAsync non consulta CanExecute: un secondo SaveAsync mentre il primo e' in
/// volo non deve registrare di nuovo (la guardia sta nel metodo, non solo nel comando).</summary>
public class QuickAddDoubleSaveTests
{
    private static readonly HabitNode Node = new() { Id = Guid.NewGuid(), HabitType = HabitType.Action };

    private static async Task AssertSavesOnce(Func<FakeCompletionService, FakeClock, QuickAddViewModelBase> make)
    {
        var cs = new FakeCompletionService();
        var gate = new TaskCompletionSource();
        cs.RecordGate = gate.Task;
        var sut = make(cs, new FakeClock(new DateTimeOffset(2026, 5, 21, 14, 0, 0, TimeSpan.Zero)));
        sut.Node = Node;

        var first = sut.SaveAsync();
        var second = sut.SaveAsync();   // il doppio clic
        gate.SetResult();
        await Task.WhenAll(first, second);

        cs.RecordCalls.Should().Be(1);
    }

    [Fact]
    public Task Measured_save_records_once() =>
        AssertSavesOnce((cs, c) => new MeasuredQuickAddViewModel(cs, new FakeMetricDefinitionService(), new FakeReferenceData(), c, new FakeActiveTimerService()));

    [Fact]
    public async Task Counter_series_save_records_once()
    {
        var metrics = new FakeMetricDefinitionService();
        var m = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = Node.Id, Name = "Reps", Type = MetricType.Integer };
        metrics.Store[m.Id] = m;
        var cs = new FakeCompletionService();
        var gate = new TaskCompletionSource();
        cs.RecordGate = gate.Task;
        var sut = new CounterSeriesQuickAddViewModel(cs, metrics, new FakeReferenceData(),
            new FakeClock(new DateTimeOffset(2026, 5, 21, 14, 0, 0, TimeSpan.Zero)), new FakeActiveTimerService(), new FakeHabitConfigService())
        { Node = Node };
        await sut.LoadMetricsAsync();
        sut.Series[0].Cells[0].IntegerValue = 3;

        var first = sut.SaveAsync();
        var second = sut.SaveAsync();
        gate.SetResult();
        await Task.WhenAll(first, second);

        cs.RecordCalls.Should().Be(1);
    }

    [Fact]
    public Task Action_save_records_once() => AssertSavesOnce((cs, c) => new ActionQuickAddViewModel(cs, c));

    [Fact]
    public Task Counter_tap_records_once() => AssertSavesOnce((cs, c) => new CounterTapQuickAddViewModel(cs, c));
}
