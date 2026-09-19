using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen;

public class HabitScreenCalendarWiringTests
{
    [Fact]
    public async Task Selecting_a_calendar_day_sets_quickadd_completion_date()
    {
        var (vm, nodes) = HabitScreenViewModelFactory.Create();
        var id = Guid.NewGuid();
        nodes.Store[id] = new HabitNode { Id = id, NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "h" };

        await vm.LoadAsync(id);
        // Load a month fully in the past so all in-month days are selectable.
        await vm.Calendar.LoadAsync(2020, 1, selected: null, DayBoundary.Utc);

        var cell = vm.Calendar.Cells.First(c => c.InCurrentMonth && !c.IsFuture);
        vm.Calendar.SelectDateCommand.Execute(cell);

        vm.QuickAdd!.CompletionDate.Date.Should().Be(cell.Date.ToDateTime(TimeOnly.MinValue).Date);
    }
}
