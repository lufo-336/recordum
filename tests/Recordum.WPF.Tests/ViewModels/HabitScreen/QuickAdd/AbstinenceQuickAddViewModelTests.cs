using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.QuickAdd;

public class AbstinenceQuickAddViewModelTests
{
    private static readonly DateTimeOffset Epoch =
        new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

    private static (AbstinenceQuickAddViewModel vm, FakeAbstinenceService abstinence, FakeClock clock) Create()
    {
        var clock = new FakeClock(Epoch);
        var abstinence = new FakeAbstinenceService();
        var completions = new FakeCompletionService();
        var vm = new AbstinenceQuickAddViewModel(completions, abstinence, clock, null)
        {
            Node = new HabitNode { Id = Guid.NewGuid(), HabitType = HabitType.Resistance }
        };
        return (vm, abstinence, clock);
    }

    /// <summary>Load on a NotStarted habit → Status NotStarted, CanStart true, CanLapse false.</summary>
    [Fact]
    public async Task NotStarted_enables_start_only()
    {
        var (vm, _, _) = Create();
        // FakeAbstinenceService returns NotStarted by default when no state is seeded.

        await vm.LoadAsync(DayBoundary.Utc);

        vm.Status.Should().Be(AbstinenceStatus.NotStarted);
        vm.CanStart.Should().BeTrue();
        vm.CanLapse.Should().BeFalse();
    }

    /// <summary>After StartCommand → Status Abstaining, ElapsedDisplay non-empty, Changed raised.</summary>
    [Fact]
    public async Task Start_transitions_to_abstaining_and_raises_changed()
    {
        var (vm, abstinence, clock) = Create();
        await vm.LoadAsync(DayBoundary.Utc); // NotStarted

        // Pre-seed the state the fake service will return after Start.
        abstinence.States[vm.Node!.Id] = new AbstinenceState(
            AbstinenceStatus.Abstaining,
            clock.UtcNow.ToUnixTimeMilliseconds(),
            null);

        bool changedFired = false;
        vm.Changed += (_, _) => changedFired = true;

        await vm.StartCommand.ExecuteAsync(null);

        vm.Status.Should().Be(AbstinenceStatus.Abstaining);
        vm.ElapsedDisplay.Should().NotBeNullOrEmpty();
        changedFired.Should().BeTrue();
    }

    /// <summary>While Abstaining, LapseCommand → Status Relapsed, Changed raised.</summary>
    [Fact]
    public async Task Lapse_while_abstaining_transitions_to_relapsed_and_raises_changed()
    {
        var (vm, abstinence, clock) = Create();
        var id = vm.Node!.Id;

        // Load into Abstaining state.
        abstinence.States[id] = new AbstinenceState(
            AbstinenceStatus.Abstaining,
            clock.UtcNow.ToUnixTimeMilliseconds(),
            null);
        await vm.LoadAsync(DayBoundary.Utc);
        vm.Status.Should().Be(AbstinenceStatus.Abstaining);

        // Pre-seed the state the fake service will return after Lapse.
        abstinence.States[id] = new AbstinenceState(
            AbstinenceStatus.Relapsed,
            null,
            clock.UtcNow.ToUnixTimeMilliseconds());

        bool changedFired = false;
        vm.Changed += (_, _) => changedFired = true;

        await vm.LapseCommand.ExecuteAsync(null);

        vm.Status.Should().Be(AbstinenceStatus.Relapsed);
        changedFired.Should().BeTrue();
    }
}
