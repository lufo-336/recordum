using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.WPF.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.Notes;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Notes;

public class NoteHistoryViewModelTests
{
    private static (NoteHistoryViewModel vm, FakeNoteService svc, Guid noteId) CreateSut()
    {
        var svc = new FakeNoteService();
        var noteId = Guid.NewGuid();
        svc.Versions[noteId] =
        [
            new NoteVersion(Guid.NewGuid(), "v1", new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc)),
            new NoteVersion(Guid.NewGuid(), "v2", new DateTime(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc)),
        ];
        return (new NoteHistoryViewModel(svc), svc, noteId);
    }

    [Fact]
    public async Task LoadAsync_orders_newest_first_and_marks_current()
    {
        var (vm, _, noteId) = CreateSut();
        await vm.LoadAsync(noteId);

        vm.Versions.Select(v => v.BodyMd).Should().Equal("v2", "v1");
        vm.Versions[0].IsCurrent.Should().BeTrue();
        vm.Versions[1].IsCurrent.Should().BeFalse();
        vm.SelectedVersion.Should().Be(vm.Versions[0]);
        vm.PreviewMarkdown.Should().Be("v2");
    }

    [Fact]
    public async Task RestoreCommand_restores_selected_and_raises_event()
    {
        var (vm, svc, noteId) = CreateSut();
        await vm.LoadAsync(noteId);
        vm.SelectedVersion = vm.Versions[1]; // "v1"
        var raised = false;
        vm.Restored += (_, _) => raised = true;

        await vm.RestoreCommand.ExecuteAsync(null);

        svc.RestoreCalls.Should().ContainSingle()
            .Which.Should().Be((noteId, vm.Versions[1].Id));
        raised.Should().BeTrue();
    }

    [Fact]
    public async Task RestoreCommand_disabled_when_no_selection()
    {
        var (vm, _, noteId) = CreateSut();
        await vm.LoadAsync(noteId);
        vm.SelectedVersion = null;

        vm.RestoreCommand.CanExecute(null).Should().BeFalse();
    }
}
