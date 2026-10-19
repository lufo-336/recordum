using FluentAssertions;
using Recordum.Core.Models;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Backup;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Backup;

public class BackupHistoryViewModelTests
{
    [Fact]
    public async Task LoadAsync_populates_rows()
    {
        var svc = new FakeBackupLogService();
        svc.Rows.Add(new BackupLog { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, Kind = "Manual", Path = "/x", SizeBytes = 10, SchemaVersion = "2.7", AppVersion = "3.10.0" });
        var vm = new BackupHistoryViewModel(svc, new FakeFolderOpener());

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
        var vm = new BackupHistoryViewModel(svc, new FakeFolderOpener());
        await vm.LoadAsync();

        await vm.DeleteCommand.ExecuteAsync(vm.Rows[0]);

        svc.Deleted.Should().ContainSingle().Which.Should().Be(id);
        vm.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenFolderCommand_opens_the_folder_that_holds_the_backup()
    {
        var dir = Directory.CreateTempSubdirectory("recordum-history-").FullName;
        try
        {
            var svc = new FakeBackupLogService();
            svc.Rows.Add(new BackupLog { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, Kind = "Manual", Path = Path.Combine(dir, "a.recbak"), SizeBytes = 1, SchemaVersion = "2.7", AppVersion = "3.10.0" });
            var opener = new FakeFolderOpener();
            var vm = new BackupHistoryViewModel(svc, opener);
            await vm.LoadAsync();

            await vm.OpenFolderCommand.ExecuteAsync(vm.Rows[0]);

            opener.Opened.Should().ContainSingle().Which.Should().Be(dir);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task OpenFolderCommand_does_nothing_when_the_folder_is_gone()
    {
        var svc = new FakeBackupLogService();
        svc.Rows.Add(new BackupLog { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, Kind = "Manual", Path = Path.Combine(Path.GetTempPath(), "recordum-gone-" + Guid.NewGuid().ToString("N"), "a.recbak"), SizeBytes = 1, SchemaVersion = "2.7", AppVersion = "3.10.0" });
        var opener = new FakeFolderOpener();
        var vm = new BackupHistoryViewModel(svc, opener);
        await vm.LoadAsync();

        await vm.OpenFolderCommand.ExecuteAsync(vm.Rows[0]);

        opener.Opened.Should().BeEmpty();
    }
}
