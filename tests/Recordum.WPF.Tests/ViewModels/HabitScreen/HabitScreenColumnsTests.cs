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

    private static Guid AddGroup(FakeHabitNodeService nodes)
    {
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Group, Name = "g", Color = "#808080" };
        return id;
    }

    private static Guid AddJournal(FakeHabitNodeService nodes)
    {
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "j", Color = "#808080" };
        return id;
    }

    // ---- Onda 2.2: per-habit column persistence + per-type defaults ----

    [Fact]
    public async Task First_load_of_a_journal_with_no_prefs_defaults_to_diario()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var j = AddJournal(nodes);

        await sut.LoadAsync(j);

        sut.ShowDiario.Should().BeTrue();
        sut.ShowCronologia.Should().BeFalse();
    }

    [Fact]
    public async Task Per_habit_open_set_persists_and_restores_across_instances()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithStores(out var settings, out var uiPrefs);
        var a = AddHabit(nodes);
        await sut.LoadAsync(a);
        sut.IsDettaglioOpen = true;   // user toggle → persists A's own set

        uiPrefs.Store[a].OpenColumns.Should().Contain("Dettaglio");

        // A fresh VM over the SAME per-habit store restores A's columns (no legacy global carry-over).
        var (sut2, nodes2) = HabitScreenViewModelFactory.CreateOver(settings, uiPrefs);
        nodes2.Store[a] = nodes.Store[a];
        await sut2.LoadAsync(a);

        sut2.IsDettaglioOpen.Should().BeTrue();
        sut2.IsCronologiaOpen.Should().BeTrue();
    }

    [Fact]
    public async Task Journal_quick_note_gesture_opens_the_Note_column_via_the_strip()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithStores(out _, out var uiPrefs);
        var j = AddJournal(nodes);
        uiPrefs.Store[j] = new HabitUiPrefs { JournalGesture = JournalPrimaryGesture.QuickNote };
        await sut.LoadAsync(j);
        sut.IsNoteOpen.Should().BeFalse();

        // The strip's primary gesture bubbles OpenQuickNote → the host opens the Note column.
        var content = (Recordum.WPF.ViewModels.HabitScreen.Today.JournalTodayContent)sut.TodayStrip.Content!;
        content.PrimaryCommand.Execute(null);

        sut.IsNoteOpen.Should().BeTrue();
    }

    [Fact]
    public async Task Null_prefs_fall_back_to_the_legacy_global_open_columns()
    {
        var (_, _) = HabitScreenViewModelFactory.CreateWithStores(out var settings, out var uiPrefs);
        await settings.SetAsync(HabitScreenViewModel.OpenColumnsSettingKey, "Cronologia,Dettaglio");

        // A brand-new VM/subject with no per-habit prefs restores the legacy global set.
        var (sut, nodes) = HabitScreenViewModelFactory.CreateOver(settings, uiPrefs);
        var id = AddHabit(nodes);
        await sut.LoadAsync(id);

        sut.IsCronologiaOpen.Should().BeTrue();
        sut.IsDettaglioOpen.Should().BeTrue();
    }

    [Fact]
    public async Task Group_with_no_applicable_columns_defaults_to_panoramica()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var g = AddGroup(nodes);

        await sut.LoadAsync(g);

        sut.IsPanoramicaOpen.Should().BeTrue();
        sut.ShowPanoramica.Should().BeTrue();
        sut.ShowCronologia.Should().BeFalse();
    }

    [Fact]
    public async Task Habit_columns_stay_flagged_but_hidden_on_a_group_and_return_on_a_habit()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var h = AddHabit(nodes);
        var g = AddGroup(nodes);
        await sut.LoadAsync(h);          // Cronologia aperta
        sut.IsDettaglioOpen = true;

        await sut.LoadAsync(g);          // gruppo: flag intatti, colonne habit nascoste

        sut.IsCronologiaOpen.Should().BeTrue();
        sut.ShowCronologia.Should().BeFalse();
        sut.ShowDettaglio.Should().BeFalse();
        sut.ShowPanoramica.Should().BeTrue();   // default applicabile aggiunto

        await sut.LoadAsync(h);          // tornando alla habit le colonne ricompaiono
        sut.ShowCronologia.Should().BeTrue();
        sut.ShowDettaglio.Should().BeTrue();
        sut.ShowPanoramica.Should().BeFalse();
    }

    [Fact]
    public async Task Closing_the_last_applicable_column_on_a_group_raises_AllColumnsClosed()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var h = AddHabit(nodes);
        var g = AddGroup(nodes);
        await sut.LoadAsync(h);          // Cronologia aperta (non applicabile al gruppo)
        await sut.LoadAsync(g);          // + Panoramica
        var raised = 0;
        sut.AllColumnsClosed += (_, _) => raised++;

        sut.IsPanoramicaOpen = false;    // Cronologia resta flaggata ma non applicabile

        raised.Should().Be(1);
    }

    [Fact]
    public async Task Group_load_populates_the_panoramica_rollup()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithSettings(out _);
        var g = AddGroup(nodes);

        await sut.LoadAsync(g);

        sut.Panoramica.Should().NotBeNull();
        sut.Panoramica.TotalChildren.Should().Be(0);   // il fake tree è vuoto: rollup caricato, zero figli
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
