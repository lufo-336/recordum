using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Journal;
using Xunit;

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
        await sut.LoadAsync(Journal());
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
        await sut.LoadAsync(Journal());
        sut.Draft = "   ";

        await sut.SaveCommand.ExecuteAsync(null);

        diary.SavedEntries.Should().BeEmpty();
        sut.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task Save_accepts_a_photo_with_no_text()
    {
        var (sut, diary, photos, dialog, _, _) = Build();
        await sut.LoadAsync(Journal());
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
        await sut.LoadAsync(Journal());
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
        await sut.LoadAsync(Journal());
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
        await sut.LoadAsync(Journal());
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
        await sut.LoadAsync(Journal());
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
        await sut.LoadAsync(Journal());
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
}
