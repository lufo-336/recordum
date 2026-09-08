using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class HabitScreenDayNoteWiringTests
{
    [Fact]
    public async Task Selecting_a_calendar_day_loads_that_days_note_into_the_day_editor()
    {
        var (vm, nodes) = HabitScreenViewModelFactory.Create(out var notes);
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" };
        await notes.CreateAsync(new Note { HabitNodeId = id, BodyMd = "day note", TargetDate = new DateOnly(2020, 1, 10) });

        await vm.LoadAsync(id);
        await vm.Calendar.LoadAsync(2020, 1, selected: null, DayBoundary.Utc);
        vm.Calendar.SelectDateCommand.Execute(vm.Calendar.Cells.Single(c => c.Date == new DateOnly(2020, 1, 10)));

        vm.DayNote.Date.Should().Be(new DateOnly(2020, 1, 10));
        vm.DayNote.Text.Should().Be("day note");
    }
}
