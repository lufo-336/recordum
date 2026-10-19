using FluentAssertions;
using Recordum.Core.Abstractions.Security;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Backup;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Backup;

public class DataAndBackupPageViewModelTests
{
    private sealed class FakeSecrets(bool dbKeyPresent) : ISecretStore
    {
        public bool DbKeyPresent { get; private set; } = dbKeyPresent;
        public bool IsAvailable => true;
        public string? TryRead(SecretKind kind) => kind == SecretKind.DbKey && DbKeyPresent ? "k" : null;
        public void Write(SecretKind kind, string value) { }
        public void Delete(SecretKind kind) { if (kind == SecretKind.DbKey) DbKeyPresent = false; }
    }

    private static (DataAndBackupPageViewModel Page, FakeSecrets Secrets, FakeConfirmationDialog Confirm,
        FakeAutoBackupKeyStore Keys, FakeBackupDialogLauncher Launcher, FakeAppSettingService Settings) Build(
        bool dbKey = true, bool showDeviceKey = true, bool autoBackupKey = false, bool enabled = false)
    {
        var settings = new FakeAppSettingService();
        settings.Store["backup.auto.enabled"] = enabled ? "true" : "false";
        var keys = new FakeAutoBackupKeyStore { Has = autoBackupKey };
        var secrets = new FakeSecrets(dbKey);
        var confirm = new FakeConfirmationDialog();
        var launcher = new FakeBackupDialogLauncher();
        var page = new DataAndBackupPageViewModel(
            new BackupSettingsViewModel(settings, keys, new FakeBackupSchedulerRunner(), new FakeFolderPicker(), new FakeBackupStoragePaths()),
            new BackupHistoryViewModel(new FakeBackupLogService(), new FakeFolderOpener()), launcher, secrets, confirm, showDeviceKey);
        return (page, secrets, confirm, keys, launcher, settings);
    }

    [Fact]
    public async Task ShowDeviceKey_is_what_the_composition_says_in_both_directions()
    {
        (await LoadedAsync(showDeviceKey: true)).ShowDeviceKey.Should().BeTrue();
        (await LoadedAsync(showDeviceKey: false)).ShowDeviceKey.Should().BeFalse();
    }

    private static async Task<DataAndBackupPageViewModel> LoadedAsync(bool showDeviceKey)
    {
        var b = Build(showDeviceKey: showDeviceKey);
        await b.Page.LoadAsync();
        return b.Page;
    }

    [Fact]
    public async Task Load_reflects_the_remembered_key_and_the_button_commands_open_the_dialogs()
    {
        var b = Build(dbKey: true);
        await b.Page.LoadAsync();
        b.Page.IsDeviceKeyRemembered.Should().BeTrue();
        await b.Page.BackupNowCommand.ExecuteAsync(null);
        await b.Page.RestoreCommand.ExecuteAsync(null);
        (b.Launcher.BackupShown, b.Launcher.RestoreShown).Should().Be((1, 1));

        var none = Build(dbKey: false);
        await none.Page.LoadAsync();
        none.Page.IsDeviceKeyRemembered.Should().BeFalse();
    }

    [Fact]
    public async Task Forget_confirmed_deletes_the_db_key_and_the_state_turns_to_not_remembered()
    {
        var b = Build();
        await b.Page.LoadAsync();
        await b.Page.ForgetDeviceKeyCommand.ExecuteAsync(null);
        b.Confirm.Calls.Should().Be(1);
        b.Secrets.DbKeyPresent.Should().BeFalse();
        b.Page.IsDeviceKeyRemembered.Should().BeFalse();
    }

    [Fact]
    public async Task Forget_declined_leaves_the_key_alone()
    {
        var b = Build();
        b.Confirm.Result = false;
        await b.Page.LoadAsync();
        await b.Page.ForgetDeviceKeyCommand.ExecuteAsync(null);
        b.Secrets.DbKeyPresent.Should().BeTrue();
        b.Page.IsDeviceKeyRemembered.Should().BeTrue();
    }

    [Fact]
    public async Task AutoBackupOffWarning_shows_when_enabled_without_a_readable_passphrase_and_not_with_one()
    {
        var without = Build(enabled: true, autoBackupKey: false);
        await without.Page.LoadAsync();
        without.Page.AutoBackupOffWarning.Should().BeTrue();

        var with = Build(enabled: true, autoBackupKey: true);
        await with.Page.LoadAsync();
        with.Page.AutoBackupOffWarning.Should().BeFalse();
    }

    [Fact]
    public async Task Self_disabled_by_the_scheduler_shows_its_own_warning_and_re_enabling_with_a_passphrase_clears_it()
    {
        var b = Build(enabled: false, autoBackupKey: false);
        b.Settings.Store["backup.auto.disabled_reason"] = "no_passphrase";
        await b.Page.LoadAsync();
        b.Page.AutoBackupOffWarning.Should().BeTrue("acceso in passato, ora spento: l'utente non e' stato lui");
        b.Page.AutoBackupOffText.Should().Be(Recordum.Presentation.Resources.Strings.Backup_Settings_SelfDisabledWarning);

        b.Page.Settings.IsEnabled = true;
        b.Page.Settings.NewPassphrase = b.Page.Settings.ConfirmPassphrase = "passphrase-1234567890";
        await b.Page.Settings.SaveCommand.ExecuteAsync(null);

        b.Settings.Store["backup.auto.disabled_reason"].Should().BeEmpty();
        b.Page.AutoBackupOffWarning.Should().BeFalse();
    }

    [Fact]
    public async Task Without_the_signal_a_user_choice_to_switch_off_shows_no_warning()
    {
        var b = Build(enabled: false, autoBackupKey: false);
        await b.Page.LoadAsync();
        b.Page.AutoBackupOffWarning.Should().BeFalse();
    }
}
