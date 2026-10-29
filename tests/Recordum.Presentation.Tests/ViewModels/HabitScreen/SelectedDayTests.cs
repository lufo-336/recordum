using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

/// <summary>Il giorno scelto per registrare (spec §6.1: "per un altro giorno", o un clic sulla mappa). Orologio al
/// 28/10/2026 alle 10:00, reset alle 05:00.</summary>
public class SelectedDayTests
{
    private static readonly DayBoundary Boundary = new(TimeZoneInfo.Utc, new TimeOnly(5, 0));
    private static readonly DateOnly Today = new(2026, 10, 28);
    private static readonly DateOnly Yesterday = new(2026, 10, 27);

    private static async Task<(HabitScreenRig Rig, Guid Id)> LoadAsync(HabitType type, DateTimeOffset? now = null)
    {
        var rig = HabitScreenViewModelFactory.CreateRig(now ?? new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero), Boundary);
        var id = Guid.NewGuid();
        rig.Nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = type, Name = "h", CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc) };
        await rig.Vm.LoadAsync(id);
        return (rig, id);
    }

    private static Completion At(Guid habit, DateTimeOffset when) => new()
    {
        Id = Guid.NewGuid(), HabitNodeId = habit, CompletedAt = when.UtcDateTime, CompletedAtMs = when.ToUnixTimeMilliseconds(),
    };

    [Fact]
    public async Task A_new_subject_starts_on_today()
    {
        var (rig, _) = await LoadAsync(HabitType.Action);
        await rig.Vm.SelectDayAsync(Yesterday);

        var other = Guid.NewGuid();
        rig.Nodes.Store[other] = new HabitNode { Id = other, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "o", CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc) };
        await rig.Vm.LoadAsync(other);

        rig.Vm.SelectedDay.Should().Be(Today);
        rig.Vm.IsSelectedDayToday.Should().BeTrue();
    }

    [Fact]
    public async Task Before_the_reset_the_selected_day_is_yesterday()
    {
        var (rig, _) = await LoadAsync(HabitType.Action, new DateTimeOffset(2026, 10, 28, 4, 30, 0, TimeSpan.Zero));

        rig.Vm.SelectedDay.Should().Be(Yesterday);
        rig.Vm.IsSelectedDayToday.Should().BeTrue();
    }

    [Fact]
    public async Task Recording_follows_the_selected_day()
    {
        var (rig, _) = await LoadAsync(HabitType.Action);

        await rig.Vm.SelectDayAsync(Yesterday);
        rig.Vm.IsSelectedDayToday.Should().BeFalse();
        DateOnly.FromDateTime(rig.Vm.QuickAdd!.CompletionDate).Should().Be(Yesterday);

        await rig.Vm.QuickAdd.SaveAsync();

        rig.Completions.LastRecord!.IsRetroactive.Should().BeTrue();
        rig.Completions.LastRecord.CompletedAtMs.Should().Be(Boundary.NoonOf(Yesterday).ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task Undo_touches_only_the_selected_day()
    {
        var (rig, id) = await LoadAsync(HabitType.Action);
        var y = At(id, new DateTimeOffset(2026, 10, 27, 20, 0, 0, TimeSpan.Zero));
        var t = At(id, new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero));
        rig.Completions.Store[y.Id] = y;
        rig.Completions.Store[t.Id] = t;

        await rig.Vm.SelectDayAsync(Yesterday);
        rig.Vm.CanUndoLast.Should().BeTrue();
        await rig.Vm.UndoLastCommand.ExecuteAsync(null);

        rig.Completions.Store.Keys.Should().BeEquivalentTo([t.Id]);
    }

    [Fact]
    public async Task Undo_belongs_to_the_habit_day_not_the_utc_day()
    {
        // Le 04:30 del 28 sono del 27 col reset alle 05:00: annullare il 28 non le tocca.
        var (rig, id) = await LoadAsync(HabitType.Action);
        var early = At(id, new DateTimeOffset(2026, 10, 28, 4, 30, 0, TimeSpan.Zero));
        rig.Completions.Store[early.Id] = early;

        await rig.Vm.SelectDayAsync(Today);

        rig.Vm.CanUndoLast.Should().BeFalse();
    }

    [Theory]
    [InlineData(HabitType.Resistance)]
    [InlineData(HabitType.Journal)]
    public async Task Undo_is_off_for_abstinence_and_journal(HabitType type)
    {
        var (rig, id) = await LoadAsync(type);
        var t = At(id, new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero));
        rig.Completions.Store[t.Id] = t;

        await rig.Vm.SelectDayAsync(Today);

        rig.Vm.CanUndoLast.Should().BeFalse();
    }

    [Fact]
    public async Task Back_to_today_returns_to_today()
    {
        var (rig, _) = await LoadAsync(HabitType.Action);
        await rig.Vm.SelectDayAsync(Yesterday);

        rig.Vm.BackToTodayCommand.Execute(null);
        await Task.Yield();

        rig.Vm.SelectedDay.Should().Be(Today);
        DateOnly.FromDateTime(rig.Vm.QuickAdd!.CompletionDate).Should().Be(Today);
    }

    [Fact]
    public async Task Case_is_resolved_on_load()
    {
        var (rig, _) = await LoadAsync(HabitType.Counter);
        rig.Vm.Case.Should().Be(HabitCase.CounterTap);
    }

    [Fact]
    public async Task Action_toggle_removes_the_day_completion()
    {
        var (rig, id) = await LoadAsync(HabitType.Action);
        var action = (ActionQuickAddViewModel)rig.Vm.QuickAdd!;

        await action.ToggleCommand.ExecuteAsync(null);
        action.IsDone.Should().BeTrue();
        rig.Completions.Store.Should().HaveCount(1);

        await action.ToggleCommand.ExecuteAsync(null);
        action.IsDone.Should().BeFalse();
        rig.Completions.Store.Should().BeEmpty();
    }

    [Fact]
    public async Task Action_is_done_reflects_the_selected_day()
    {
        var (rig, id) = await LoadAsync(HabitType.Action);
        var y = At(id, new DateTimeOffset(2026, 10, 27, 20, 0, 0, TimeSpan.Zero));
        rig.Completions.Store[y.Id] = y;
        var action = (ActionQuickAddViewModel)rig.Vm.QuickAdd!;

        await rig.Vm.SelectDayAsync(Yesterday);
        action.IsDone.Should().BeTrue();

        await rig.Vm.SelectDayAsync(Today);
        action.IsDone.Should().BeFalse();
    }
}
