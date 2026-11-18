using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

/// <summary>GIO-10: la schermata di un'astinenza segue ciò che succede fuori da lei (Home, elenco dei periodi)
/// e ricarica una volta sola per gesto: l'unico segnale è il bus, che il servizio alza sempre.</summary>
public sealed class HabitScreenAbstinenceFollowTests
{
    private static readonly long StartMs =
        new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static async Task Eventually(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(10);
    }

    private static (HabitScreenViewModel Sut, Fixtures.FakeAbstinenceService Abstinence, Guid Id) Rig(bool abstaining)
    {
        var sut = HabitScreenViewModelTests.CreateSut(out var nodes, out _, out _, out _, out _, out _, out _, out var abstinence);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Resistance, Name = "NoSmoke" };
        abstinence.Periods[id] = abstaining
            ? [new AbstinencePeriod { Id = Guid.NewGuid(), HabitNodeId = id, StartedAtMs = StartMs, EndedAtMs = null }]
            : [];
        abstinence.States[id] = abstaining
            ? new AbstinenceState(AbstinenceStatus.Abstaining, StartMs, null)
            : new AbstinenceState(AbstinenceStatus.NotStarted, null, null);
        return (sut, abstinence, id);
    }

    [Fact]
    public async Task Start_from_the_Home_row_moves_the_open_screen_off_Start()
    {
        var (sut, abstinence, id) = Rig(abstaining: false);
        await sut.LoadAsync(id);
        var quick = (AbstinenceQuickAddViewModel)sut.QuickAdd!;
        quick.CanStart.Should().BeTrue();

        await abstinence.StartAsync(id); // la riga della Home chiama il servizio, che annuncia sul bus
        await Eventually(() => quick.CanLapse);

        quick.CanLapse.Should().BeTrue();
    }

    [Fact]
    public async Task Deleting_the_open_period_from_the_period_list_stops_the_clock()
    {
        var (sut, _, id) = Rig(abstaining: true);
        await sut.LoadAsync(id);
        var quick = (AbstinenceQuickAddViewModel)sut.QuickAdd!;
        quick.CanLapse.Should().BeTrue();
        var periods = sut.PeriodRecords.Periods;
        periods.ExcludedDay = null;
        await periods.LoadRangeAsync(id, null, null);

        await periods.Rows.Single(r => r.IsCurrent).DeleteCommand.ExecuteAsync(null);
        await Eventually(() => quick.Status == AbstinenceStatus.NotStarted);

        quick.Status.Should().Be(AbstinenceStatus.NotStarted);
    }

    [Fact]
    public async Task A_lapse_from_the_screen_reloads_it_once_like_one_from_outside()
    {
        var (sut, abstinence, id) = Rig(abstaining: false);
        await sut.LoadAsync(id);
        var quick = (AbstinenceQuickAddViewModel)sut.QuickAdd!;
        await Task.Delay(100);

        var before = abstinence.ListPeriodsCalls;
        await abstinence.StartAsync(id); // da fuori: solo il bus
        await Eventually(() => quick.CanLapse);
        await Task.Delay(100);
        var fromOutside = abstinence.ListPeriodsCalls - before;

        before = abstinence.ListPeriodsCalls;
        await quick.LapseCommand.ExecuteAsync(null); // dalla schermata: il bus e il segnale della casella
        await Task.Delay(100);
        var fromScreen = abstinence.ListPeriodsCalls - before;

        fromOutside.Should().BePositive();
        fromScreen.Should().Be(fromOutside);
    }
}
