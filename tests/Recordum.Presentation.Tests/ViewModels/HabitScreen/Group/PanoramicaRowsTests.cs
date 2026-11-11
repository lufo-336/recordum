using System.Collections.Immutable;
using System.Globalization;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Tests.Abstractions;
using Recordum.Plugins.Contracts.Events;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Quick;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.Group;

/// <summary>Le figlie di un gruppo come righe della Home: stesso comando di registrazione rapida. Orologio al 28/10/2026 10:00 UTC, it-IT.</summary>
public sealed class PanoramicaRowsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture, _uiCulture = CultureInfo.CurrentUICulture;
    private readonly Guid _group = Guid.NewGuid();
    private readonly FakeGroupHistoryQueryService _history = new();

    public PanoramicaRowsTests() => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("it-IT");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    private static HomeTreeRow Row(Guid id, Guid? parent, string name, HabitType? type, bool today = false, bool archived = false,
        AbstinenceStatus status = AbstinenceStatus.NotStarted, long? since = null)
        => new(id, parent, name, "#112233", type, null, 0, today, PinScope.None, 0, archived,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty,
            AbstinenceStatus: status, AbstinenceOpenSinceMs: since);

    private HabitScreenRig CreateRig(QuickActionHandlerSelector? selector = null, FakeTickSourceFactory? ticks = null)
    {
        var rig = HabitScreenViewModelFactory.CreateRig(Now, groupHistory: _history, quickActions: selector, ticks: ticks);
        rig.Nodes.Store[_group] = new HabitNode { Id = _group, NodeType = NodeType.Group, Name = "G", CreatedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc) };
        rig.Tree.HomeRows.Add(Row(_group, null, "G", null));
        return rig;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(10);
        condition().Should().BeTrue("the async event handler should have reloaded in time");
    }

    [Fact]
    public async Task Rows_are_home_rows_in_tree_order()
    {
        var rig = CreateRig();
        var sub = Guid.NewGuid();
        rig.Tree.HomeRows.Add(Row(Guid.NewGuid(), _group, "diretto", HabitType.Action));
        rig.Tree.HomeRows.Add(Row(sub, _group, "Sub", null));
        rig.Tree.HomeRows.Add(Row(Guid.NewGuid(), sub, "annidato", HabitType.Counter));
        rig.Tree.HomeRows.Add(Row(Guid.NewGuid(), null, "estraneo", HabitType.Action));
        rig.Tree.HomeRows.Add(Row(Guid.NewGuid(), _group, "vecchio", HabitType.Action, archived: true));

        await rig.Vm.LoadAsync(_group);

        var rows = rig.Vm.Panoramica.Rows;
        rows.Select(r => r.Name).Should().Equal("diretto", "Sub", "annidato");
        rows.Select(r => r.IndentLevel).Should().Equal(0, 0, 1);
        rows.Single(r => r.Name == "Sub").IsGroup.Should().BeTrue();
        // La lista e il conto in testa concordano: l'archiviata non c'è in nessuno dei due.
        rig.Vm.Panoramica.Children.Select(c => c.Name).Should().Equal("diretto", "Sub", "annidato");
        rig.Vm.Panoramica.TotalChildren.Should().Be(2);
    }

    [Fact]
    public async Task Quick_log_records_and_refreshes()
    {
        var completions = new FakeCompletionService();
        var rig = CreateRig(new QuickActionHandlerSelector([new ActionQuickActionHandler(completions, new FakeClock(Now), new FakeResetTimeResolver())]));
        var child = Guid.NewGuid();
        rig.Tree.HomeRows.Add(Row(child, _group, "corsa", HabitType.Action));
        await rig.Vm.LoadAsync(_group);
        rig.Vm.Panoramica.TodayLabel.Should().Contain("0/1");
        rig.Vm.GroupFigures!.Items[0].Value.Should().Be("0");

        await rig.Vm.Panoramica.Rows.Single(r => r.HabitNodeId == child).QuickActionCommand.ExecuteAsync(null);

        completions.LastRecord!.HabitNodeId.Should().Be(child);
        // Il bus annuncia la registrazione: il conto e i numeri del gruppo si rileggono.
        rig.Tree.HomeRows[1] = Row(child, _group, "corsa", HabitType.Action, today: true);
        var today = new DateOnly(2026, 10, 28);
        _history.Children.Add(new GroupChildHistory(child, "corsa", "#112233", HabitType.Action, 0, today,
            ImmutableHashSet.Create(today), ImmutableHashSet.Create(today)));
        rig.Bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), child, new DateTime(2026, 10, 28, 10, 0, 0, DateTimeKind.Utc)));
        await WaitUntilAsync(() => rig.Vm.Panoramica.TodayLabel.Contains("1/1") && rig.Vm.GroupFigures.Items[0].Value == "1");

        rig.Vm.Panoramica.Rows.Single(r => r.HabitNodeId == child).IsCompletedToday.Should().BeTrue();
    }

    [Fact]
    public async Task Form_types_open_the_child()
    {
        var completions = new FakeCompletionService();
        var metrics = new FakeMetricDefinitionService();
        var child = Guid.NewGuid();
        var field = new MetricDefinition { Id = Guid.NewGuid(), HabitNodeId = child, Name = "km", Type = MetricType.Integer, SortOrder = 0 };
        metrics.Store[field.Id] = field;
        var rig = CreateRig(new QuickActionHandlerSelector([new CounterQuickActionHandler(completions, metrics, new FakeClock(Now), new FakeResetTimeResolver())]));
        rig.Tree.HomeRows.Add(Row(child, _group, "corsa", HabitType.Counter));
        await rig.Vm.LoadAsync(_group);
        var opened = new List<Guid>();
        rig.Vm.Panoramica.ChildSelected += (_, id) => opened.Add(id);

        await rig.Vm.Panoramica.Rows.Single(r => r.HabitNodeId == child).QuickActionCommand.ExecuteAsync(null);

        opened.Should().Equal(child);
        completions.LastRecord.Should().BeNull();
    }

    [Fact]
    public async Task Abstinence_row_has_the_live_clock()
    {
        var ticks = new FakeTickSourceFactory();
        var rig = CreateRig(ticks: ticks);
        var child = Guid.NewGuid();
        rig.Tree.HomeRows.Add(Row(child, _group, "senza", HabitType.Resistance,
            status: AbstinenceStatus.Abstaining, since: Now.AddHours(-1).ToUnixTimeMilliseconds()));
        await rig.Vm.LoadAsync(_group);
        var row = rig.Vm.Panoramica.Rows.Single();
        ticks.RunningCount.Should().Be(1);
        var before = row.KeyFigureText;

        rig.Clock.Advance(TimeSpan.FromSeconds(30));
        ticks.TickAll();

        row.KeyFigureText.Should().NotBeNullOrEmpty().And.NotBe(before);
        // Una ricarica non lascia battiti doppi, e lo smontaggio li ferma.
        await rig.Vm.LoadAsync(_group);
        ticks.RunningCount.Should().Be(1);
        rig.Vm.Dispose();
        ticks.RunningCount.Should().Be(0);
    }

    [Fact]
    public async Task A_stale_reload_never_overwrites_a_newer_one()
    {
        var rig = CreateRig();
        rig.Tree.HomeRows.Add(Row(Guid.NewGuid(), _group, "uno", HabitType.Action));
        await rig.Vm.LoadAsync(_group);
        var panoramica = rig.Vm.Panoramica;
        var group = rig.Nodes.Store[_group];

        // Il primo caricamento resta fermo sul cancello; il secondo vede due figlie e finisce prima.
        rig.Tree.HomeGate = new TaskCompletionSource<bool>();
        var slow = panoramica.LoadAsync(group, Recordum.Core.Time.DayBoundary.Utc);
        var due = Row(Guid.NewGuid(), _group, "due", HabitType.Action);
        rig.Tree.HomeRows.Add(due);
        var gate = rig.Tree.HomeGate;
        rig.Tree.HomeGate = null;
        await panoramica.LoadAsync(group, Recordum.Core.Time.DayBoundary.Utc);
        // Il vecchio caricamento si risveglia dopo, e vede un albero più vecchio.
        rig.Tree.HomeRows.RemoveAt(rig.Tree.HomeRows.Count - 1);
        gate.SetResult(true);
        await slow;

        panoramica.Rows.Select(r => r.Name).Should().Equal("due", "uno");
        panoramica.TotalChildren.Should().Be(2);
    }
}
