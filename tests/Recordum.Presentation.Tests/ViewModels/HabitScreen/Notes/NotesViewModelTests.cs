using System.IO;
using FluentAssertions;
using Recordum.Core.Models;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Notes;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.Notes;

public class NotesViewModelTests
{
    private static NotesViewModel CreateSut(out FakeNoteService notes, out FakeEventBus bus, out FakeClock clock)
        => CreateSut(out notes, out bus, out clock, out _, out _, out _);

    private static NotesViewModel CreateSut(
        out FakeNoteService notes,
        out FakeEventBus bus,
        out FakeClock clock,
        out FakeAttachmentService attachments,
        out FakeOpenFileDialog dialog)
        => CreateSut(out notes, out bus, out clock, out attachments, out dialog, out _);

    private static NotesViewModel CreateSut(
        out FakeNoteService notes,
        out FakeEventBus bus,
        out FakeClock clock,
        out FakeAttachmentService attachments,
        out FakeOpenFileDialog dialog,
        out FakeConfirmationDialog confirm,
        FakePhotoImporter? importer = null)
    {
        notes = new FakeNoteService();
        bus = new FakeEventBus();
        clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero));
        attachments = new FakeAttachmentService();
        dialog = new FakeOpenFileDialog();
        confirm = new FakeConfirmationDialog();
        return new NotesViewModel(notes, bus, clock, new FakeEditorAvailability(available: true), attachments, importer ?? new FakePhotoImporter(), dialog, confirm);
    }

    [Fact]
    public async Task LoadAsync_populates_Notes_ordered_by_updated_desc()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var habitId = Guid.NewGuid();
        var a = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "a", UpdatedAtMs = 1 };
        var b = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "b", UpdatedAtMs = 3 };
        var c = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "c", UpdatedAtMs = 2 };
        svc.Store[a.Id] = a;
        svc.Store[b.Id] = b;
        svc.Store[c.Id] = c;

        await sut.LoadAsync(habitId, DayBoundary.Utc);

        sut.Notes.Select(r => r.Note.BodyMd).Should().Equal("b", "c", "a");
    }

    [Fact]
    public async Task LoadAsync_clears_selection_and_editor_content()
    {
        var sut = CreateSut(out _, out _, out _);

        await sut.LoadAsync(Guid.NewGuid(), DayBoundary.Utc);

        sut.SelectedNote.Should().BeNull();
        sut.EditorMarkdown.Should().Be(string.Empty);
    }

    [Fact]
    public async Task NewAsync_creates_note_and_selects_it()
    {
        var sut = CreateSut(out var svc, out _, out _);
        await sut.LoadAsync(Guid.NewGuid(), DayBoundary.Utc);

        await sut.NewCommand.ExecuteAsync(null);

        svc.Store.Should().HaveCount(1);
        sut.Notes.Should().ContainSingle();
        sut.SelectedNote.Should().NotBeNull();
        sut.EditorMarkdown.Should().Be(string.Empty);
    }

    [Fact]
    public void Setting_SelectedNote_loads_BodyMd_into_EditorMarkdown_and_discards_unsaved_edits()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var habitId = Guid.NewGuid();
        var a = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "AAA", UpdatedAtMs = 1 };
        var b = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "BBB", UpdatedAtMs = 2 };
        svc.Store[a.Id] = a;
        svc.Store[b.Id] = b;
        sut.Notes.Add(new NoteRowViewModel(b, DayBoundary.Utc));
        sut.Notes.Add(new NoteRowViewModel(a, DayBoundary.Utc));

        sut.SelectedNote = sut.Notes[0];
        sut.EditorMarkdown.Should().Be("BBB");
        sut.EditorMarkdown = "dirty";

        sut.SelectedNote = sut.Notes[1];
        sut.EditorMarkdown.Should().Be("AAA");
    }

    [Fact]
    public void IsDirty_is_true_when_editor_differs_from_selected_body()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "orig" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];

        sut.IsDirty.Should().BeFalse();

        sut.EditorMarkdown = "edited";
        sut.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_persists_EditorMarkdown_and_clears_dirty()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "orig", UpdatedAtMs = 1 };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        sut.EditorMarkdown = "edited";

        await sut.SaveCommand.ExecuteAsync(null);

        svc.UpdateCalls.Should().ContainSingle().Which.Id.Should().Be(n.Id);
        svc.Store[n.Id].BodyMd.Should().Be("edited");
        sut.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void RequestDelete_sets_IsConfirmingDelete()
    {
        var sut = CreateSut(out _, out _, out _);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];

        sut.RequestDeleteCommand.Execute(null);

        sut.IsConfirmingDelete.Should().BeTrue();
    }

    [Fact]
    public void CancelDelete_clears_IsConfirmingDelete()
    {
        var sut = CreateSut(out _, out _, out _);
        sut.IsConfirmingDelete = true;

        sut.CancelDeleteCommand.Execute(null);

        sut.IsConfirmingDelete.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmDeleteAsync_removes_row_calls_service_and_clears_selection()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        sut.IsConfirmingDelete = true;

        await sut.ConfirmDeleteCommand.ExecuteAsync(null);

        svc.DeleteCalls.Should().Contain(n.Id);
        sut.Notes.Should().BeEmpty();
        sut.SelectedNote.Should().BeNull();
        sut.IsConfirmingDelete.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmDeleteAsync_selects_first_remaining_when_others_exist()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var habitId = Guid.NewGuid();
        var n1 = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "1", UpdatedAtMs = 2 };
        var n2 = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "2", UpdatedAtMs = 1 };
        svc.Store[n1.Id] = n1;
        svc.Store[n2.Id] = n2;
        sut.Notes.Add(new NoteRowViewModel(n1, DayBoundary.Utc));
        sut.Notes.Add(new NoteRowViewModel(n2, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        sut.IsConfirmingDelete = true;

        await sut.ConfirmDeleteCommand.ExecuteAsync(null);

        sut.SelectedNote.Should().NotBeNull();
        sut.SelectedNote!.Id.Should().Be(n2.Id);
    }

    [Fact]
    public async Task NoteCreated_event_from_another_source_refreshes_list()
    {
        var sut = CreateSut(out var svc, out var bus, out _);
        var habitId = Guid.NewGuid();
        await sut.LoadAsync(habitId, DayBoundary.Utc);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "external", UpdatedAtMs = 100 };
        svc.Store[n.Id] = n;

        bus.Publish(new Recordum.Plugins.Contracts.Events.NoteCreatedPayload(n.Id, habitId));
        await Task.Delay(20);

        sut.Notes.Should().ContainSingle().Which.Id.Should().Be(n.Id);
    }

    [Fact]
    public async Task NoteEvent_for_other_habit_is_ignored()
    {
        var sut = CreateSut(out _, out var bus, out _);
        var habitId = Guid.NewGuid();
        await sut.LoadAsync(habitId, DayBoundary.Utc);

        bus.Publish(new Recordum.Plugins.Contracts.Events.NoteCreatedPayload(Guid.NewGuid(), Guid.NewGuid()));
        await Task.Delay(20);

        sut.Notes.Should().BeEmpty();
    }

    [Fact]
    public async Task NoteDeleted_event_removes_row_from_list()
    {
        var sut = CreateSut(out var svc, out var bus, out _);
        var habitId = Guid.NewGuid();
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "x", UpdatedAtMs = 1 };
        svc.Store[n.Id] = n;
        await sut.LoadAsync(habitId, DayBoundary.Utc);
        sut.Notes.Should().ContainSingle();

        svc.Store.Remove(n.Id);
        bus.Publish(new Recordum.Plugins.Contracts.Events.NoteDeletedPayload(n.Id, habitId));
        await Task.Delay(20);

        sut.Notes.Should().BeEmpty();
    }

    [Fact]
    public void SaveCommand_CanExecute_only_when_dirty()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];

        sut.SaveCommand.CanExecute(null).Should().BeFalse();

        sut.EditorMarkdown = "y";
        sut.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task ShowHistoryCommand_loads_versions_and_enters_history()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var habitId = Guid.NewGuid();
        var note = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "v2", UpdatedAtMs = 5 };
        svc.Store[note.Id] = note;
        svc.Versions[note.Id] =
        [
            new Recordum.Core.Abstractions.Services.Dtos.NoteVersion(Guid.NewGuid(), "v2", new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc)),
        ];
        await sut.LoadAsync(habitId, DayBoundary.Utc);
        var row = sut.Notes.Single();

        await sut.ShowHistoryCommand.ExecuteAsync(row);

        sut.IsShowingHistory.Should().BeTrue();
        sut.SelectedNote.Should().Be(row);
        sut.History.Versions.Should().ContainSingle();
    }

    [Fact]
    public async Task History_restore_leaves_history_panel()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var habitId = Guid.NewGuid();
        var note = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "v2", UpdatedAtMs = 5 };
        svc.Store[note.Id] = note;
        svc.Versions[note.Id] = [ new Recordum.Core.Abstractions.Services.Dtos.NoteVersion(Guid.NewGuid(), "v1", new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc)) ];
        await sut.LoadAsync(habitId, DayBoundary.Utc);
        await sut.ShowHistoryCommand.ExecuteAsync(sut.Notes.Single());

        sut.History.SelectedVersion = sut.History.Versions[0];
        await sut.History.RestoreCommand.ExecuteAsync(null);

        sut.IsShowingHistory.Should().BeFalse();
    }

    [Fact]
    public async Task HideHistoryCommand_returns_to_editor()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var habitId = Guid.NewGuid();
        var note = new Note { Id = Guid.NewGuid(), HabitNodeId = habitId, BodyMd = "x", UpdatedAtMs = 1 };
        svc.Store[note.Id] = note;
        svc.Versions[note.Id] = [];
        await sut.LoadAsync(habitId, DayBoundary.Utc);
        await sut.ShowHistoryCommand.ExecuteAsync(sut.Notes.Single());

        sut.HideHistoryCommand.Execute(null);

        sut.IsShowingHistory.Should().BeFalse();
    }

    [Fact]
    public async Task Default_period_All_shows_every_note()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var habitId = Guid.NewGuid();
        svc.Store[Guid.NewGuid()] = new Note { HabitNodeId = habitId, BodyMd = "a", UpdatedAtMs = 1, TargetDate = new DateOnly(2026, 1, 1) };
        svc.Store[Guid.NewGuid()] = new Note { HabitNodeId = habitId, BodyMd = "b", UpdatedAtMs = 2, TargetDate = new DateOnly(2026, 6, 1) };

        await sut.LoadAsync(habitId, DayBoundary.Utc);

        sut.Notes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Day_mode_filters_notes_to_the_selected_calendar_day()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var habitId = Guid.NewGuid();
        svc.Store[Guid.NewGuid()] = new Note { HabitNodeId = habitId, BodyMd = "kept", UpdatedAtMs = 2, TargetDate = new DateOnly(2026, 6, 19) };
        svc.Store[Guid.NewGuid()] = new Note { HabitNodeId = habitId, BodyMd = "other", UpdatedAtMs = 1, TargetDate = new DateOnly(2026, 6, 18) };
        await sut.LoadAsync(habitId, DayBoundary.Utc);

        sut.SetCalendarDay(new DateOnly(2026, 6, 19));
        sut.Mode = EntriesRecordsMode.Giorno;
        await Task.Delay(20);

        sut.Notes.Should().ContainSingle().Which.Note.BodyMd.Should().Be("kept");
    }

    [Fact]
    public async Task AddAttachmentCommand_is_noop_when_no_note_selected()
    {
        var sut = CreateSut(out _, out _, out _, out var attachments, out var dialog);
        dialog.MultipleResult = ["C:\\photos\\a.jpg"];

        await sut.AddAttachmentCommand.ExecuteAsync(null);

        attachments.AddCalls.Should().BeEmpty();
        sut.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task AddAttachmentCommand_adds_one_attachment_per_selected_file()
    {
        var sut = CreateSut(out var svc, out _, out _, out var attachments, out var dialog);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        dialog.MultipleResult = ["C:\\photos\\a.jpg", "C:\\photos\\b.png"];

        await sut.AddAttachmentCommand.ExecuteAsync(null);

        attachments.AddCalls.Should().HaveCount(2);
        attachments.AddCalls.Should().OnlyContain(c => c.NoteId == n.Id);
        sut.Attachments.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddAttachmentCommand_continues_batch_when_one_file_throws()
    {
        var importer = new FakePhotoImporter();
        var sut = CreateSut(out var svc, out _, out _, out _, out var dialog, out _, importer);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        dialog.MultipleResult = ["C:\\photos\\bad.jpg", "C:\\photos\\good.jpg"];
        importer.ThrowOnImport["C:\\photos\\bad.jpg"] = new IOException("locked");

        await sut.AddAttachmentCommand.ExecuteAsync(null);

        sut.Attachments.Should().ContainSingle();
    }

    [Fact]
    public async Task AddAttachmentCommand_does_not_leak_into_a_different_note_selected_mid_batch()
    {
        var sut = CreateSut(out var svc, out _, out _, out var attachments, out var dialog);
        var n1 = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "one" };
        var n2 = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "two" };
        svc.Store[n1.Id] = n1;
        svc.Store[n2.Id] = n2;
        sut.Notes.Add(new NoteRowViewModel(n1, DayBoundary.Utc));
        sut.Notes.Add(new NoteRowViewModel(n2, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        dialog.MultipleResult = ["C:\\photos\\a.jpg"];
        attachments.AddGate = new TaskCompletionSource<bool>();

        // Start the add for n1 but don't let AddAsync complete yet.
        var addTask = sut.AddAttachmentCommand.ExecuteAsync(null);

        // User switches to n2 before the add resolves.
        sut.SelectedNote = sut.Notes[1];

        // Now let the in-flight add for n1 complete.
        attachments.AddGate.SetResult(true);
        await addTask;

        attachments.AddCalls.Should().ContainSingle(c => c.NoteId == n1.Id);
        sut.Attachments.Should().NotContain(a => a.NoteId == n1.Id);
    }

    [Fact]
    public async Task DeleteAttachmentCommand_removes_from_collection_and_calls_service()
    {
        var sut = CreateSut(out var svc, out _, out _, out var attachments, out var dialog);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        dialog.MultipleResult = ["C:\\photos\\a.jpg"];
        await sut.AddAttachmentCommand.ExecuteAsync(null);
        var added = sut.Attachments.Single();

        await sut.DeleteAttachmentCommand.ExecuteAsync(added);

        attachments.DeleteCalls.Should().ContainSingle().Which.Should().Be(added.Id);
        sut.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAttachmentCommand_is_noop_when_null()
    {
        var sut = CreateSut(out _, out _, out _, out var attachments, out _);

        await sut.DeleteAttachmentCommand.ExecuteAsync(null);

        attachments.DeleteCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAttachmentCommand_asks_for_confirmation_before_deleting()
    {
        var sut = CreateSut(out var svc, out _, out _, out var attachments, out var dialog, out var confirm);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        dialog.MultipleResult = ["C:\\photos\\a.jpg"];
        await sut.AddAttachmentCommand.ExecuteAsync(null);
        var added = sut.Attachments.Single();

        await sut.DeleteAttachmentCommand.ExecuteAsync(added);

        confirm.Calls.Should().Be(1);
        attachments.DeleteCalls.Should().ContainSingle().Which.Should().Be(added.Id);
        sut.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAttachmentCommand_declined_confirmation_keeps_the_attachment()
    {
        var sut = CreateSut(out var svc, out _, out _, out var attachments, out var dialog, out var confirm);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        dialog.MultipleResult = ["C:\\photos\\a.jpg"];
        await sut.AddAttachmentCommand.ExecuteAsync(null);
        var added = sut.Attachments.Single();
        confirm.Result = false;

        await sut.DeleteAttachmentCommand.ExecuteAsync(added);

        attachments.DeleteCalls.Should().BeEmpty();
        sut.Attachments.Should().ContainSingle();
    }

    [Fact]
    public async Task Selecting_a_note_loads_its_attachments()
    {
        var sut = CreateSut(out var svc, out _, out _, out var attachments, out _);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        attachments.Store[Guid.NewGuid()] = new Recordum.Core.Models.Attachment
        {
            Id = Guid.NewGuid(), NoteId = n.Id, Filename = "existing.png", MimeType = "image/png", FileSizeBytes = 10,
        };
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));

        sut.SelectedNote = sut.Notes[0];
        await Task.Delay(20);

        sut.Attachments.Should().ContainSingle().Which.Filename.Should().Be("existing.png");
    }

    [Fact]
    public async Task Switching_selection_away_clears_attachments()
    {
        var sut = CreateSut(out var svc, out _, out _, out var attachments, out _);
        var n1 = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "1" };
        var n2 = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "2" };
        svc.Store[n1.Id] = n1;
        svc.Store[n2.Id] = n2;
        attachments.Store[Guid.NewGuid()] = new Recordum.Core.Models.Attachment
        {
            Id = Guid.NewGuid(), NoteId = n1.Id, Filename = "a.png", MimeType = "image/png", FileSizeBytes = 10,
        };
        sut.Notes.Add(new NoteRowViewModel(n1, DayBoundary.Utc));
        sut.Notes.Add(new NoteRowViewModel(n2, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        await Task.Delay(20);
        sut.Attachments.Should().ContainSingle();

        sut.SelectedNote = sut.Notes[1];

        sut.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenLightboxCommand_sets_attachment_and_opens()
    {
        var sut = CreateSut(out var svc, out _, out _, out _, out var dialog);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        dialog.MultipleResult = ["C:\\photos\\a.jpg"];
        await sut.AddAttachmentCommand.ExecuteAsync(null);
        var added = sut.Attachments.Single();

        sut.OpenLightboxCommand.Execute(added);

        sut.IsLightboxOpen.Should().BeTrue();
        sut.LightboxAttachment.Should().Be(added);
    }

    [Fact]
    public void OpenLightboxCommand_is_noop_when_null()
    {
        var sut = CreateSut(out _, out _, out _);

        sut.OpenLightboxCommand.Execute(null);

        sut.IsLightboxOpen.Should().BeFalse();
        sut.LightboxAttachment.Should().BeNull();
    }

    [Fact]
    public async Task CloseLightboxCommand_clears_state()
    {
        var sut = CreateSut(out var svc, out _, out _, out _, out var dialog);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        dialog.MultipleResult = ["C:\\photos\\a.jpg"];
        await sut.AddAttachmentCommand.ExecuteAsync(null);
        sut.OpenLightboxCommand.Execute(sut.Attachments.Single());

        sut.CloseLightboxCommand.Execute(null);

        sut.IsLightboxOpen.Should().BeFalse();
        sut.LightboxAttachment.Should().BeNull();
    }

    [Fact]
    public async Task LightboxNextCommand_wraps_around_to_first()
    {
        var sut = CreateSut(out var svc, out _, out _, out _, out var dialog);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        dialog.MultipleResult = ["C:\\photos\\a.jpg", "C:\\photos\\b.jpg", "C:\\photos\\c.jpg"];
        await sut.AddAttachmentCommand.ExecuteAsync(null);
        sut.OpenLightboxCommand.Execute(sut.Attachments[2]);

        sut.LightboxNextCommand.Execute(null);

        sut.LightboxAttachment.Should().Be(sut.Attachments[0]);
    }

    [Fact]
    public async Task LightboxPreviousCommand_wraps_around_to_last()
    {
        var sut = CreateSut(out var svc, out _, out _, out _, out var dialog);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        dialog.MultipleResult = ["C:\\photos\\a.jpg", "C:\\photos\\b.jpg", "C:\\photos\\c.jpg"];
        await sut.AddAttachmentCommand.ExecuteAsync(null);
        sut.OpenLightboxCommand.Execute(sut.Attachments[0]);

        sut.LightboxPreviousCommand.Execute(null);

        sut.LightboxAttachment.Should().Be(sut.Attachments[2]);
    }

    [Fact]
    public async Task CanNavigateLightbox_is_true_only_with_more_than_one_attachment()
    {
        var sut = CreateSut(out var svc, out _, out _, out _, out var dialog);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        dialog.MultipleResult = ["C:\\photos\\a.jpg"];
        await sut.AddAttachmentCommand.ExecuteAsync(null);

        sut.CanNavigateLightbox.Should().BeFalse();

        dialog.MultipleResult = ["C:\\photos\\b.jpg"];
        await sut.AddAttachmentCommand.ExecuteAsync(null);

        sut.CanNavigateLightbox.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAttachmentCommand_closes_lightbox_when_deleting_the_open_photo()
    {
        var sut = CreateSut(out var svc, out _, out _, out _, out var dialog);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        dialog.MultipleResult = ["C:\\photos\\a.jpg"];
        await sut.AddAttachmentCommand.ExecuteAsync(null);
        var added = sut.Attachments.Single();
        sut.OpenLightboxCommand.Execute(added);

        await sut.DeleteAttachmentCommand.ExecuteAsync(added);

        sut.IsLightboxOpen.Should().BeFalse();
        sut.LightboxAttachment.Should().BeNull();
    }

    [Fact]
    public async Task Switching_selected_note_closes_the_lightbox()
    {
        var sut = CreateSut(out var svc, out _, out _, out _, out var dialog);
        var n1 = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "1" };
        var n2 = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "2" };
        svc.Store[n1.Id] = n1;
        svc.Store[n2.Id] = n2;
        sut.Notes.Add(new NoteRowViewModel(n1, DayBoundary.Utc));
        sut.Notes.Add(new NoteRowViewModel(n2, DayBoundary.Utc));
        sut.SelectedNote = sut.Notes[0];
        dialog.MultipleResult = ["C:\\photos\\a.jpg"];
        await sut.AddAttachmentCommand.ExecuteAsync(null);
        sut.OpenLightboxCommand.Execute(sut.Attachments.Single());

        sut.SelectedNote = sut.Notes[1];

        sut.IsLightboxOpen.Should().BeFalse();
    }
}
