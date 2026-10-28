using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Text;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

/// <summary>Modalita' diario del composer (F2d-2, Task 3): una superficie per voce nuova e voce in modifica,
/// immagini come riferimenti nel testo, conferma prima di scartare una modifica aperta.</summary>
public class JournalDiaryModeTests
{
    private static HabitNode Journal() => new()
    {
        Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Journal, Name = "Diary",
    };

    private sealed record Rig(
        JournalQuickEntryViewModel Sut, FakeDiaryService Diary, FakeAttachmentService Atts,
        FakeOpenFileDialog Dialog, FakeConfirmationDialog Confirm, HabitNode Node);

    private static async Task<Rig> BuildAsync(bool diaryMode = true)
    {
        var notes = new FakeNoteService();
        var diary = new FakeDiaryService(notes);
        var atts = new FakeAttachmentService();
        var dialog = new FakeOpenFileDialog();
        var confirm = new FakeConfirmationDialog();
        var sut = new JournalQuickEntryViewModel(diary, notes, atts, new FakePhotoImporter(), new FakeTagService(), dialog, confirm, new FakeClock(), new FakeAppSettingService());
        var node = Journal();
        await sut.LoadAsync(node, DayBoundary.Utc, diaryMode);
        return new Rig(sut, diary, atts, dialog, confirm, node);
    }

    /// <summary>Salva una voce con il testo dato e le foto allegate; la ricarica e torna la sua riga.</summary>
    private static async Task<(JournalEntryRowViewModel Row, Guid[] PhotoIds)> SeedEntryAsync(
        Rig r, string text, int photos, Func<Guid[], string>? textFromIds = null)
    {
        var ids = Enumerable.Range(0, photos).Select(_ => Guid.NewGuid()).ToArray();
        r.Sut.Draft = textFromIds?.Invoke(ids) ?? text;
        foreach (var id in ids) r.Sut.PendingPhotos.Add(new ImportedPhoto(id, "a.jpg", "image/jpeg", [1], [2]));
        await r.Sut.SaveCommand.ExecuteAsync(null);
        var saved = r.Diary.SavedEntries.Last().NoteId;
        foreach (var id in ids)
            r.Atts.Store[id] = new Attachment { Id = id, NoteId = saved, Filename = "a.jpg", MimeType = "image/jpeg" };
        await r.Sut.LoadAsync(r.Node, DayBoundary.Utc, r.Sut.IsDiaryMode);
        return (r.Sut.Entries.Single(), ids);
    }

    [Fact]
    public async Task InsertPhotos_puts_a_reference_line_at_the_caret_and_keeps_the_photo_pending()
    {
        var r = await BuildAsync();
        r.Dialog.MultipleResult = ["/x/x.jpg"];

        var result = await r.Sut.InsertPhotosAsync("Cena\n", 5, default);

        var photo = r.Sut.PendingPhotos.Should().ContainSingle().Subject;
        var reference = PhotoRefs.Format(photo.Id, "x.jpg");
        result.Should().Be(($"Cena\n{reference}\n", 5 + reference.Length + 1));
        r.Sut.PhotoThumbnails[photo.Id].Should().Equal(FakePhotoImporter.FakeThumbnail);
    }

    [Fact]
    public async Task InsertPhotos_in_mid_line_starts_a_new_line_and_returns_null_when_nothing_entered()
    {
        var r = await BuildAsync();
        r.Dialog.MultipleResult = ["/x/x.jpg"];
        var (text, _) = (await r.Sut.InsertPhotosAsync("ab", 1, default))!.Value;
        text.Should().StartWith("a\n![x.jpg](photo:").And.EndWith(")\nb");

        r.Dialog.MultipleResult = [];
        (await r.Sut.InsertPhotosAsync("ab", 1, default)).Should().BeNull();
    }

    [Fact]
    public async Task Save_drops_a_pending_photo_whose_reference_was_deleted_and_strips_unknown_references()
    {
        var r = await BuildAsync();
        r.Dialog.MultipleResult = ["/x/x.jpg"];
        await r.Sut.InsertPhotosAsync("Cena\n", 5, default);
        var unknown = PhotoRefs.Format(Guid.NewGuid(), "ghost.jpg");

        // Riferimento cancellato dal testo (resta un id sconosciuto): la foto non entra, il riferimento sconosciuto si toglie.
        r.Sut.ActiveText = $"Prima {unknown} dopo";
        await r.Sut.SaveCommand.ExecuteAsync(null);

        var draft = r.Diary.Drafts.Single();
        draft.Photos.Should().BeEmpty();
        draft.Text.Should().NotContain("photo:").And.Contain("Prima").And.Contain("dopo");
    }

    [Fact]
    public async Task Save_keeps_a_pending_photo_whose_reference_is_still_in_the_text()
    {
        var r = await BuildAsync();
        r.Dialog.MultipleResult = ["/x/x.jpg"];
        var (text, _) = (await r.Sut.InsertPhotosAsync("Cena\n", 5, default))!.Value;
        r.Sut.ActiveText = text;

        await r.Sut.SaveCommand.ExecuteAsync(null);

        r.Diary.Drafts.Single().Photos.Should().ContainSingle();
        r.Diary.Drafts.Single().Text.Should().Contain("photo:");
    }

    [Fact]
    public async Task Editing_only_the_text_keeps_a_photo_that_was_never_referenced()
    {
        var r = await BuildAsync(diaryMode: false);
        await SeedEntryAsync(r, "nota veloce", photos: 1);
        await r.Sut.LoadAsync(r.Node, DayBoundary.Utc, isDiaryMode: true);
        var row = r.Sut.Entries.Single();

        await r.Sut.BeginEditCommand.ExecuteAsync(row);
        r.Sut.ActiveText = "nota veloce, corretta";
        await r.Sut.SaveCommand.ExecuteAsync(null);

        r.Diary.Edits.Should().ContainSingle().Which.Edit.RemovedPhotoIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_a_reference_in_edit_removes_that_photo_only()
    {
        var r = await BuildAsync();
        var (row, ids) = await SeedEntryAsync(r, "", photos: 2,
            ids => $"{PhotoRefs.Format(ids[0], "a.jpg")}\n{PhotoRefs.Format(ids[1], "a.jpg")}\nciao");

        await r.Sut.BeginEditCommand.ExecuteAsync(row);
        r.Sut.ActiveText = $"{PhotoRefs.Format(ids[1], "a.jpg")}\nciao";
        await r.Sut.SaveCommand.ExecuteAsync(null);

        var edit = r.Diary.Edits.Should().ContainSingle().Subject.Edit;
        edit.RemovedPhotoIds.Should().Equal(ids[0]);
        edit.Text.Should().Contain(ids[1].ToString("N"));
    }

    [Fact]
    public async Task UnreferencedPhotos_lists_only_the_photos_the_text_does_not_reference()
    {
        var r = await BuildAsync();
        var (row, ids) = await SeedEntryAsync(r, "", photos: 2,
            ids => $"{PhotoRefs.Format(ids[0], "a.jpg")}\nciao");

        row.UnreferencedPhotos.Select(p => p.Id).Should().Equal(ids[1]);
    }

    [Fact]
    public async Task BeginEdit_moves_the_entry_into_the_editor_and_Cancel_gives_the_draft_back()
    {
        var r = await BuildAsync();
        var (row, _) = await SeedEntryAsync(r, "Pranzo", photos: 0);
        r.Sut.Draft = "bozza nuova";
        var seen = new List<string?>();
        r.Sut.PropertyChanged += (_, e) => seen.Add(e.PropertyName);

        await r.Sut.BeginEditCommand.ExecuteAsync(row);

        r.Sut.ActiveText.Should().Be("Pranzo");
        r.Sut.Draft.Should().Be("bozza nuova");
        r.Sut.EditingEntry.Should().BeSameAs(row);
        r.Sut.EditingBanner.Should().Contain(row.TimeDisplay);
        r.Sut.ActiveTags.Should().BeSameAs(row.EditTags);
        seen.Should().Contain(["ActiveText", "ActiveTags", "EditingEntry", "EditingBanner"]);

        r.Sut.CancelEditCommand.Execute(null);

        r.Sut.ActiveText.Should().Be("bozza nuova");
        r.Sut.EditingEntry.Should().BeNull();
        r.Sut.EditingBanner.Should().BeNull();
        r.Sut.ActiveTags.Should().BeSameAs(r.Sut.PropertyTags);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)] // il parametro muto: stessa domanda in nota veloce
    public async Task Switching_entry_with_unsaved_changes_asks_first(bool diaryMode)
    {
        var r = await BuildAsync(diaryMode);
        r.Sut.Draft = "uno"; await r.Sut.SaveCommand.ExecuteAsync(null);
        r.Sut.Draft = "due"; await r.Sut.SaveCommand.ExecuteAsync(null);
        var (first, second) = (r.Sut.Entries[0], r.Sut.Entries[1]);
        await r.Sut.BeginEditCommand.ExecuteAsync(first);
        first.EditDraft = first.Text + " modificata";

        r.Confirm.Result = false;
        await r.Sut.BeginEditCommand.ExecuteAsync(second);
        r.Confirm.Calls.Should().Be(1);
        first.IsEditing.Should().BeTrue();
        first.EditDraft.Should().EndWith("modificata");
        second.IsEditing.Should().BeFalse();

        r.Confirm.Result = true;
        await r.Sut.BeginEditCommand.ExecuteAsync(second);
        first.IsEditing.Should().BeFalse();
        second.IsEditing.Should().BeTrue();
    }

    // ---- Ruling 6: cambiare giorno con una modifica aperta ----

    [Theory]
    [InlineData(true)]
    [InlineData(false)] // il parametro muto: stessa domanda in nota veloce
    public async Task Changing_day_with_unsaved_changes_asks_and_no_keeps_the_edit(bool diaryMode)
    {
        var r = await BuildAsync(diaryMode);
        var (row, _) = await SeedEntryAsync(r, "Pranzo", photos: 0);
        await r.Sut.BeginEditCommand.ExecuteAsync(row);
        row.EditDraft = "Pranzo modificato";
        r.Confirm.Result = false;

        (await r.Sut.ConfirmDayChangeAsync()).Should().BeFalse();

        r.Confirm.Calls.Should().Be(1);
        row.IsEditing.Should().BeTrue();
        row.EditDraft.Should().Be("Pranzo modificato");
    }

    [Fact]
    public async Task Changing_day_with_unsaved_changes_and_yes_discards_the_edit()
    {
        var r = await BuildAsync();
        var (row, _) = await SeedEntryAsync(r, "Pranzo", photos: 0);
        r.Sut.Draft = "bozza";
        await r.Sut.BeginEditCommand.ExecuteAsync(row);
        row.EditDraft = "Pranzo modificato";
        r.Confirm.Result = true;

        (await r.Sut.ConfirmDayChangeAsync()).Should().BeTrue();

        r.Confirm.Calls.Should().Be(1);
        row.IsEditing.Should().BeFalse();
        r.Sut.EditingEntry.Should().BeNull();
        r.Sut.ActiveText.Should().Be("bozza", "la bozza della voce nuova resta");
    }

    [Fact]
    public async Task Changing_day_without_changes_does_not_ask_and_closes_the_edit()
    {
        var r = await BuildAsync();
        var (row, _) = await SeedEntryAsync(r, "Pranzo", photos: 0);
        await r.Sut.BeginEditCommand.ExecuteAsync(row);

        (await r.Sut.ConfirmDayChangeAsync()).Should().BeTrue();

        r.Confirm.Calls.Should().Be(0);
        row.IsEditing.Should().BeFalse();
    }

    [Fact]
    public async Task While_an_image_is_being_inserted_the_surface_and_the_day_do_not_change()
    {
        var r = await BuildAsync();
        var (row, _) = await SeedEntryAsync(r, "Pranzo", photos: 0);
        var gate = new TaskCompletionSource<string[]>();
        r.Dialog.PendingMultiple = gate.Task;

        var inserting = r.Sut.InsertPhotosAsync("Cena", 4, default);
        r.Sut.IsInserting.Should().BeTrue();
        await r.Sut.BeginEditCommand.ExecuteAsync(row);
        row.IsEditing.Should().BeFalse("Modifica aspetta la fine dell'inserimento");
        (await r.Sut.ConfirmDayChangeAsync()).Should().BeFalse("il giorno resta, senza chiedere");
        r.Confirm.Calls.Should().Be(0);

        gate.SetResult([]);
        await inserting;
        r.Sut.IsInserting.Should().BeFalse();
        await r.Sut.BeginEditCommand.ExecuteAsync(row);
        row.IsEditing.Should().BeTrue();
    }

    [Fact]
    public async Task UnreferencedPhotos_follows_the_saved_text_while_the_entry_is_edited()
    {
        var r = await BuildAsync();
        var (row, ids) = await SeedEntryAsync(r, "", photos: 2,
            ids => $"{PhotoRefs.Format(ids[0], "a.jpg")}\nciao");
        await r.Sut.BeginEditCommand.ExecuteAsync(row);

        row.EditDraft = "ciao"; // riferimento cancellato nell'editor: la riga mostra ancora il testo salvato, che lo contiene

        row.UnreferencedPhotos.Select(p => p.Id).Should().Equal(ids[1]);
    }

    [Fact]
    public async Task Switching_entry_without_changes_does_not_ask()
    {
        var r = await BuildAsync();
        r.Sut.Draft = "uno"; await r.Sut.SaveCommand.ExecuteAsync(null);
        r.Sut.Draft = "due"; await r.Sut.SaveCommand.ExecuteAsync(null);
        await r.Sut.BeginEditCommand.ExecuteAsync(r.Sut.Entries[0]);

        await r.Sut.BeginEditCommand.ExecuteAsync(r.Sut.Entries[1]);

        r.Confirm.Calls.Should().Be(0);
        r.Sut.Entries[1].IsEditing.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)] // il parametro muto: in nota veloce AddPhoto non tocca il testo
    public async Task AddPhoto_appends_the_reference_only_in_diary_mode(bool diaryMode, bool expectsRef)
    {
        var r = await BuildAsync(diaryMode);
        r.Dialog.MultipleResult = ["/x/x.jpg"];
        r.Sut.Draft = "Cena";

        await r.Sut.AddPhotoCommand.ExecuteAsync(null);
        var photo = r.Sut.PendingPhotos.Should().ContainSingle().Subject;
        r.Sut.ActiveText.Should().Be(expectsRef ? $"Cena\n{PhotoRefs.Format(photo.Id, "x.jpg")}\n" : "Cena");

        await r.Sut.SaveCommand.ExecuteAsync(null);
        r.Diary.Drafts.Single().Photos.Should().HaveCount(1, "la foto resta in entrambe le modalita'");
    }

    [Fact]
    public async Task ActiveText_ActiveTags_and_EditingBanner_notify_when_editing_starts_and_stops()
    {
        var r = await BuildAsync();
        var (row, _) = await SeedEntryAsync(r, "Pranzo", photos: 0);
        var seen = new List<string?>();
        r.Sut.PropertyChanged += (_, e) => seen.Add(e.PropertyName);

        await r.Sut.BeginEditCommand.ExecuteAsync(row);
        seen.Should().Contain(["ActiveText", "ActiveTags", "EditingBanner"]);

        seen.Clear();
        r.Sut.CancelEditCommand.Execute(null);
        seen.Should().Contain(["ActiveText", "ActiveTags", "EditingBanner"]);
    }

    [Fact]
    public async Task Edit_save_attaches_a_photo_added_in_the_edit_and_strips_a_dangling_reference()
    {
        var r = await BuildAsync();
        var (row, _) = await SeedEntryAsync(r, "Pranzo", photos: 0);
        await r.Sut.BeginEditCommand.ExecuteAsync(row);
        r.Dialog.MultipleResult = ["/x/n.jpg"];
        await r.Sut.AddPhotoCommand.ExecuteAsync(null); // in diario mette il riferimento in coda a EditDraft
        var added = row.AddedPhotos.Should().ContainSingle().Subject;
        r.Sut.ActiveText += $"{PhotoRefs.Format(Guid.NewGuid(), "ghost.jpg")}\nfine";

        await r.Sut.SaveCommand.ExecuteAsync(null);

        var edit = r.Diary.Edits.Should().ContainSingle().Subject.Edit;
        edit.AddedPhotos.Select(p => p.Id).Should().Equal(added.Id);
        edit.Text.Should().Contain(added.Id.ToString("N")).And.NotContain("ghost").And.EndWith("fine");
    }

    [Fact]
    public async Task Save_does_not_run_while_InsertPhotos_is_importing()
    {
        var r = await BuildAsync();
        r.Sut.Draft = "Cena";
        var gate = new TaskCompletionSource<string[]>();
        r.Dialog.PendingMultiple = gate.Task;

        var inserting = r.Sut.InsertPhotosAsync("Cena", 4, default);
        await r.Sut.SaveCommand.ExecuteAsync(null);
        r.Diary.Drafts.Should().BeEmpty();

        gate.SetResult([]);
        await inserting;
        await r.Sut.SaveCommand.ExecuteAsync(null);
        r.Diary.Drafts.Should().ContainSingle();
    }

    [Fact]
    public async Task Stripping_a_reference_that_filled_a_whole_line_removes_the_line()
    {
        var r = await BuildAsync();
        r.Sut.Draft = $"uno\n{PhotoRefs.Format(Guid.NewGuid(), "g.jpg")}\ndue {PhotoRefs.Format(Guid.NewGuid(), "h.jpg")}fine";

        await r.Sut.SaveCommand.ExecuteAsync(null);

        r.Diary.Drafts.Single().Text.Should().Be("uno\ndue fine");
    }

    // ---- F2d2-5: in modifica, le foto senza riferimento nel testo si tolgono con la croce ----

    [Fact]
    public async Task EditUnreferencedPhotos_is_the_rows_photos_not_referenced_in_the_edit_text_and_follows_EditDraft()
    {
        var r = await BuildAsync();
        var (row, ids) = await SeedEntryAsync(r, "", photos: 2, ids => $"{PhotoRefs.Format(ids[0], "a.jpg")}\nciao");
        row.EditUnreferencedPhotos.Should().BeEmpty("fuori dalla modifica non c'e' niente");
        await r.Sut.BeginEditCommand.ExecuteAsync(row);

        row.EditUnreferencedPhotos.Select(p => p.Id).Should().Equal(ids[1]);

        row.EditDraft = "ciao"; // riferimento cancellato: la foto 0 passa nell'elenco
        row.EditUnreferencedPhotos.Select(p => p.Id).Should().BeEquivalentTo([ids[0], ids[1]]);

        row.EditDraft = $"{PhotoRefs.Format(ids[0], "a.jpg")}\n{PhotoRefs.Format(ids[1], "a.jpg")}";
        row.EditUnreferencedPhotos.Should().BeEmpty("le foto nel testo sono inline");
    }

    [Fact]
    public async Task Removing_an_unreferenced_photo_in_the_editor_deletes_it_on_Save_and_Annulla_restores_it()
    {
        var r = await BuildAsync();
        var (row, ids) = await SeedEntryAsync(r, "ciao", photos: 1);
        await r.Sut.BeginEditCommand.ExecuteAsync(row);
        r.Sut.RemoveEditPhotoCommand.Execute(row.EditUnreferencedPhotos.Single());
        row.EditUnreferencedPhotos.Should().BeEmpty();

        r.Sut.CancelEditCommand.Execute(null);
        row.Photos.Select(p => p.Id).Should().Equal(ids);

        await r.Sut.BeginEditCommand.ExecuteAsync(row);
        r.Sut.RemoveEditPhotoCommand.Execute(row.EditUnreferencedPhotos.Single());
        await r.Sut.SaveCommand.ExecuteAsync(null);

        r.Diary.Edits.Should().ContainSingle().Which.Edit.RemovedPhotoIds.Should().Equal(ids);
    }

    [Fact]
    public async Task EditUnreferencedPhotos_is_not_notified_by_typing_that_leaves_the_set_unchanged()
    {
        var r = await BuildAsync();
        var (row, _) = await SeedEntryAsync(r, "ciao", photos: 1);
        await r.Sut.BeginEditCommand.ExecuteAsync(row);
        var raised = 0;
        row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(JournalEntryRowViewModel.EditUnreferencedPhotos)) raised++; };

        row.EditDraft = "ciao a";
        row.EditDraft = "ciao a tutti";

        raised.Should().Be(0);
    }
}
