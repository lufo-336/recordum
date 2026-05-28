using FluentAssertions;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels;

public class MainWindowViewModelBackupTests
{
    [Fact]
    public async Task ShowBackupHistoryCommand_switches_workspace_and_loads_rows()
    {
        var fakeLauncher = new FakeBackupDialogLauncher();
        var fakeLogs = new FakeBackupLogService();
        fakeLogs.Rows.Add(new BackupLog { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, Kind = "Manual", Path = "/x", SizeBytes = 1, SchemaVersion = "2.7", AppVersion = "3.10.0" });

        var vm = MainWindowViewModelFactory.Build(launcher: fakeLauncher, log: fakeLogs);
        await vm.ShowBackupHistoryCommand.ExecuteAsync(null);

        vm.IsBackupHistoryWorkspace.Should().BeTrue();
        vm.BackupHistory.Rows.Should().HaveCount(1);
    }

    [Fact]
    public async Task BackupCreateCommand_invokes_launcher()
    {
        var fakeLauncher = new FakeBackupDialogLauncher();
        var vm = MainWindowViewModelFactory.Build(launcher: fakeLauncher);
        await vm.BackupCreateCommand.ExecuteAsync(null);
        fakeLauncher.BackupShown.Should().Be(1);
    }

    [Fact]
    public async Task BackupRestoreCommand_invokes_launcher()
    {
        var fakeLauncher = new FakeBackupDialogLauncher();
        var vm = MainWindowViewModelFactory.Build(launcher: fakeLauncher);
        await vm.BackupRestoreCommand.ExecuteAsync(null);
        fakeLauncher.RestoreShown.Should().Be(1);
    }

    [Fact]
    public async Task BackupSettingsCommand_invokes_launcher()
    {
        var fakeLauncher = new FakeBackupDialogLauncher();
        var vm = MainWindowViewModelFactory.Build(launcher: fakeLauncher);
        await vm.BackupSettingsCommand.ExecuteAsync(null);
        fakeLauncher.SettingsShown.Should().Be(1);
    }

    [Fact]
    public void ShowHome_resets_IsBackupHistoryWorkspace_to_false()
    {
        var vm = MainWindowViewModelFactory.Build();
        vm.ShowBackupHistoryCommand.Execute(null);
        vm.ShowHomeCommand.Execute(null);
        vm.IsBackupHistoryWorkspace.Should().BeFalse();
        vm.IsHomeWorkspace.Should().BeTrue();
    }
}
