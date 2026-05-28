using FluentAssertions;
using Recordum.Core.Backup;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Backup;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Backup;

public class BackupDialogViewModelTests
{
    [Fact]
    public async Task RunCommand_calls_backup_service_with_chosen_path_and_passphrase()
    {
        var backup = new FakeBackupService();
        var dlg = new FakeBackupSaveFileDialog { Result = "C:/tmp/x.recbak" };
        var vm = new BackupDialogViewModel(backup, dlg);

        vm.BrowseCommand.Execute(null);
        vm.Passphrase = "passphrase-1234567890";
        vm.ConfirmPassphrase = "passphrase-1234567890";

        await vm.RunCommand.ExecuteAsync(null);

        backup.Calls.Should().ContainSingle()
            .Which.Should().Be(("C:/tmp/x.recbak", "passphrase-1234567890", BackupKind.Manual));
        vm.SuccessPath.Should().Be("C:/tmp/x.recbak");
    }

    [Fact]
    public void RunCommand_disabled_when_passphrases_do_not_match()
    {
        var vm = new BackupDialogViewModel(new FakeBackupService(), new FakeBackupSaveFileDialog());
        vm.Destination = "x.recbak";
        vm.Passphrase = "a-passphrase-long";
        vm.ConfirmPassphrase = "different-passphrase";

        vm.RunCommand.CanExecute(null).Should().BeFalse();
    }

    [Theory]
    [InlineData("short", PassphraseStrength.Weak)]
    [InlineData("medium-pp", PassphraseStrength.Ok)]
    [InlineData("a-long-and-strong-passphrase", PassphraseStrength.Strong)]
    public void Strength_classifies_by_length(string pass, PassphraseStrength expected)
    {
        var vm = new BackupDialogViewModel(new FakeBackupService(), new FakeBackupSaveFileDialog());
        vm.Passphrase = pass;
        vm.Strength.Should().Be(expected);
    }
}
