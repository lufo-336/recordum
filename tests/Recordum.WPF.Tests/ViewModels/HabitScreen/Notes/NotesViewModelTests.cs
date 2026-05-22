using FluentAssertions;
using Microsoft.Web.WebView2.Core;
using Recordum.Core.Models;
using Recordum.WPF.Bootstrap;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Notes;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Notes;

public class NotesViewModelTests
{
    private sealed class FakeWebView2Availability(bool available) : IWebView2Availability
    {
        public bool IsAvailable { get; } = available;
        public CoreWebView2Environment? Environment => null;
        public Task ProbeAsync() => Task.CompletedTask;
    }

    private static NotesViewModel CreateSut(out FakeNoteService notes, out FakeEventBus bus, out FakeClock clock)
    {
        notes = new FakeNoteService();
        bus = new FakeEventBus();
        clock = new FakeClock(new DateTimeOffset(2026, 5, 22, 10, 0, 0, TimeSpan.Zero));
        return new NotesViewModel(notes, bus, clock, new FakeWebView2Availability(available: true));
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

        await sut.LoadAsync(habitId);

        sut.Notes.Select(r => r.Note.BodyMd).Should().Equal("b", "c", "a");
    }

    [Fact]
    public async Task LoadAsync_clears_selection_and_editor_content()
    {
        var sut = CreateSut(out _, out _, out _);

        await sut.LoadAsync(Guid.NewGuid());

        sut.SelectedNote.Should().BeNull();
        sut.EditorMarkdown.Should().Be(string.Empty);
    }

    [Fact]
    public async Task NewAsync_creates_note_and_selects_it()
    {
        var sut = CreateSut(out var svc, out _, out _);
        await sut.LoadAsync(Guid.NewGuid());

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
        sut.Notes.Add(new NoteRowViewModel(b));
        sut.Notes.Add(new NoteRowViewModel(a));

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
        sut.Notes.Add(new NoteRowViewModel(n));
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
        sut.Notes.Add(new NoteRowViewModel(n));
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
        sut.Notes.Add(new NoteRowViewModel(n));
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
        sut.Notes.Add(new NoteRowViewModel(n));
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
        sut.Notes.Add(new NoteRowViewModel(n1));
        sut.Notes.Add(new NoteRowViewModel(n2));
        sut.SelectedNote = sut.Notes[0];
        sut.IsConfirmingDelete = true;

        await sut.ConfirmDeleteCommand.ExecuteAsync(null);

        sut.SelectedNote.Should().NotBeNull();
        sut.SelectedNote!.Id.Should().Be(n2.Id);
    }

    [Fact]
    public void SaveCommand_CanExecute_only_when_dirty()
    {
        var sut = CreateSut(out var svc, out _, out _);
        var n = new Note { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), BodyMd = "x" };
        svc.Store[n.Id] = n;
        sut.Notes.Add(new NoteRowViewModel(n));
        sut.SelectedNote = sut.Notes[0];

        sut.SaveCommand.CanExecute(null).Should().BeFalse();

        sut.EditorMarkdown = "y";
        sut.SaveCommand.CanExecute(null).Should().BeTrue();
    }
}
