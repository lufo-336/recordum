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
}
