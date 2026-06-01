using FluentAssertions;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Notes;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Notes;

public class DayNoteViewModelTests
{
    private static DayNoteViewModel NewSut(FakeNoteService notes)
        => new(notes, new FakeClock());

    [Fact]
    public async Task LoadDayAsync_with_no_existing_note_is_empty_and_cannot_save()
    {
        var sut = NewSut(new FakeNoteService());
        sut.SetContext(Guid.NewGuid());

        await sut.LoadDayAsync(new DateOnly(2026, 6, 10));

        sut.Text.Should().BeEmpty();
        sut.HasDate.Should().BeTrue();
        sut.CanSave.Should().BeFalse();
    }

    [Fact]
    public async Task Save_creates_a_note_anchored_to_the_day()
    {
        var notes = new FakeNoteService();
        var sut = NewSut(notes);
        var habitId = Guid.NewGuid();
        sut.SetContext(habitId);
        await sut.LoadDayAsync(new DateOnly(2026, 6, 10));

        sut.Text = "Good session";
        sut.CanSave.Should().BeTrue();
        await sut.SaveCommand.ExecuteAsync(null);

        var persisted = await notes.ListByHabitAndDateAsync(habitId, new DateOnly(2026, 6, 10));
        persisted.Should().ContainSingle();
        persisted[0].BodyMd.Should().Be("Good session");
        persisted[0].TargetDate.Should().Be(new DateOnly(2026, 6, 10));
        persisted[0].HabitNodeId.Should().Be(habitId);
    }

    [Fact]
    public async Task LoadDayAsync_with_existing_note_loads_its_text_and_update_persists()
    {
        var notes = new FakeNoteService();
        var habitId = Guid.NewGuid();
        await notes.CreateAsync(new Note { HabitNodeId = habitId, BodyMd = "orig", TargetDate = new DateOnly(2026, 6, 10) });
        var sut = NewSut(notes);
        sut.SetContext(habitId);

        await sut.LoadDayAsync(new DateOnly(2026, 6, 10));
        sut.Text.Should().Be("orig");

        sut.Text = "edited";
        await sut.SaveCommand.ExecuteAsync(null);

        var persisted = await notes.ListByHabitAndDateAsync(habitId, new DateOnly(2026, 6, 10));
        persisted.Should().ContainSingle();
        persisted[0].BodyMd.Should().Be("edited");
    }
}
