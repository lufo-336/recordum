using FluentAssertions;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Backup;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Backup;

public class BackupHistoryViewModelTests
{
    [Fact]
    public async Task LoadAsync_populates_rows()
    {
        var svc = new FakeBackupLogService();
        svc.Rows.Add(new BackupLog { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, Kind = "Manual", Path = "/x", SizeBytes = 10, SchemaVersion = "2.7", AppVersion = "3.10.0" });
        var vm = new BackupHistoryViewModel(svc);

        await vm.LoadAsync();

        vm.Rows.Should().HaveCount(1);
        vm.Rows[0].Kind.Should().Be("Manual");
    }

    [Fact]
    public async Task DeleteCommand_removes_row_via_service()
    {
        var svc = new FakeBackupLogService();
        var id = Guid.NewGuid();
        svc.Rows.Add(new BackupLog { Id = id, CreatedAt = DateTime.UtcNow, Kind = "Auto", Path = "/y", SizeBytes = 0, SchemaVersion = "2.7", AppVersion = "3.10.0" });
        var vm = new BackupHistoryViewModel(svc);
        await vm.LoadAsync();

        await vm.DeleteCommand.ExecuteAsync(vm.Rows[0]);

        svc.Deleted.Should().ContainSingle().Which.Should().Be(id);
        vm.Rows.Should().BeEmpty();
    }
}
