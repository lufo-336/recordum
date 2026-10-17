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

    private sealed class ThrowingKeyStore(Exception ex) : Recordum.Core.Abstractions.Security.IAutoBackupKeyStore
    {
        public bool HasKey() => throw ex;
        public void WriteKey(string passphrase) => throw ex;
        public void DeleteKey() { }
        public string? ReadKey() => throw ex;
    }

    private sealed class BlockingRunner : IBackupSchedulerRunner
    {
        public readonly ManualResetEventSlim Gate = new(false);
        public int Thread;
        public Exception? Throw;
        public Task RunNowAsync(CancellationToken ct = default)
        {
            Thread = Environment.CurrentManagedThreadId;
            Gate.Wait(TimeSpan.FromSeconds(5));   // come ReadKey() sul portachiavi: sincrono
            if (Throw is not null) throw Throw;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task TestAsync_never_runs_the_scheduler_on_the_calling_thread()
    {
        var runner = new BlockingRunner();
        var vm = new BackupSettingsViewModel(new FakeAppSettingService(), new FakeAutoBackupKeyStore(), runner);
        var me = Environment.CurrentManagedThreadId;

        var test = vm.TestCommand.ExecuteAsync(null);   // col codice rotto resta qui 5 s sul thread del chiamante

        Environment.CurrentManagedThreadId.Should().Be(me);
        test.IsCompleted.Should().BeFalse();
        runner.Gate.Set();
        await test;
        runner.Thread.Should().NotBe(me);
        vm.TestResultMessage.Should().Be(Recordum.Presentation.Resources.Strings.Backup_Settings_TestOk);
    }

    [Fact]
    public async Task TestAsync_shows_a_localized_message_for_an_unavailable_keyring_never_the_raw_one()
    {
        var runner = new BlockingRunner { Throw = new Recordum.Core.Abstractions.Security.SecretStoreUnavailableException("GLib: raw english detail") };
        runner.Gate.Set();
        var vm = new BackupSettingsViewModel(new FakeAppSettingService(), new FakeAutoBackupKeyStore(), runner);

        await vm.TestCommand.ExecuteAsync(null);

        vm.TestResultMessage.Should().Be(Recordum.Presentation.Resources.Strings.Backup_Settings_KeyringUnavailable);
    }

    [Fact]
    public async Task SaveAsync_shows_a_localized_message_when_the_keyring_refuses_the_write()
    {
        var store = new ThrowingKeyStore(new Recordum.Core.Abstractions.Security.SecretStoreUnavailableException("GLib: raw"));
        var vm = new BackupSettingsViewModel(new FakeAppSettingService(), store, new FakeBackupSchedulerRunner())
        { NewPassphrase = "passphrase-1234567890", ConfirmPassphrase = "passphrase-1234567890" };

        await vm.SaveCommand.ExecuteAsync(null);

        vm.TestResultMessage.Should().Be(Recordum.Presentation.Resources.Strings.Backup_Settings_KeyringUnavailable);
    }

    [Fact]
    public async Task LoadAsync_survives_an_unavailable_keyring()
    {
        var store = new ThrowingKeyStore(new Recordum.Core.Abstractions.Security.SecretStoreUnavailableException("x"));
        var vm = new BackupSettingsViewModel(new FakeAppSettingService(), store, new FakeBackupSchedulerRunner());

        await vm.LoadAsync();

        vm.HasPassphrase.Should().BeFalse();
    }
}
