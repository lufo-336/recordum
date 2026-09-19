using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

/// <summary>The dedicated "✎ Diario" pane for journal habits (workspace shell SP1 §3.5): the markdown
/// editor lives in Diario, not in Cronologia, and is the default pane for a journal subject.</summary>
public class HabitScreenDiarioColumnTests
{
    private static Guid AddHabit(FakeHabitNodeService nodes, HabitType type)
    {
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = type, Name = "h", Color = "#808080" };
        return id;
    }

    [Fact]
    public async Task Journal_habit_with_no_columns_defaults_to_Diario()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var j = AddHabit(nodes, HabitType.Journal);

        await sut.LoadAsync(j);

        sut.IsDiarioOpen.Should().BeTrue();
        sut.ShowDiario.Should().BeTrue();
        sut.IsCronologiaOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Non_journal_habit_does_not_default_to_Diario()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var a = AddHabit(nodes, HabitType.Action);

        await sut.LoadAsync(a);

        sut.IsCronologiaOpen.Should().BeTrue();
        sut.ShowDiario.Should().BeFalse();
    }

    [Fact]
    public void Diario_applies_only_to_journal_habits()
    {
        HabitScreenViewModel.IsColumnApplicable(HabitColumn.Diario, NodeType.Habit, HabitType.Journal).Should().BeTrue();
        HabitScreenViewModel.IsColumnApplicable(HabitColumn.Diario, NodeType.Habit, HabitType.Action).Should().BeFalse();
        HabitScreenViewModel.IsColumnApplicable(HabitColumn.Diario, NodeType.Group, null).Should().BeFalse();
    }

    [Fact]
    public async Task Journal_routes_the_editor_to_Diario_not_Cronologia()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var j = AddHabit(nodes, HabitType.Journal);

        await sut.LoadAsync(j);

        sut.QuickAdd.Should().BeOfType<JournalQuickAddViewModel>();
        sut.QuickAddForDiario.Should().BeSameAs(sut.QuickAdd);
        sut.QuickAddForCronologia.Should().BeNull();
    }

    [Fact]
    public async Task Non_journal_keeps_the_quick_add_in_Cronologia_not_Diario()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var a = AddHabit(nodes, HabitType.Action);

        await sut.LoadAsync(a);

        sut.QuickAdd.Should().NotBeNull();
        sut.QuickAddForCronologia.Should().BeSameAs(sut.QuickAdd);
        sut.QuickAddForDiario.Should().BeNull();
    }

    [Fact]
    public async Task Diario_flag_stays_hidden_on_a_non_journal_habit()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var j = AddHabit(nodes, HabitType.Journal);
        var a = AddHabit(nodes, HabitType.Action);
        await sut.LoadAsync(j);     // Diario opens by default

        await sut.LoadAsync(a);     // switch to a non-journal: flag intact, pane hidden

        sut.IsDiarioOpen.Should().BeTrue();
        sut.ShowDiario.Should().BeFalse();
    }
}
