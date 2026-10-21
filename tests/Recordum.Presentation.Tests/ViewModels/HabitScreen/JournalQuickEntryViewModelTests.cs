using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Errors;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitScreen.Notes;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

public class JournalQuickEntryViewModelTests
{
    private static HabitNode Journal() => new()
    {
        Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "Food diary",
    };

    private static (JournalQuickEntryViewModel Sut, FakeDiaryService Diary, FakeAttachmentService Photos,
                    FakeOpenFileDialog Dialog, FakeConfirmationDialog Confirm, FakeNoteService Notes, FakePhotoImporter Importer) Build(FakeClock? clock = null)
    {
        var notes = new FakeNoteService();
        var diary = new FakeDiaryService(notes);
        var photos = new FakeAttachmentService();
        var dialog = new FakeOpenFileDialog();
        var confirm = new FakeConfirmationDialog();
        var importer = new FakePhotoImporter();
        var sut = new JournalQuickEntryViewModel(diary, notes, photos, importer, new FakeTagService(), dialog, confirm, clock ?? new FakeClock());
        return (sut, diary, photos, dialog, confirm, notes, importer);
    }

    [Fact]
    public async Task Inline_menu_lists_matches_then_a_create_entry_when_the_prefix_is_not_a_known_tag()
    {
        var notes = new FakeNoteService();
        var tags = new FakeTagService();
        var sut = new JournalQuickEntryViewModel(new FakeDiaryService(notes), notes, new FakeAttachmentService(), new FakePhotoImporter(),
            tags, new FakeOpenFileDialog(), new FakeConfirmationDialog(), new FakeClock());
        await tags.CreateAsync(new Tag { Name = "pranzo" });
        await sut.LoadAsync(Journal(), DayBoundary.Utc);

        sut.UpdateInlineQuery("#pra", 4);
        sut.InlineSuggestions.Should().Equal("pranzo", string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.Diario_Tag_Create, "pra"));

        sut.UpdateInlineQuery("#PRANZO", 7);
        sut.InlineSuggestions.Should().Equal(["pranzo"], "un nome noto (senza maiuscole) non offre Crea");

        sut.UpdateInlineQuery("#", 1);
        sut.InlineSuggestions.Should().Equal(["pranzo"], "prefisso vuoto: tutti i tag, niente Crea");

        sut.UpdateInlineQuery("#zup", 4);
        var create = sut.InlineSuggestions.Should().ContainSingle().Subject;
        sut.AcceptInlineSuggestion(create).Should().Be(("#zup ", 5), "la voce Crea torna il testo digitato");
    }

    [Fact]
    public async Task Save_creates_a_diary_entry_so_the_day_counts_as_done()
    {
        var (sut, diary, _, _, _, _, _) = Build();
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
        var (sut, diary, _, _, _, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        sut.Draft = "   ";

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().BeEmpty();
        sut.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task Save_accepts_a_photo_with_no_text()
    {
        var (sut, diary, _, dialog, _, _, importer) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        dialog.MultipleResult = ["C:\\tmp\\piatto.jpg"];
        sut.AddPhotoCommand.Execute(null);

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().ContainSingle();
        importer.ImportCalls.Should().ContainSingle().Which.Should().Be("C:\\tmp\\piatto.jpg");
        diary.SavedPhotos.Single().Photos.Should().ContainSingle().Which.Filename.Should().Be("piatto.jpg");
    }

    [Fact]
    public async Task Photos_travel_in_the_draft_of_the_same_save_not_in_a_second_call()
    {
        var (sut, diary, photos, dialog, _, _, importer) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        dialog.MultipleResult = ["C:\\tmp\\a.jpg"];
        sut.AddPhotoCommand.Execute(null);

        // La foto si importa alla scelta (decisione 3); prima del salvataggio non esiste nessuna nota.
        diary.SavedEntries.Should().BeEmpty();
        importer.ImportCalls.Should().ContainSingle();
        sut.PendingPhotos.Should().ContainSingle();

        sut.Draft = "colazione";
        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedPhotos.Single().NoteId.Should().Be(diary.SavedEntries.Single().NoteId);
        diary.SavedPhotos.Single().Photos.Should().ContainSingle();
        photos.AddCalls.Should().BeEmpty("la foto non passa più da IAttachmentService.AddAsync");
        sut.PendingPhotos.Should().BeEmpty();
    }

    [Fact]
    public async Task A_failing_photo_does_not_lose_the_entry_or_the_other_photos()
    {
        var (sut, diary, _, dialog, _, _, importer) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        importer.ThrowOnImport["C:\\tmp\\rotta.jpg"] = new IOException("locked");
        dialog.MultipleResult = ["C:\\tmp\\rotta.jpg", "C:\\tmp\\buona.jpg"];
        await sut.AddPhotoCommand.ExecuteAsync(null);
        sut.ErrorMessage.Should().NotBeNull("l'errore si vede subito, alla scelta");
        sut.Draft = "pranzo";

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().ContainSingle("la voce resta salvata anche se la foto fallisce");
        diary.SavedPhotos.Single().Photos.Should().ContainSingle().Which.Filename.Should().Be("buona.jpg");
    }

    [Fact]
    public async Task Save_uses_the_selected_day_so_yesterdays_dinner_lands_on_yesterday()
    {
        var (sut, diary, _, _, _, _, _) = Build();
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
        var (sut, diary, _, _, confirm, _, _) = Build();
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
        var (sut, diary, _, _, _, _, _) = Build();
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
        var (sut, diary, _, _, _, _, _) = Build();
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
        var (sut, diary, _, _, _, _, _) = Build();
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
        var (sut, diary, _, _, _, _, _) = Build();
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
        var (sut, _, _, _, _, notes, _) = Build();
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

    // ---- F2d-1 Task 6: tag e foto ----

    [Fact]
    public async Task Save_sends_text_tag_row_and_photos_as_one_draft()
    {
        var (sut, diary, _, dialog, _, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        dialog.MultipleResult = ["C:\\tmp\\a.jpg", "C:\\tmp\\b.jpg"];
        await sut.AddPhotoCommand.ExecuteAsync(null);
        sut.Draft = "pranzo #fuori";
        sut.PropertyTags.Add("salute");

        await sut.SaveCommand.ExecuteAsync(null);

        var draft = diary.Drafts.Should().ContainSingle().Subject;
        draft.Text.Should().Be("pranzo #fuori");
        draft.PropertyTags.Should().Equal("salute");
        draft.Photos.Select(p => p.Filename).Should().Equal("a.jpg", "b.jpg");
        sut.PropertyTags.Tags.Should().BeEmpty();
        sut.PendingPhotos.Should().BeEmpty();
    }

    [Fact]
    public async Task A_failing_save_leaves_draft_tags_and_photos_where_the_user_left_them()
    {
        var (sut, diary, _, dialog, _, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        dialog.MultipleResult = ["C:\\tmp\\a.jpg"];
        await sut.AddPhotoCommand.ExecuteAsync(null);
        sut.Draft = "pranzo";
        sut.PropertyTags.Add("salute");
        diary.ThrowOnWrite = new InvalidOperationException("database is locked");

        await sut.SaveCommand.ExecuteAsync(null);

        sut.Draft.Should().Be("pranzo");
        sut.PropertyTags.Tags.Should().Equal("salute");
        sut.PendingPhotos.Should().ContainSingle();
        sut.ErrorMessage.Should().Be("database is locked");
        sut.IsSaving.Should().BeFalse();
    }

    [Fact]
    public async Task A_rejected_file_names_itself_and_does_not_stop_the_others()
    {
        var (sut, _, _, dialog, _, _, importer) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        importer.ThrowOnImport["C:\\tmp\\b.heic"] = new PhotoImportException("b.heic", PhotoImportFailure.Unsupported);
        dialog.MultipleResult = ["C:\\tmp\\a.jpg", "C:\\tmp\\b.heic", "C:\\tmp\\c.jpg"];

        await sut.AddPhotoCommand.ExecuteAsync(null);

        sut.PendingPhotos.Select(p => p.Filename).Should().Equal("a.jpg", "c.jpg");
        sut.ErrorMessage.Should().Be(string.Format(Strings.Diario_QuickEntry_Error_PhotoUnsupported, "b.heic"));
    }

    [Fact]
    public void An_entry_shows_its_time_in_the_local_zone()
    {
        var rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        var clock = new FakeClock(localTimeZone: rome);
        var note = new Note
        {
            Id = Guid.NewGuid(), BodyMd = "x",
            CreatedAt = new DateTime(2026, 7, 1, 11, 42, 0, DateTimeKind.Utc),
        };

        new JournalEntryRowViewModel(note, [], clock).TimeDisplay.Should().Be("13:42");
    }

    [Fact]
    public async Task Edit_applies_added_and_removed_photos_only_on_save_and_cancel_discards_them()
    {
        var (sut, diary, atts, dialog, _, _, _) = Build();
        var node = Journal();
        await sut.LoadAsync(node, DayBoundary.Utc);
        sut.Draft = "colazione";
        await sut.SaveCommand.ExecuteAsync(null);
        var noteId = diary.SavedEntries.Single().NoteId;
        var kept = new Attachment { Id = Guid.NewGuid(), NoteId = noteId, Filename = "old.jpg", MimeType = "image/jpeg" };
        atts.Store[kept.Id] = kept;
        await sut.LoadAsync(node, DayBoundary.Utc);
        var row = sut.Entries.Single();
        row.Photos.Should().ContainSingle();

        // Annulla: nessuna chiamata, la riga com'era.
        sut.BeginEditCommand.Execute(row);
        sut.RemoveEditPhotoCommand.Execute(row.Photos.Single());
        dialog.MultipleResult = ["C:\\tmp\\new.jpg"];
        await sut.AddPhotoCommand.ExecuteAsync(null);
        row.Photos.Should().BeEmpty();
        row.AddedPhotos.Should().ContainSingle();
        sut.CancelEditCommand.Execute(null);
        diary.Edits.Should().BeEmpty();
        row.Photos.Should().ContainSingle().Which.Id.Should().Be(kept.Id);
        row.AddedPhotos.Should().BeEmpty();
        row.RemovedPhotoIds.Should().BeEmpty();

        // Salva: una sola DiaryEntryEdit con entrambe.
        sut.BeginEditCommand.Execute(row);
        sut.RemoveEditPhotoCommand.Execute(row.Photos.Single());
        await sut.AddPhotoCommand.ExecuteAsync(null);
        await sut.SaveCommand.ExecuteAsync(null);

        var edit = diary.Edits.Should().ContainSingle().Subject.Edit;
        edit.RemovedPhotoIds.Should().Equal(kept.Id);
        edit.AddedPhotos.Select(p => p.Filename).Should().Equal("new.jpg");
        edit.PropertyTags.Should().BeEmpty("la riga Tag si manda sempre in modifica");
    }

    [Fact]
    public async Task A_photo_picked_while_a_save_is_in_flight_is_refused_not_lost()
    {
        var (sut, diary, _, dialog, _, _, _) = Build();
        await sut.LoadAsync(Journal(), DayBoundary.Utc);
        sut.Draft = "cena";
        var gate = new TaskCompletionSource();
        diary.NextWriteDelay = () => gate.Task;
        var save = sut.SaveCommand.ExecuteAsync(null);

        dialog.MultipleResult = ["C:\\tmp\\late.jpg"];
        await sut.AddPhotoCommand.ExecuteAsync(null);
        gate.SetResult();
        await save;

        sut.PendingPhotos.Should().BeEmpty("la scelta durante il salvataggio e' rifiutata, non persa in silenzio");
        diary.Drafts.Single().Photos.Should().BeEmpty();
    }

    [Fact]
    public async Task A_failing_edit_save_keeps_the_edit_open_with_everything_in_it()
    {
        var (sut, diary, atts, dialog, _, _, _) = Build();
        var node = Journal();
        await sut.LoadAsync(node, DayBoundary.Utc);
        sut.Draft = "colazione";
        await sut.SaveCommand.ExecuteAsync(null);
        var noteId = diary.SavedEntries.Single().NoteId;
        atts.Store[Guid.NewGuid()] = new Attachment { Id = Guid.NewGuid(), NoteId = noteId, Filename = "old.jpg", MimeType = "image/jpeg" };
        await sut.LoadAsync(node, DayBoundary.Utc);
        var row = sut.Entries.Single();
        sut.BeginEditCommand.Execute(row);
        row.EditDraft = "nuovo testo";
        row.EditTags!.Add("salute");
        sut.RemoveEditPhotoCommand.Execute(row.Photos.Single());
        dialog.MultipleResult = ["C:\\tmp\\new.jpg"];
        await sut.AddPhotoCommand.ExecuteAsync(null);
        diary.ThrowOnWrite = new InvalidOperationException("database is locked");

        await sut.SaveCommand.ExecuteAsync(null);

        row.IsEditing.Should().BeTrue();
        row.EditDraft.Should().Be("nuovo testo");
        row.EditTags!.Tags.Should().Equal("salute");
        row.AddedPhotos.Should().ContainSingle();
        row.RemovedPhotoIds.Should().ContainSingle();
        sut.ErrorMessage.Should().Be("database is locked");
        sut.IsSaving.Should().BeFalse();
    }
}
