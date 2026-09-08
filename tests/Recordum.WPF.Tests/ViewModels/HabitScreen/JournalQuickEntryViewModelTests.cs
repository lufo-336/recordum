using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Journal;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class JournalQuickEntryViewModelTests
{
    private static HabitNode Journal() => new()
    {
        Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "Food diary",
    };

    private static (JournalQuickEntryViewModel Sut, FakeDiaryService Diary, FakeAttachmentService Photos,
                    FakeOpenFileDialog Dialog, FakeConfirmationDialog Confirm, FakeNoteService Notes) Build()
    {
        var notes = new FakeNoteService();
        var diary = new FakeDiaryService(notes);
        var photos = new FakeAttachmentService();
        var dialog = new FakeOpenFileDialog();
        var confirm = new FakeConfirmationDialog();
        var sut = new JournalQuickEntryViewModel(diary, notes, photos, dialog, confirm, new FakeClock());
        return (sut, diary, photos, dialog, confirm, notes);
    }

    [Fact]
    public async Task Save_creates_a_diary_entry_so_the_day_counts_as_done()
    {
        var (sut, diary, _, _, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        sut.Draft = "Insalata di farro";

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().ContainSingle()
            .Which.Text.Should().Be("Insalata di farro");
        sut.Draft.Should().BeEmpty("il composer si svuota dopo il salvataggio");
    }

    [Fact]
    public async Task Save_refuses_an_entry_with_neither_text_nor_photo()
    {
        var (sut, diary, _, _, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        sut.Draft = "   ";

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().BeEmpty();
        sut.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task Save_accepts_a_photo_with_no_text()
    {
        var (sut, diary, photos, dialog, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        dialog.MultipleResult = ["C:\\tmp\\piatto.jpg"];
        sut.AddPhotoCommand.Execute(null);

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().ContainSingle();
        photos.AddCalls.Should().ContainSingle().Which.SourceFilePath.Should().Be("C:\\tmp\\piatto.jpg");
    }

    [Fact]
    public async Task Photos_are_attached_only_after_the_note_exists()
    {
        var (sut, diary, photos, dialog, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        dialog.MultipleResult = ["C:\\tmp\\a.jpg"];
        sut.AddPhotoCommand.Execute(null);

        // Prima del salvataggio non deve esistere nessuna nota e nessun allegato.
        diary.SavedEntries.Should().BeEmpty();
        photos.AddCalls.Should().BeEmpty();
        sut.PendingPhotos.Should().ContainSingle();

        sut.Draft = "colazione";
        await sut.SaveCommand.ExecuteAsync(null);

        photos.AddCalls.Single().NoteId.Should().Be(diary.SavedEntries.Single().NoteId);
        sut.PendingPhotos.Should().BeEmpty();
    }

    [Fact]
    public async Task A_failing_photo_does_not_lose_the_entry()
    {
        var (sut, diary, photos, dialog, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        photos.ThrowOnAdd["C:\\tmp\\rotta.jpg"] = new IOException("locked");
        dialog.MultipleResult = ["C:\\tmp\\rotta.jpg"];
        sut.AddPhotoCommand.Execute(null);
        sut.Draft = "pranzo";

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().ContainSingle("la voce resta salvata anche se la foto fallisce");
        sut.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task Save_uses_the_selected_day_so_yesterdays_dinner_lands_on_yesterday()
    {
        var (sut, diary, _, _, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        var yesterday = new DateOnly(2026, 8, 7);
        sut.SetDay(yesterday);
        sut.Draft = "cena di ieri";

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Single().Day.Should().Be(yesterday);
    }

    [Fact]
    public async Task Delete_asks_for_confirmation_and_removes_the_entry()
    {
        var (sut, diary, _, _, confirm, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        sut.Draft = "da buttare";
        await sut.SaveCommand.ExecuteAsync(null);
        var row = sut.Entries.Single();

        confirm.Result = false;
        await sut.DeleteCommand.ExecuteAsync(row);
        diary.DeletedIds.Should().BeEmpty("un rifiuto non cancella niente");

        confirm.Result = true;
        await sut.DeleteCommand.ExecuteAsync(row);
        diary.DeletedIds.Should().ContainSingle();
        sut.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Editing_a_row_updates_the_body_without_creating_a_second_entry()
    {
        var (sut, diary, _, _, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        sut.Draft = "Pasta al pesto";
        await sut.SaveCommand.ExecuteAsync(null);
        var row = sut.Entries.Single();

        sut.BeginEditCommand.Execute(row);
        row.EditDraft = "Pasta al pomodoro";
        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().ContainSingle("la modifica non crea una voce nuova");
        diary.EditedEntries.Single().Text.Should().Be("Pasta al pomodoro");
        sut.Entries.Single().Text.Should().Be("Pasta al pomodoro");
    }

    [Fact]
    public async Task Editing_a_row_to_empty_text_is_refused_like_the_composer()
    {
        var (sut, diary, _, _, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        sut.Draft = "Pasta al pesto";
        await sut.SaveCommand.ExecuteAsync(null);
        var row = sut.Entries.Single();

        sut.BeginEditCommand.Execute(row);
        row.EditDraft = "   ";
        await sut.SaveCommand.ExecuteAsync(null);

        diary.EditedEntries.Should().BeEmpty("un salvataggio con testo vuoto non deve modificare la voce");
        sut.ErrorMessage.Should().NotBeNull();
        row.IsEditing.Should().BeTrue("la modifica resta aperta cosi' l'utente puo' correggere");
        sut.Entries.Single().Text.Should().Be("Pasta al pesto");
    }

    [Fact]
    public async Task Save_sets_IsSaving_during_the_composer_path_and_clears_it_after()
    {
        // W24-R3: asserire solo false→false non intercetterebbe un salvataggio che il flag non lo
        // alza mai. Il gate tiene la scrittura aperta finche' non la rilasciamo, cosi' lo stato
        // "in volo" e' davvero osservabile.
        var (sut, diary, _, _, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        sut.Draft = "cena";
        sut.IsSaving.Should().BeFalse();

        var gate = new TaskCompletionSource();
        diary.NextWriteDelay = () => gate.Task;
        var save = sut.SaveCommand.ExecuteAsync(null);

        sut.IsSaving.Should().BeTrue("il salvataggio e' in volo: il flag deve essere alzato");

        gate.SetResult();
        await save;

        sut.IsSaving.Should().BeFalse("IsSaving deve tornare false dopo il salvataggio");
    }

    [Fact]
    public async Task Save_sets_IsSaving_during_the_in_row_edit_path_too()
    {
        var (sut, diary, _, _, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        sut.Draft = "Pasta al pesto";
        await sut.SaveCommand.ExecuteAsync(null);
        var row = sut.Entries.Single();
        sut.BeginEditCommand.Execute(row);
        row.EditDraft = "Pasta al pomodoro";
        sut.IsSaving.Should().BeFalse();

        var gate = new TaskCompletionSource();
        diary.NextWriteDelay = () => gate.Task;
        var save = sut.SaveCommand.ExecuteAsync(null);

        sut.IsSaving.Should().BeTrue("anche la modifica in riga deve alzare il flag mentre scrive");

        gate.SetResult();
        await save;

        sut.IsSaving.Should().BeFalse("IsSaving deve tornare false anche dopo il salvataggio in riga");
    }

    [Fact]
    public async Task Overlapping_SetDay_calls_do_not_interleave_into_a_mixed_day_list()
    {
        // I4: SetDay is fire-and-forget (no caller holds a CancellationToken to propagate from a
        // calendar click). Before the fix, a slower reload (A) resuming after a faster one (B) would
        // Clear() Entries out from under B's already-applied rows, or append its own rows on top of
        // B's — either way, a day list mixing two days. This holds A's note fetch open with a gate,
        // fires B (which completes immediately), then releases A: only B's day must survive.
        var (sut, _, _, _, _, notes) = Build();
        var node = Journal();
        await sut.LoadAsync(node, DayBoundary.Utc);
        var dayA = new DateOnly(2026, 8, 1);
        var dayB = new DateOnly(2026, 8, 2);
        var noteAId = Guid.NewGuid();
        var noteBId = Guid.NewGuid();
        notes.Store[noteAId] = new Note
        {
            Id = noteAId, HabitNodeId = node.Id, Category = NoteCategory.Diario,
            TargetDate = dayA, BodyMd = "giorno A", CreatedAt = DateTime.UtcNow,
        };
        notes.Store[noteBId] = new Note
        {
            Id = noteBId, HabitNodeId = node.Id, Category = NoteCategory.Diario,
            TargetDate = dayB, BodyMd = "giorno B", CreatedAt = DateTime.UtcNow,
        };

        var gate = new TaskCompletionSource();
        notes.NextListByHabitDelay = () => gate.Task;
        sut.SetDay(dayA); // starts, blocks inside ListByHabitAsync awaiting the gate

        sut.SetDay(dayB); // no delay queued anymore: runs to completion immediately

        sut.Entries.Should().ContainSingle().Which.Text.Should().Be("giorno B");

        gate.SetResult(); // release the superseded A; it must bail instead of touching Entries
        await Task.Delay(20);

        sut.Entries.Should().ContainSingle().Which.Text.Should().Be("giorno B",
            "il reload superato non deve ne' sovrascrivere ne' aggiungersi alla lista di B");
    }
}
