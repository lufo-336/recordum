using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.QuickAdd;

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

    [Fact]
    public async Task Lapse_uses_the_chosen_completion_day()
    {
        var (vm, abstinence, clock) = Create();
        var id = vm.Node!.Id;
        abstinence.States[id] = new AbstinenceState(AbstinenceStatus.Abstaining, clock.UtcNow.ToUnixTimeMilliseconds(), null);
        await vm.LoadAsync(DayBoundary.Utc);
        vm.CompletionDate = new DateTime(2026, 5, 30);

        await vm.LapseCommand.ExecuteAsync(null);

        abstinence.LapseDays.Should().Equal(new DateOnly(2026, 5, 30));
    }

    [Fact]
    public async Task Lapse_before_start_shows_the_error_and_does_not_reload()
    {
        var (vm, _, _) = Create();
        var throwing = new ThrowingAbstinence();
        var sut = new AbstinenceQuickAddViewModel(new FakeCompletionService(), throwing, new FakeClock(Epoch), null)
        {
            Node = vm.Node,
        };
        bool changedFired = false;
        sut.Changed += (_, _) => changedFired = true;

        await sut.LapseCommand.ExecuteAsync(null);

        sut.ErrorMessage.Should().Be(Recordum.Presentation.Resources.Strings.Abstinence_Error_BeforeStart);
        changedFired.Should().BeFalse();
    }

    [Fact]
    public void RequestLapsePrompt_raises_the_event()
    {
        var (vm, _, _) = Create();
        bool raised = false;
        vm.LapsePromptRequested += (_, _) => raised = true;

        vm.RequestLapsePrompt();

        raised.Should().BeTrue();
    }

    private sealed class ThrowingAbstinence : IAbstinenceService
    {
        public Task<AbstinenceState> GetStateAsync(Guid habitNodeId, CancellationToken ct = default)
            => Task.FromResult(new AbstinenceState(AbstinenceStatus.Abstaining, 0, null));
        public Task StartAsync(Guid habitNodeId, CancellationToken ct = default) => Task.CompletedTask;
        public Task LapseAsync(Guid habitNodeId, DateOnly? day = null, CancellationToken ct = default)
            => throw new Recordum.Core.Errors.AbstinenceLapseBeforeStartException(habitNodeId, day ?? default);
        public Task<IReadOnlyList<AbstinencePeriod>> ListPeriodsAsync(
            Guid habitNodeId, DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<AbstinencePeriod>>([]);
        public Task DeletePeriodAsync(Guid periodId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
