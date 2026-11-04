using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Presentation.Tests.Fixtures;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.Figures;

public class HabitScreenFiguresWiringTests
{
    [Fact]
    public async Task Figures_load_with_the_subject_and_follow_completions()
    {
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero));
        var id = Guid.NewGuid();
        rig.Nodes.Store[id] = new HabitNode
        {
            Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "acqua",
            CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        };

        await rig.Vm.LoadAsync(id);
        rig.Vm.Figures.Items.Should().HaveCount(4);
        rig.Vm.Figures.Items[0].Value.Should().Be("0");

        rig.Completions.HeatmapByHabit[id] = [new HeatmapCell(new DateOnly(2026, 10, 28), 3)];
        rig.Bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), id, new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc)));
        await WaitUntilAsync(() => rig.Vm.Figures.Items[0].Value == "3");

        rig.Vm.Figures.Items[0].Value.Should().Be("3");
    }

    [Fact]
    public async Task A_group_has_no_figures()
    {
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero));
        var id = Guid.NewGuid();
        rig.Nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Group, Name = "g", CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc) };

        await rig.Vm.LoadAsync(id);

        rig.Vm.Figures.Items.Should().BeEmpty();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(10);
        condition().Should().BeTrue("the async event handler should have reloaded in time");
    }

    [Fact]
    public async Task A_group_loads_group_figures_and_follows_a_childs_completion()
    {
        var history = new FakeGroupHistoryQueryService();
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero), groupHistory: history);
        var id = Guid.NewGuid();
        rig.Nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Group, Name = "g", CreatedAt = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc) };

        var child = Guid.NewGuid();
        rig.Tree.HomeRows.Add(Row(id, null, null, false));
        rig.Tree.HomeRows.Add(Row(child, id, HabitType.Action, false));
        await rig.Vm.LoadAsync(id);
        rig.Vm.GroupFigures!.Items.Select(f => f.Value).Should().Equal("0", "0%");

        var today = new DateOnly(2026, 10, 28);
        history.Children.Add(new GroupChildHistory(child, "a", "#112233", HabitType.Action, 0, today,
            System.Collections.Immutable.ImmutableHashSet.Create(today), System.Collections.Immutable.ImmutableHashSet.Create(today)));
        rig.Bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), child, new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc)));
        await WaitUntilAsync(() => rig.Vm.GroupFigures.Items[0].Value == "1");

        rig.Vm.GroupFigures.Items.Select(f => f.Value).Should().Equal("1", "100%");
    }

    [Fact]
    public async Task A_childs_completion_refreshes_the_group_count_and_goal()
    {
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero),
            groupHistory: new FakeGroupHistoryQueryService());
        var group = Guid.NewGuid();
        var child = Guid.NewGuid();
        rig.Nodes.Store[group] = new HabitNode { Id = group, NodeType = NodeType.Group, Name = "g", CreatedAt = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc) };
        rig.Goals.Store[Guid.NewGuid()] = new Goal { HabitNodeId = group, Name = "obiettivo", TargetValue = 5, PeriodType = GoalPeriodType.Rolling, RollingDays = 7 };
        rig.Tree.HomeRows.Add(Row(group, null, null, false));
        rig.Tree.HomeRows.Add(Row(child, group, HabitType.Action, false));

        await rig.Vm.LoadAsync(group);
        rig.Vm.Panoramica.TodayLabel.Should().Contain("0/1");
        rig.Vm.GroupFigures!.Items.Last().Value.Should().Contain("0");

        rig.Tree.HomeRows[1] = Row(child, group, HabitType.Action, true);
        rig.Completions.Store[Guid.NewGuid()] = new Completion
        {
            HabitNodeId = group, CompletedAtMs = new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
        };
        rig.Bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), child, new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc)));
        await WaitUntilAsync(() => rig.Vm.Panoramica.TodayLabel.Contains("1/1"));

        rig.Vm.Panoramica.TodayFraction.Should().Be(1);
        rig.Vm.GroupFigures.Items.Last().Value.Should().MatchRegex("^1 \\S+ 5$");
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 28);

    // Un gruppo creato quest'anno (20 ottobre) con una figlia.
    private static (HabitScreenRig Rig, FakeGroupHistoryQueryService History, Guid Group, Guid Child) GroupRig(HabitType childType = HabitType.Action)
    {
        var history = new FakeGroupHistoryQueryService();
        var rig = HabitScreenViewModelFactory.CreateRig(Now, groupHistory: history);
        var group = Guid.NewGuid();
        var child = Guid.NewGuid();
        rig.Nodes.Store[group] = new HabitNode { Id = group, NodeType = NodeType.Group, Name = "g", CreatedAt = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc) };
        rig.Tree.HomeRows.Add(Row(group, null, null, false));
        rig.Tree.HomeRows.Add(Row(child, group, childType, false) with { AbstinenceStatus = AbstinenceStatus.Relapsed });
        return (rig, history, group, child);
    }

    private static GroupChildHistory DoneToday(Guid child) => new(child, "a", "#112233", HabitType.Resistance, 0, Today,
        System.Collections.Immutable.ImmutableHashSet.Create(Today), System.Collections.Immutable.ImmutableHashSet.Create(Today));

    public static TheoryData<string> AbstinenceEvents => ["abstinence.started", "abstinence.lapsed", "abstinence.period.deleted"];

    // Finding 1: Inizia/Ricaduta di una figlia (dalla riga del gruppo, dalla Home, da un'altra scheda) non scrive
    // registrazioni: solo l'evento dell'astinenza dice al gruppo di rileggersi.
    [Theory]
    [MemberData(nameof(AbstinenceEvents))]
    public async Task A_childs_abstinence_change_reloads_the_group(string eventName)
    {
        var name = new EventName(eventName);
        var (rig, history, group, child) = GroupRig(HabitType.Resistance);
        await rig.Vm.LoadAsync(group);
        rig.Vm.Panoramica.CompletedToday.Should().Be(0);
        rig.Vm.GroupFigures!.Items[0].Value.Should().Be("0");

        rig.Tree.HomeRows[1] = rig.Tree.HomeRows[1] with { AbstinenceStatus = AbstinenceStatus.Abstaining };
        history.Children.Add(DoneToday(child));
        rig.Bus.Publish(new AbstinenceChangedPayload(child, name));
        await WaitUntilAsync(() => rig.Vm.Panoramica.CompletedToday == 1 && rig.Vm.GroupFigures.Items[0].Value == "1");

        rig.Vm.GroupProgressCount.Should().Contain("1");
    }

    // Finding 5: un evento di un'abitudine fuori dal gruppo non rilegge il gruppo (0,4–0,8 s su un gruppo grande).
    [Fact]
    public async Task An_event_for_a_habit_outside_the_group_does_not_reload_it()
    {
        var (rig, _, group, _) = GroupRig(HabitType.Resistance);
        await rig.Vm.LoadAsync(group);
        var calls = rig.Tree.HomeCallCount;
        var outsider = Guid.NewGuid();

        rig.Bus.Publish(new CompletionRecordedPayload(Guid.NewGuid(), outsider, new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc)));
        rig.Bus.Publish(new AbstinenceChangedPayload(outsider, WellKnownEvents.AbstinenceStarted));
        rig.Bus.Publish(new HabitUpdatedPayload(outsider));
        await Task.Delay(150);

        rig.Tree.HomeCallCount.Should().Be(calls);
    }

    // Finding 5: rinominare (o ricolorare) una figlia si vede subito nel gruppo aperto.
    [Fact]
    public async Task A_childs_rename_shows_in_the_open_group()
    {
        var (rig, _, group, child) = GroupRig();
        await rig.Vm.LoadAsync(group);

        rig.Tree.HomeRows[1] = rig.Tree.HomeRows[1] with { Name = "corsa" };
        rig.Bus.Publish(new HabitUpdatedPayload(child));
        await WaitUntilAsync(() => rig.Vm.Panoramica.Children.Any(c => c.Name == "corsa"));

        rig.Vm.Panoramica.Rows.Should().Contain(r => r.HabitNodeId == child);
    }

    // Finding 2: l'obiettivo del gruppo si modifica in Configura; GoalService pubblica HabitUpdated(gruppo).
    [Fact]
    public async Task Editing_the_groups_goal_refreshes_the_head_goal_figure()
    {
        var (rig, _, group, _) = GroupRig();
        var goalId = Guid.NewGuid();
        rig.Goals.Store[goalId] = new Goal { Id = goalId, HabitNodeId = group, Name = "obiettivo", TargetValue = 5, PeriodType = GoalPeriodType.Rolling, RollingDays = 7 };
        await rig.Vm.LoadAsync(group);
        rig.Vm.GroupFigures!.Items.Last().Value.Should().MatchRegex("^0 \\S+ 5$");

        rig.Goals.Store[goalId].TargetValue = 9;
        rig.Bus.Publish(new HabitUpdatedPayload(group));
        await WaitUntilAsync(() => rig.Vm.Panoramica.Goal.Target == 9);

        rig.Vm.GroupFigures.Items.Last().Value.Should().MatchRegex("^0 \\S+ 9$");
    }

    // Finding 3 (Ruling 4): "Tutto" su un gruppo comincia dal suo inizio — la più vecchia fra la creazione del
    // gruppo e l'inizio delle figlie con giorni previsti — non dall'anno di creazione del gruppo.
    [Fact]
    public async Task All_on_a_group_starts_at_the_oldest_childs_start()
    {
        var (rig, history, group, child) = GroupRig();
        var start = new DateOnly(2024, 5, 1);
        history.Children.Add(new GroupChildHistory(child, "a", "#112233", HabitType.Action, 0, start,
            System.Collections.Immutable.ImmutableHashSet.Create(start, Today), System.Collections.Immutable.ImmutableHashSet.Create(start)));
        await rig.Vm.LoadAsync(group);

        rig.Vm.Period.Period = Recordum.Presentation.ViewModels.HabitScreen.History.HistoryPeriod.All;

        rig.Vm.Period.From.Should().Be(new DateOnly(2024, 1, 1));
        rig.Vm.Period.To.Should().Be(Today);
    }

    private static HomeTreeRow Row(Guid id, Guid? parent, HabitType? type, bool today)
        => new(id, parent, "n", "#112233", type, null, 0, today, PinScope.None, 0, false,
            System.Collections.Immutable.ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), System.Collections.Immutable.ImmutableArray<Guid>.Empty);
}
