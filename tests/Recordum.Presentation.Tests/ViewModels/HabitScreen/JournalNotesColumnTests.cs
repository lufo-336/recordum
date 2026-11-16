using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Notes;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

/// <summary>Onda "Giornata", Task 13 (B2): in un diario si scrivono voci, non note. La colonna Note su un diario non c'è,
/// salvo che il diario abbia già note libere (categoria Veloce): allora le mostra e non ne crea di nuove. Le voci del
/// diario sono anch'esse entità <c>Note</c> (categoria Diario): da sole non fanno comparire la colonna.</summary>
public class JournalNotesColumnTests
{
    private static Guid Add(FakeHabitNodeService nodes, HabitType? type, NodeType nodeType = NodeType.Habit)
    {
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = nodeType, HabitType = type, Name = "n", Color = "#808080" };
        return id;
    }

    private static void Seed(FakeNoteService notes, Guid habit, NoteCategory category = NoteCategory.Veloce)
    {
        var note = new Note { Id = Guid.NewGuid(), HabitNodeId = habit, BodyMd = "c'era già", Category = category };
        notes.Store[note.Id] = note;
    }

    [Fact]
    public async Task A_journal_has_no_notes_column()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.Create();
        var j = Add(nodes, HabitType.Journal);
        await sut.LoadAsync(j);

        sut.IsNoteOpen = true;   // l'insieme aperto può portarsela dietro da un'altra abitudine

        sut.IsNoteColumnAvailable.Should().BeFalse("il pulsante della colonna non c'è");
        sut.ShowNote.Should().BeFalse();
        sut.NotesForColumn.Should().BeNull();
    }

    [Fact]
    public async Task A_journal_with_only_diary_entries_has_no_notes_column()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.Create(out var notes);
        var j = Add(nodes, HabitType.Journal);
        Seed(notes, j, NoteCategory.Diario);   // una voce del diario è un'entità Note: non conta
        await sut.LoadAsync(j);
        sut.IsNoteOpen = true;

        sut.IsNoteColumnAvailable.Should().BeFalse();
        sut.ShowNote.Should().BeFalse();
    }

    [Fact]
    public async Task A_journal_with_existing_notes_keeps_it_read_only()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.Create(out var notes);
        var j = Add(nodes, HabitType.Journal);
        Seed(notes, j);
        await sut.LoadAsync(j);
        sut.IsNoteOpen = true;

        sut.IsNoteColumnAvailable.Should().BeTrue();
        sut.ShowNote.Should().BeTrue();
        sut.Notes.IsReadOnlyLegacy.Should().BeTrue();
        sut.Notes.Notes.Should().ContainSingle("la nota che c'era si vede");

        await sut.Notes.NewCommand.ExecuteAsync(null);   // ExecuteAsync non consulta CanExecute: la guardia sta nel metodo
        notes.Store.Should().ContainSingle("su un diario non si creano note nuove");
    }

    [Fact]
    public async Task A_journal_gets_the_column_back_when_notes_appear_after_reload()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.Create(out var notes);
        var j = Add(nodes, HabitType.Journal);
        await sut.LoadAsync(j);
        sut.IsNoteOpen = true;
        sut.ShowNote.Should().BeFalse();

        Seed(notes, j);            // un ripristino le riporta
        await sut.LoadAsync(j);

        sut.IsNoteColumnAvailable.Should().BeTrue();
        sut.ShowNote.Should().BeTrue();
    }

    [Theory]
    [InlineData(NodeType.Habit, HabitType.Action)]
    [InlineData(NodeType.Habit, HabitType.Counter)]
    [InlineData(NodeType.Group, null)]
    public async Task Other_types_keep_the_notes_column(NodeType nodeType, HabitType? type)
    {
        var (sut, nodes) = HabitScreenViewModelFactory.Create(out var notes);
        var id = Add(nodes, type, nodeType);
        await sut.LoadAsync(id);
        sut.IsNoteOpen = true;

        sut.IsNoteColumnAvailable.Should().BeTrue();
        sut.ShowNote.Should().BeTrue();
        sut.Notes.IsReadOnlyLegacy.Should().BeFalse();
        await sut.Notes.NewCommand.ExecuteAsync(null);
        notes.Store.Should().ContainSingle("qui una nota nuova si crea");
    }

    [Fact]
    public async Task Read_only_does_not_leak_from_a_journal_to_the_next_habit()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.Create(out var notes);
        var j = Add(nodes, HabitType.Journal);
        var a = Add(nodes, HabitType.Action);
        Seed(notes, j);
        await sut.LoadAsync(j);
        sut.Notes.IsReadOnlyLegacy.Should().BeTrue();

        await sut.LoadAsync(a);

        sut.Notes.IsReadOnlyLegacy.Should().BeFalse();
    }

    [Fact]
    public async Task Open_columns_survive_switching_between_a_journal_and_a_habit()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.Create();
        var a = Add(nodes, HabitType.Action);
        var j = Add(nodes, HabitType.Journal);
        await sut.LoadAsync(a);
        sut.IsCronologiaOpen = false;
        sut.IsNoteOpen = true;       // sull'azione resta aperta solo Note

        await sut.LoadAsync(j);
        sut.IsNoteOpen.Should().BeTrue("l'insieme aperto non si tocca: sul diario la colonna solo non si mostra");
        sut.ShowNote.Should().BeFalse();
        (sut.ShowDiario || sut.ShowCronologia).Should().BeTrue("un diario senza note non resta a schermo vuoto");

        await sut.LoadAsync(a);
        sut.ShowNote.Should().BeTrue("tornando sull'azione Note è ancora aperta");
    }

    [Fact]
    public async Task A_journal_whose_saved_columns_are_only_notes_still_shows_something()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.CreateWithStores(out _, out var uiPrefs);
        var j = Add(nodes, HabitType.Journal);
        uiPrefs.Store[j] = new HabitUiPrefs { OpenColumns = ["Note"] };   // salvato prima di quest'onda

        await sut.LoadAsync(j);

        sut.ShowNote.Should().BeFalse();
        sut.ShowDiario.Should().BeTrue();
    }

    [Fact]
    public async Task Closing_the_last_visible_column_of_a_journal_counts_as_all_closed()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.Create();
        var j = Add(nodes, HabitType.Journal);
        await sut.LoadAsync(j);
        sut.IsNoteOpen = true;       // aperta ma non disponibile: non deve contare
        var raised = 0;
        sut.AllColumnsClosed += (_, _) => raised++;

        sut.IsDiarioOpen = false;

        raised.Should().Be(1);
    }

    // ---- dalla review ----

    [Fact]
    public async Task A_journal_with_legacy_notes_lists_only_those()
    {
        // Le voci del diario sono Note anche loro: nella colonna farebbero doppione, e cancellarle da lì lascerebbe
        // orfana la registrazione accoppiata.
        var (sut, nodes) = HabitScreenViewModelFactory.Create(out var notes);
        var j = Add(nodes, HabitType.Journal);
        Seed(notes, j);
        Seed(notes, j, NoteCategory.Diario);
        await sut.LoadAsync(j);

        sut.Notes.Notes.Should().ContainSingle().Which.Note.Category.Should().Be(NoteCategory.Veloce);
    }

    [Fact]
    public async Task A_focused_notes_column_does_not_leave_a_journal_without_columns()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.Create();
        var a = Add(nodes, HabitType.Action);
        var j = Add(nodes, HabitType.Journal);
        await sut.LoadAsync(a);
        sut.IsNoteOpen = true;
        sut.ToggleFocusCommand.Execute(HabitColumn.Note);   // Note a tutta scheda

        await sut.LoadAsync(j);

        sut.FocusedColumn.Should().BeNull("la colonna a tutta scheda qui non esiste");
        (sut.ShowDiario || sut.ShowCronologia).Should().BeTrue();
        sut.ShowChipBar.Should().BeTrue();
    }

    [Fact]
    public async Task Switching_to_a_journal_with_notes_never_hides_the_open_column()
    {
        // Il conto si legge prima di cambiare soggetto: altrimenti la colonna si chiude e si ricostruisce a ogni passaggio.
        var (sut, nodes) = HabitScreenViewModelFactory.Create(out var notes);
        var a = Add(nodes, HabitType.Action);
        var j = Add(nodes, HabitType.Journal);
        Seed(notes, j);
        await sut.LoadAsync(a);
        sut.IsNoteOpen = true;
        var seen = new List<bool>();
        sut.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(sut.ShowNote)) seen.Add(sut.ShowNote); };

        await sut.LoadAsync(j);

        seen.Should().NotContain(false);
        sut.ShowNote.Should().BeTrue();
    }

    [Fact]
    public async Task Esc_on_a_journal_closes_a_column_that_is_there()
    {
        var (sut, nodes) = HabitScreenViewModelFactory.Create();
        var j = Add(nodes, HabitType.Journal);
        await sut.LoadAsync(j);
        sut.IsNoteOpen = true;       // l'ultima aperta, ma su un diario non si vede

        sut.CloseLastColumn();

        sut.ShowDiario.Should().BeFalse("Esc chiude la colonna che si vede");
        sut.IsNoteOpen.Should().BeTrue();
    }

    [Fact]
    public void A_note_row_previews_its_whole_first_line()
    {
        // La riga è resa: tagliare a 40 caratteri spezzerebbe un marcatore e lascerebbe gli asterischi a vista.
        var first = "**lenta** e poi una riga ben più lunga di quaranta caratteri";
        var row = new NoteRowViewModel(new Note { Id = Guid.NewGuid(), BodyMd = $"\n  {first}  \nseconda riga" }, DayBoundary.Utc);

        row.PreviewMarkdown.Should().Be(first);
    }

    [Fact]
    public void An_empty_note_row_previews_the_untitled_marker()
    {
        var row = new NoteRowViewModel(new Note { Id = Guid.NewGuid(), BodyMd = "  \n" }, DayBoundary.Utc);

        row.PreviewMarkdown.Should().Be(Recordum.Presentation.Resources.Strings.Notes_Untitled);
    }
}
