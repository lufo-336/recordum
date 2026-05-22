using FluentAssertions;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen;

public class CompletionListViewModelTests
{
    [Fact]
    public async Task LoadAsync_takes_top_N_completions_in_descending_order()
    {
        var cs = new FakeCompletionService();
        var habitId = Guid.NewGuid();
        for (int i = 0; i < 30; i++)
        {
            var ts = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i);
            cs.Store[Guid.NewGuid()] = new Completion
            {
                Id = Guid.NewGuid(),
                HabitNodeId = habitId,
                CompletedAt = ts,
                CompletedAtMs = new DateTimeOffset(ts).ToUnixTimeMilliseconds(),
            };
        }
        var sut = new CompletionListViewModel(cs, new FakeMetricDefinitionService());

        await sut.LoadAsync(habitId);

        sut.Rows.Should().HaveCount(20);
        sut.Rows[0].CompletedAt.Date.Should().Be(new DateTime(2026, 5, 30));
    }

    [Fact]
    public async Task DeleteCommand_removes_row_and_calls_DeleteAsync()
    {
        var cs = new FakeCompletionService();
        var habitId = Guid.NewGuid();
        var c = new Completion { Id = Guid.NewGuid(), HabitNodeId = habitId, CompletedAt = DateTime.UtcNow, CompletedAtMs = 1 };
        cs.Store[c.Id] = c;
        var sut = new CompletionListViewModel(cs, new FakeMetricDefinitionService());
        await sut.LoadAsync(habitId);

        await sut.Rows[0].DeleteCommand.ExecuteAsync(null);

        cs.DeleteCalls.Should().Contain(c.Id);
        sut.Rows.Should().BeEmpty();
    }
}

public class CompletionRowViewModelTests
{
    [Fact]
    public async Task SaveEditCommand_persists_notes_and_date_via_UpdateAsync()
    {
        var cs = new FakeCompletionService();
        var habitId = Guid.NewGuid();
        var c = new Completion
        {
            Id = Guid.NewGuid(),
            HabitNodeId = habitId,
            CompletedAt = new DateTime(2026, 5, 20, 10, 0, 0, DateTimeKind.Utc),
            CompletedAtMs = 1,
            Notes = "orig",
        };
        cs.Store[c.Id] = c;
        var sut = new CompletionRowViewModel(c, cs);

        sut.BeginEditCommand.Execute(null);
        sut.EditedNotes = "new";
        sut.EditedDate = new DateTime(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc);
        await sut.SaveEditCommand.ExecuteAsync(null);

        cs.UpdateCalls.Should().ContainSingle().Which.Id.Should().Be(c.Id);
        cs.Store[c.Id].Notes.Should().Be("new");
        cs.Store[c.Id].CompletedAt.Date.Should().Be(new DateTime(2026, 5, 21));
        sut.IsEditing.Should().BeFalse();
    }

    [Fact]
    public void CancelEdit_exits_edit_mode_without_persisting()
    {
        var cs = new FakeCompletionService();
        var c = new Completion { Id = Guid.NewGuid(), HabitNodeId = Guid.NewGuid(), CompletedAt = DateTime.UtcNow, CompletedAtMs = 1, Notes = "orig" };
        cs.Store[c.Id] = c;
        var sut = new CompletionRowViewModel(c, cs);
        sut.BeginEditCommand.Execute(null);
        sut.EditedNotes = "mutated";

        sut.CancelEditCommand.Execute(null);

        sut.IsEditing.Should().BeFalse();
        cs.UpdateCalls.Should().BeEmpty();
        cs.Store[c.Id].Notes.Should().Be("orig");
    }
}
