using FluentAssertions;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Backup;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Backup;

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

    private sealed class BlockingKeyStore : Recordum.Core.Abstractions.Security.IAutoBackupKeyStore
    {
        public readonly ManualResetEventSlim Gate = new(false);
        public int HasKeyThread, WriteThread;
        public bool HasKey() { HasKeyThread = Environment.CurrentManagedThreadId; Gate.Wait(TimeSpan.FromSeconds(5)); return true; }
        public void WriteKey(string passphrase) { WriteThread = Environment.CurrentManagedThreadId; Gate.Wait(TimeSpan.FromSeconds(5)); }
        public void DeleteKey() { }
        public string? ReadKey() => null;
    }

    [Fact]
    public async Task LoadAsync_never_waits_for_the_keyring_on_the_calling_thread()
    {
        var store = new BlockingKeyStore();
        var vm = new BackupSettingsViewModel(new FakeAppSettingService(), store, new FakeBackupSchedulerRunner());
        var me = Environment.CurrentManagedThreadId;

        var load = vm.LoadAsync();   // col codice rotto resta qui 5 s sul thread del chiamante

        Environment.CurrentManagedThreadId.Should().Be(me);
        load.IsCompleted.Should().BeFalse();
        store.Gate.Set();
        await load;
        store.HasKeyThread.Should().NotBe(me);
        vm.HasPassphrase.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_never_writes_to_the_keyring_on_the_calling_thread()
    {
        var store = new BlockingKeyStore();
        store.Gate.Set();
        var vm = new BackupSettingsViewModel(new FakeAppSettingService(), store, new FakeBackupSchedulerRunner())
        {
            NewPassphrase = "passphrase-1234567890",
            ConfirmPassphrase = "passphrase-1234567890",
        };

        await vm.SaveCommand.ExecuteAsync(null);

        store.WriteThread.Should().NotBe(0).And.NotBe(Environment.CurrentManagedThreadId);
    }
}
