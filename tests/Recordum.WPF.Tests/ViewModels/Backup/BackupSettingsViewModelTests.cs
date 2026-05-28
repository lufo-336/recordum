using FluentAssertions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Backup;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Backup;

public class BackupSettingsViewModelTests
{
    [Fact]
    public async Task LoadAsync_reads_settings()
    {
        var settings = new FakeAppSettingService();
        settings.Store["backup.auto.enabled"] = "true";
        settings.Store["backup.auto.frequency_days"] = "7";
        settings.Store["backup.auto.retention_count"] = "8";
        settings.Store["backup.auto.path"] = "";
        var keyStore = new FakeAutoBackupKeyStore { Has = true };
        var vm = new BackupSettingsViewModel(settings, keyStore, new FakeBackupSchedulerRunner());
        await vm.LoadAsync();

        vm.IsEnabled.Should().BeTrue();
        vm.FrequencyDays.Should().Be(7);
        vm.RetentionCount.Should().Be(8);
        vm.HasPassphrase.Should().BeTrue();
        vm.InertWarning.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAsync_writes_passphrase_when_provided_and_persists_settings()
    {
        var settings = new FakeAppSettingService();
        settings.Store["backup.auto.enabled"] = "false";
        settings.Store["backup.auto.frequency_days"] = "7";
        settings.Store["backup.auto.retention_count"] = "8";
        settings.Store["backup.auto.path"] = "";
        var keyStore = new FakeAutoBackupKeyStore();
        var vm = new BackupSettingsViewModel(settings, keyStore, new FakeBackupSchedulerRunner());
        await vm.LoadAsync();
        vm.IsEnabled = true;
        vm.FrequencyDays = 14;
        vm.RetentionCount = 4;
        vm.NewPassphrase = "passphrase-1234567890";
        vm.ConfirmPassphrase = "passphrase-1234567890";

        await vm.SaveCommand.ExecuteAsync(null);

        keyStore.Written.Should().Be("passphrase-1234567890");
        settings.Store["backup.auto.enabled"].Should().Be("true");
        settings.Store["backup.auto.frequency_days"].Should().Be("14");
        settings.Store["backup.auto.retention_count"].Should().Be("4");
    }

    [Fact]
    public async Task InertWarning_true_when_enabled_but_no_key()
    {
        var settings = new FakeAppSettingService();
        settings.Store["backup.auto.enabled"] = "true";
        settings.Store["backup.auto.frequency_days"] = "7";
        settings.Store["backup.auto.retention_count"] = "8";
        settings.Store["backup.auto.path"] = "";
        var keyStore = new FakeAutoBackupKeyStore { Has = false };
        var vm = new BackupSettingsViewModel(settings, keyStore, new FakeBackupSchedulerRunner());
        await vm.LoadAsync();

        vm.InertWarning.Should().BeTrue();
    }
}
