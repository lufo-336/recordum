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
        var vm = new BackupSettingsViewModel(settings, keyStore, new FakeBackupSchedulerRunner(), new FakeFolderPicker(), new FakeBackupStoragePaths());
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
        var vm = new BackupSettingsViewModel(settings, keyStore, new FakeBackupSchedulerRunner(), new FakeFolderPicker(), new FakeBackupStoragePaths());
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
        var vm = new BackupSettingsViewModel(settings, keyStore, new FakeBackupSchedulerRunner(), new FakeFolderPicker(), new FakeBackupStoragePaths());
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
        var vm = new BackupSettingsViewModel(new FakeAppSettingService(), store, new FakeBackupSchedulerRunner(), new FakeFolderPicker(), new FakeBackupStoragePaths());
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
        var vm = new BackupSettingsViewModel(new FakeAppSettingService(), store, new FakeBackupSchedulerRunner(), new FakeFolderPicker(), new FakeBackupStoragePaths())
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
        public Task<Recordum.Core.Backup.BackupRunOutcome> RunNowAsync(CancellationToken ct = default)
        {
            Thread = Environment.CurrentManagedThreadId;
            Gate.Wait(TimeSpan.FromSeconds(5));   // come ReadKey() sul portachiavi: sincrono
            if (Throw is not null) throw Throw;
            return Task.FromResult(Recordum.Core.Backup.BackupRunOutcome.Created);
        }
    }

    [Fact]
    public async Task TestAsync_never_runs_the_scheduler_on_the_calling_thread()
    {
        var runner = new BlockingRunner();
        var vm = new BackupSettingsViewModel(new FakeAppSettingService(), new FakeAutoBackupKeyStore(), runner, new FakeFolderPicker(), new FakeBackupStoragePaths());
        var me = Environment.CurrentManagedThreadId;

        var test = vm.TestCommand.ExecuteAsync(null);   // col codice rotto resta qui 5 s sul thread del chiamante

        Environment.CurrentManagedThreadId.Should().Be(me);
        test.IsCompleted.Should().BeFalse();
        runner.Gate.Set();
        await test;
        runner.Thread.Should().NotBe(me);
        vm.TestResultMessage.Should().Be(Recordum.Presentation.Resources.Strings.Backup_Settings_TestCreated);
    }

    [Fact]
    public async Task TestAsync_shows_a_localized_message_for_an_unavailable_keyring_never_the_raw_one()
    {
        var runner = new BlockingRunner { Throw = new Recordum.Core.Abstractions.Security.SecretStoreUnavailableException("GLib: raw english detail") };
        runner.Gate.Set();
        var vm = new BackupSettingsViewModel(new FakeAppSettingService(), new FakeAutoBackupKeyStore(), runner, new FakeFolderPicker(), new FakeBackupStoragePaths());

        await vm.TestCommand.ExecuteAsync(null);

        vm.TestResultMessage.Should().Be(Recordum.Presentation.Resources.Strings.Backup_Settings_KeyringUnavailable);
    }

    [Fact]
    public async Task SaveAsync_shows_a_localized_message_when_the_keyring_refuses_the_write()
    {
        var store = new ThrowingKeyStore(new Recordum.Core.Abstractions.Security.SecretStoreUnavailableException("GLib: raw"));
        var vm = new BackupSettingsViewModel(new FakeAppSettingService(), store, new FakeBackupSchedulerRunner(), new FakeFolderPicker(), new FakeBackupStoragePaths())
        { NewPassphrase = "passphrase-1234567890", ConfirmPassphrase = "passphrase-1234567890" };

        await vm.SaveCommand.ExecuteAsync(null);

        vm.TestResultMessage.Should().Be(Recordum.Presentation.Resources.Strings.Backup_Settings_KeyringUnavailable);
    }

    [Fact]
    public async Task LoadAsync_survives_an_unavailable_keyring()
    {
        var store = new ThrowingKeyStore(new Recordum.Core.Abstractions.Security.SecretStoreUnavailableException("x"));
        var vm = new BackupSettingsViewModel(new FakeAppSettingService(), store, new FakeBackupSchedulerRunner(), new FakeFolderPicker(), new FakeBackupStoragePaths());

        await vm.LoadAsync();

        vm.HasPassphrase.Should().BeFalse();
    }

    private static BackupSettingsViewModel NewVm(FakeAppSettingService settings, FakeAutoBackupKeyStore keys, FakeFolderPicker? picker = null) =>
        new(settings, keys, new FakeBackupSchedulerRunner(), picker ?? new FakeFolderPicker(), new FakeBackupStoragePaths());

    [Fact]
    public async Task BrowseFolder_writes_the_picked_path_and_a_cancel_changes_nothing()
    {
        var picker = new FakeFolderPicker { Result = "/home/x/Backups" };
        var vm = NewVm(new FakeAppSettingService(), new FakeAutoBackupKeyStore(), picker);

        await vm.BrowseFolderCommand.ExecuteAsync(null);
        vm.Folder.Should().Be("/home/x/Backups");
        picker.AskedInitial.Should().Be(vm.DefaultFolder, "folder vuota: il selettore parte dalla cartella predefinita");

        picker.Result = null;
        await vm.BrowseFolderCommand.ExecuteAsync(null);
        vm.Folder.Should().Be("/home/x/Backups");
        picker.AskedInitial.Should().Be("/home/x/Backups");
    }

    [Fact]
    public void DefaultFolder_is_the_scheduler_default()
    {
        NewVm(new FakeAppSettingService(), new FakeAutoBackupKeyStore()).DefaultFolder.Should().Be(new FakeBackupStoragePaths().BackupsDirectory);
    }

    [Theory]
    [InlineData("short-pass", "short-pass", "PassphraseTooShort")]
    [InlineData("passphrase-1234567890", "passphrase-OTHER-1234", "PassphraseMismatch")]
    public async Task SaveAsync_with_an_invalid_passphrase_says_why_and_writes_nothing(string pass, string confirm, string expected)
    {
        var settings = new FakeAppSettingService();
        settings.Store["backup.auto.enabled"] = "false";
        var keys = new FakeAutoBackupKeyStore();
        var vm = NewVm(settings, keys);
        await vm.LoadAsync();
        vm.IsEnabled = true;
        vm.NewPassphrase = pass;
        vm.ConfirmPassphrase = confirm;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.TestResultMessage.Should().Be(expected == "PassphraseTooShort"
            ? Recordum.Presentation.Resources.Strings.Backup_Settings_PassphraseTooShort
            : Recordum.Presentation.Resources.Strings.Backup_Settings_PassphraseMismatch);
        keys.Written.Should().BeNull();
        settings.Store["backup.auto.enabled"].Should().Be("false");
    }

    [Fact]
    public async Task SaveAsync_success_says_saved_and_writes_the_key()
    {
        var keys = new FakeAutoBackupKeyStore();
        var vm = NewVm(new FakeAppSettingService(), keys);
        vm.NewPassphrase = vm.ConfirmPassphrase = "passphrase-1234567890";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.TestResultMessage.Should().Be(Recordum.Presentation.Resources.Strings.Backup_Settings_Saved);
        keys.Written.Should().Be("passphrase-1234567890");
    }

    private sealed class UnreachableKeyStore : Recordum.Core.Abstractions.Security.IAutoBackupKeyStore
    {
        public bool HasKey() => throw new Recordum.Core.Abstractions.Security.SecretStoreUnavailableException("locked");
        public void WriteKey(string passphrase) { }
        public void DeleteKey() { }
        public string? ReadKey() => throw new Recordum.Core.Abstractions.Security.SecretStoreUnavailableException("locked");
    }

    [Fact]
    public async Task With_the_keyring_unreachable_the_page_says_so_and_does_not_claim_the_passphrase_is_missing()
    {
        // F2c-8: prima diceva "nessuna passphrase, non partira'" anche quando il portachiavi semplicemente non rispondeva.
        var settings = new FakeAppSettingService();
        settings.Store["backup.auto.enabled"] = "true";
        var vm = new BackupSettingsViewModel(settings, new UnreachableKeyStore(), new FakeBackupSchedulerRunner(), new FakeFolderPicker(), new FakeBackupStoragePaths());

        await vm.LoadAsync();

        vm.IsEnabled.Should().BeTrue();
        vm.KeyringUnavailable.Should().BeTrue();
        vm.InertWarning.Should().BeFalse();
        vm.TestResultMessage.Should().Be(Recordum.Presentation.Resources.Strings.Backup_Settings_KeyringUnavailable);
    }

    [Theory]
    [InlineData(Recordum.Core.Backup.BackupRunOutcome.Disabled)]
    [InlineData(Recordum.Core.Backup.BackupRunOutcome.NotDue)]
    [InlineData(Recordum.Core.Backup.BackupRunOutcome.Created)]
    [InlineData(Recordum.Core.Backup.BackupRunOutcome.KeyringUnavailable)]
    [InlineData(Recordum.Core.Backup.BackupRunOutcome.SwitchedOff)]
    public async Task Test_says_what_the_scheduler_actually_did(Recordum.Core.Backup.BackupRunOutcome outcome)
    {
        // F2c-8: prima ogni giro, anche saltato per il portachiavi, diceva "controllo finito".
        var settings = new FakeAppSettingService();
        settings.Store["backup.auto.enabled"] = "true";
        var vm = new BackupSettingsViewModel(settings, new FakeAutoBackupKeyStore { Has = true }, new FakeBackupSchedulerRunner { Outcome = outcome }, new FakeFolderPicker(), new FakeBackupStoragePaths());
        await vm.LoadAsync();

        await vm.TestCommand.ExecuteAsync(null);

        vm.TestResultMessage.Should().Be(outcome switch
        {
            Recordum.Core.Backup.BackupRunOutcome.Disabled => Recordum.Presentation.Resources.Strings.Backup_Settings_TestDisabled,
            Recordum.Core.Backup.BackupRunOutcome.NotDue => Recordum.Presentation.Resources.Strings.Backup_Settings_TestNotDue,
            Recordum.Core.Backup.BackupRunOutcome.Created => Recordum.Presentation.Resources.Strings.Backup_Settings_TestCreated,
            Recordum.Core.Backup.BackupRunOutcome.KeyringUnavailable => Recordum.Presentation.Resources.Strings.Backup_Settings_KeyringUnavailable,
            _ => Recordum.Presentation.Resources.Strings.Backup_Settings_SelfDisabledWarning,
        });
        vm.IsEnabled.Should().Be(outcome != Recordum.Core.Backup.BackupRunOutcome.SwitchedOff, "spento dallo scheduler: la casella lo segue");
        vm.SelfDisabled.Should().Be(outcome == Recordum.Core.Backup.BackupRunOutcome.SwitchedOff);
    }
}
