using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Core.Time;
using Recordum.Plugins.Contracts.Events;
using Recordum.Presentation.Tests.Fixtures;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

/// <summary>La nota del giorno è la stessa nota della colonna Note (spec §6.1): scriverla in un posto la mostra
/// nell'altro, e nessun clic perde il testo scritto.</summary>
public class DayNoteSyncTests
{
    private static readonly DateOnly Today = new(2026, 10, 28);

    private static async Task<(HabitScreenRig Rig, Guid Id)> LoadAsync()
    {
        var rig = HabitScreenViewModelFactory.CreateRig(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero));
        var id = Guid.NewGuid();
        rig.Nodes.Store[id] = new HabitNode
        {
            Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h",
            CreatedAt = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        };
        await rig.Vm.LoadAsync(id);
        return (rig, id);
    }

    [Fact]
    public async Task A_note_saved_in_the_notes_column_shows_in_the_day()
    {
        var (rig, id) = await LoadAsync();
        rig.Vm.DayNote.Text = "prima";
        await rig.Vm.DayNote.SaveCommand.ExecuteAsync(null);
        // Il servizio vero restituisce copie staccate; il finto condivide l'istanza fra i due ViewModel.
        var stored = rig.Notes.Store.Values.Single();
        rig.Notes.Store[stored.Id] = new Note
        {
            Id = stored.Id, HabitNodeId = stored.HabitNodeId, BodyMd = stored.BodyMd, TargetDate = stored.TargetDate,
            UpdatedAtMs = stored.UpdatedAtMs,
        };
        await rig.Vm.Notes.LoadAsync(id, DayBoundary.Utc);

        var notes = rig.Vm.Notes;
        notes.SelectedNote = notes.Notes.Single();
        notes.EditorMarkdown = "dopo";
        await notes.SaveCommand.ExecuteAsync(null);
        rig.Bus.Publish(new NoteUpdatedPayload(notes.SelectedNote.Id, id));
        await Task.Delay(30);

        rig.Vm.DayNote.Text.Should().Be("dopo");
    }

    [Fact]
    public async Task A_dirty_day_note_is_not_overwritten_by_an_event()
    {
        var (rig, id) = await LoadAsync();
        rig.Vm.DayNote.Text = "sto scrivendo";
        rig.Bus.Publish(new NoteUpdatedPayload(Guid.NewGuid(), id));
        await Task.Delay(30);

        rig.Vm.DayNote.Text.Should().Be("sto scrivendo");
    }

    [Fact]
    public async Task Changing_day_saves_the_day_note()
    {
        var (rig, id) = await LoadAsync();
        rig.Vm.DayNote.Text = "non perdermi";

        await rig.Vm.SelectDayAsync(Today.AddDays(-1));

        (await rig.Notes.ListByHabitAndDateAsync(id, Today)).Should().ContainSingle().Which.BodyMd.Should().Be("non perdermi");
        await rig.Vm.SelectDayAsync(Today);
        rig.Vm.DayNote.Text.Should().Be("non perdermi");
    }
}
