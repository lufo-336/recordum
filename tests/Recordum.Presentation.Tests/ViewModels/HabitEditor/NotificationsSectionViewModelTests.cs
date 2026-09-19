using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitEditor;

public class NotificationsSectionViewModelTests
{
    private readonly FakeNotificationService _service = new();
    private readonly HabitNode _node = new() { Id = Guid.NewGuid(), NodeType = NodeType.Habit, HabitType = HabitType.Action };

    private NotificationsSectionViewModel CreateSut()
    {
        var sut = new NotificationsSectionViewModel(_service);
        sut.LoadAsync(_node).GetAwaiter().GetResult();
        return sut;
    }

    [Fact]
    public async Task AddNotificationCommand_appends_row_and_persists()
    {
        var sut = CreateSut();
        await sut.AddNotificationCommand.ExecuteAsync(null);
        sut.Notifications.Should().HaveCount(1);
        _service.Store.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveNotificationCommand_removes_row_and_persists()
    {
        var sut = CreateSut();
        await sut.AddNotificationCommand.ExecuteAsync(null);
        var row = sut.Notifications[0];

        await sut.RemoveNotificationCommand.ExecuteAsync(row);

        sut.Notifications.Should().BeEmpty();
        _service.Store.Should().BeEmpty();
    }

    [Fact]
    public async Task Duplicate_kind_and_time_sets_Error_status()
    {
        var sut = CreateSut();
        await sut.AddNotificationCommand.ExecuteAsync(null);
        sut.Notifications[0].NotifyTime = new TimeOnly(8, 0);
        await sut.AddNotificationCommand.ExecuteAsync(null);
        sut.Notifications[1].NotifyTime = new TimeOnly(8, 0);

        sut.Status.Should().Be(EditorStatus.Error);
        sut.ErrorMessage.Should().Be(Strings.Editor_Error_NotificationDuplicate);
    }

    [Fact]
    public async Task Changing_time_to_unique_value_clears_duplicate_error()
    {
        var sut = CreateSut();
        await sut.AddNotificationCommand.ExecuteAsync(null);
        sut.Notifications[0].NotifyTime = new TimeOnly(8, 0);
        await sut.AddNotificationCommand.ExecuteAsync(null);
        sut.Notifications[1].NotifyTime = new TimeOnly(8, 0);
        sut.Status.Should().Be(EditorStatus.Error);

        sut.Notifications[1].NotifyTime = new TimeOnly(9, 0);

        sut.Status.Should().NotBe(EditorStatus.Error);
    }
}
