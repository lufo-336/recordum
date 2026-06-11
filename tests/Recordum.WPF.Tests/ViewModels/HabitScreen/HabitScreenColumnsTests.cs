using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class HabitScreenColumnsTests
{
    private static Guid AddHabit(FakeHabitNodeService nodes)
    {
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h", Color = "#808080" };
        return id;
    }

    [Fact]
    public async Task First_load_with_no_columns_opens_cronologia()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out var _unused);
        var id = AddHabit(nodes);

        await sut.LoadAsync(id);

        sut.IsCronologiaOpen.Should().BeTrue();
        sut.IsDettaglioOpen.Should().BeFalse();
        sut.IsNoteOpen.Should().BeFalse();
        sut.IsConfiguraOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Open_set_survives_switching_habit()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out var _unused);
        var a = AddHabit(nodes);
        var b = AddHabit(nodes);
        await sut.LoadAsync(a);
        sut.IsNoteOpen = true;

        await sut.LoadAsync(b);

        sut.IsCronologiaOpen.Should().BeTrue();
        sut.IsNoteOpen.Should().BeTrue();
    }

    [Fact]
    public async Task Toggle_persists_csv_and_roundtrips()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out var settings);
        var id = AddHabit(nodes);
        await sut.LoadAsync(id);
        sut.IsDettaglioOpen = true;

        (await settings.GetAsync(HabitScreenViewModel.OpenColumnsSettingKey)).Should().Be("Cronologia,Dettaglio");

        // Round-trip: a fresh VM over the same store restores the same set.
        var (sut2, nodes2) = HabitScreenViewModelFactory.CreateOver(settings);
        var id2 = AddHabit(nodes2);
        await sut2.RestoreOpenColumnsAsync();
        await sut2.LoadAsync(id2);
        sut2.IsDettaglioOpen.Should().BeTrue();
        sut2.IsCronologiaOpen.Should().BeTrue();
    }

    [Fact]
    public async Task Opening_configura_raises_ConfigureRequested()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out var _unused);
        var id = AddHabit(nodes);
        await sut.LoadAsync(id);
        var raised = 0;
        sut.ConfigureRequested += (_, _) => raised++;

        sut.IsConfiguraOpen = true;

        raised.Should().Be(1);
        sut.IsConfiguraOpen.Should().BeTrue();
    }

    [Fact]
    public async Task Reopen_defaults_to_the_last_closed_column()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out var settings);
        var a = AddHabit(nodes);
        var b = AddHabit(nodes);
        await sut.LoadAsync(a);          // Cronologia (default)
        sut.IsDettaglioOpen = true;
        sut.IsCronologiaOpen = false;    // last closed = Cronologia
        sut.IsDettaglioOpen = false;     // last closed = Dettaglio, set vuoto

        (await settings.GetAsync(HabitScreenViewModel.LastClosedColumnSettingKey)).Should().Be("Dettaglio");

        await sut.LoadAsync(b);          // riapertura: usa l'ultima chiusa, non Cronologia
        sut.IsDettaglioOpen.Should().BeTrue();
        sut.IsCronologiaOpen.Should().BeFalse();
    }

    [Fact]
    public async Task AllColumnsClosed_raised_when_the_open_set_empties()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var id = AddHabit(nodes);
        await sut.LoadAsync(id);         // Cronologia
        var raised = 0;
        sut.AllColumnsClosed += (_, _) => raised++;

        sut.IsCronologiaOpen = false;

        raised.Should().Be(1);
    }

    [Fact]
    public async Task Restore_reads_the_persisted_last_closed_column()
    {
        var (_, _) = HabitScreenViewModelFactory.CreateWithSettings(out var settings);
        await settings.SetAsync(HabitScreenViewModel.LastClosedColumnSettingKey, "Note");

        var (sut, nodes) = HabitScreenViewModelFactory.CreateOver(settings);
        await sut.RestoreOpenColumnsAsync();
        var id = AddHabit(nodes);
        await sut.LoadAsync(id);

        sut.IsNoteOpen.Should().BeTrue();
        sut.IsCronologiaOpen.Should().BeFalse();
    }

    [Fact]
    public async Task CloseLastColumn_closes_most_recently_opened()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out var _unused);
        var id = AddHabit(nodes);
        await sut.LoadAsync(id);    // Cronologia (default)
        sut.IsNoteOpen = true;      // then Note
        sut.IsDettaglioOpen = true; // then Dettaglio

        sut.CloseLastColumn();
        sut.IsDettaglioOpen.Should().BeFalse();
        sut.CloseLastColumn();
        sut.IsNoteOpen.Should().BeFalse();
        sut.IsCronologiaOpen.Should().BeTrue();
    }
}
