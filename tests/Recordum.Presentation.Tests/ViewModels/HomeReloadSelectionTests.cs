using System.Collections.Immutable;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Plugins.Contracts.Events;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels;

/// <summary>GIO-11 (selezione): se l'utente cambia scelta mentre un ricaricamento è in volo, la
/// fine del ricaricamento non ripristina la selezione letta prima dell'attesa.</summary>
public class HomeReloadSelectionTests
{
    private readonly FakeHabitTreeQueryService _query = new();
    private readonly FakeEventBus _bus = new();

    private HomeViewModel CreateSut() =>
        new(_query, new FakeHabitNodeService(), new FakePurgeService(), events: _bus);

    private static HomeTreeRow Row(Guid id, string name) =>
        new(id, null, name, "#000000", HabitType.Action, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty);

    private static async Task WaitForLoadedAsync(HomeViewModel sut)
    {
        for (var i = 0; i < 50 && sut.State != HomeState.Loaded; i++)
            await Task.Yield();
        await Task.Yield();
    }

    [Fact]
    public async Task Selection_changed_during_reload_keeps_the_new_selection()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        _query.HomeRows.Add(Row(idA, "A"));
        _query.HomeRows.Add(Row(idB, "B"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectedRow = sut.Rows.Single(r => r.HabitNodeId == idA);

        var gate = new TaskCompletionSource<bool>();
        _query.HomeGate = gate;
        _bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), idA, DateTime.UtcNow));
        _query.HomeGate = null;

        sut.SelectedRow = sut.Rows.Single(r => r.HabitNodeId == idB);

        gate.SetResult(true);
        await WaitForLoadedAsync(sut);

        sut.SelectedRow.Should().NotBeNull();
        sut.SelectedRow!.HabitNodeId.Should().Be(idB);
        sut.SelectedRow.Should().BeSameAs(sut.Rows.Single(r => r.HabitNodeId == idB));
    }

    [Fact]
    public async Task Selection_unchanged_during_reload_keeps_the_original_selection()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        _query.HomeRows.Add(Row(idA, "A"));
        _query.HomeRows.Add(Row(idB, "B"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectedRow = sut.Rows.Single(r => r.HabitNodeId == idA);

        var gate = new TaskCompletionSource<bool>();
        _query.HomeGate = gate;
        _bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), idA, DateTime.UtcNow));
        _query.HomeGate = null;

        gate.SetResult(true);
        await WaitForLoadedAsync(sut);

        sut.SelectedRow.Should().NotBeNull();
        sut.SelectedRow!.HabitNodeId.Should().Be(idA);
        sut.SelectedRow.Should().BeSameAs(sut.Rows.Single(r => r.HabitNodeId == idA));
    }

    [Fact]
    public async Task Selection_changed_to_a_removed_row_clears_selection()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        _query.HomeRows.Add(Row(idA, "A"));
        _query.HomeRows.Add(Row(idB, "B"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectedRow = sut.Rows.Single(r => r.HabitNodeId == idA);

        var gate = new TaskCompletionSource<bool>();
        _query.HomeGate = gate;
        _bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), idA, DateTime.UtcNow));
        _query.HomeGate = null;

        sut.SelectedRow = sut.Rows.Single(r => r.HabitNodeId == idB);
        _query.HomeRows.RemoveAll(r => r.HabitNodeId == idB);

        gate.SetResult(true);
        await WaitForLoadedAsync(sut);

        sut.SelectedRow.Should().BeNull();
    }

    [Fact]
    public async Task Selection_cleared_during_reload_stays_cleared()
    {
        var idA = Guid.NewGuid();
        _query.HomeRows.Add(Row(idA, "A"));
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.SelectedRow = sut.Rows.Single(r => r.HabitNodeId == idA);

        var gate = new TaskCompletionSource<bool>();
        _query.HomeGate = gate;
        _bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), idA, DateTime.UtcNow));
        _query.HomeGate = null;

        sut.SelectedRow = null;

        gate.SetResult(true);
        await WaitForLoadedAsync(sut);

        sut.SelectedRow.Should().BeNull();
    }
}
