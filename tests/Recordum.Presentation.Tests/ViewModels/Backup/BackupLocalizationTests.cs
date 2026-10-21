using System.Globalization;
using FluentAssertions;
using Recordum.Core.Backup;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Backup;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Backup;

/// <summary>F2c-6: testi di backup e restore che arrivavano all'utente in inglese grezzo. In italiano, perche' in
/// inglese alcune etichette coincidono con il valore grezzo ("Manual") e il test passerebbe anche col difetto.</summary>
[Collection(ProcessGlobalCultureCollection.Name)]
public sealed class BackupLocalizationTests : IDisposable
{
    private readonly CultureInfo? _previous = Strings.Culture;

    public BackupLocalizationTests() => Strings.Culture = CultureInfo.GetCultureInfo("it-IT");

    public void Dispose() => Strings.Culture = _previous;

    [Theory]
    [InlineData("Manual", "Manuale")]
    [InlineData("Auto", "Automatico")]
    [InlineData("PreRestore", "Prima del ripristino")]
    [InlineData("Imported", "Imported")]   // sconosciuto: resta com'e'
    public void History_rows_show_the_kind_in_the_user_language(string kind, string shown)
    {
        var row = new BackupHistoryRowViewModel(new BackupLog { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, Kind = kind, Path = "/x", SizeBytes = 1, SchemaVersion = "2.19", AppVersion = "1.0.0" });
        row.KindDisplay.Should().Be(shown);
    }

    [Fact]
    public async Task Restore_preview_names_an_inactive_plugin_in_the_user_language()
    {
        var vm = new RestoreDialogViewModel(new FakeRestoreService { InactivePlugins = ["io.example.charts"] }, new FakeOpenFileDialog { Result = "x.recbak" });
        vm.BrowseCommand.Execute(null);
        vm.Passphrase = "a-passphrase-long";

        await vm.PreviewCommand.ExecuteAsync(null);

        vm.Warnings.Should().ContainSingle().Which.Should().Be("Il backup usa il plugin «io.example.charts», che qui non è attivo.");
    }

    [Fact]
    public async Task An_unknown_restore_code_is_wrapped_in_a_sentence_not_shown_raw()
    {
        var vm = new RestoreDialogViewModel(new FakeRestoreService { RestoreThrows = new RestoreException("weird_code") }, new FakeOpenFileDialog { Result = "x.recbak" });
        vm.BrowseCommand.Execute(null);
        vm.Passphrase = "a-passphrase-long";
        await vm.PreviewCommand.ExecuteAsync(null);

        await vm.RunCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be("Il ripristino non è andato a buon fine. Dettagli: weird_code");
    }

    [Theory]
    [InlineData(true, "Non è stato possibile creare il backup. Dettagli: weird_code")]
    [InlineData(false, "Non è stato possibile creare il backup. Dettagli: disk full")]
    public async Task Backup_failures_are_a_sentence_in_the_user_language(bool backupException, string message)
    {
        var backup = new FakeBackupService { Throws = backupException ? new BackupException("weird_code") : new IOException("disk full") };
        var vm = new BackupDialogViewModel(backup, new FakeBackupSaveFileDialog { Result = "/tmp/x.recbak" }, new FakeClock());
        vm.BrowseCommand.Execute(null);
        vm.Passphrase = "passphrase-1234567890";
        vm.ConfirmPassphrase = "passphrase-1234567890";

        await vm.RunCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be(message);
    }

    [Fact]
    public void Both_dialogs_name_the_file_type_in_the_user_language()
    {
        var save = new FakeBackupSaveFileDialog();
        new BackupDialogViewModel(new FakeBackupService(), save, new FakeClock()).BrowseCommand.Execute(null);
        var open = new FakeOpenFileDialog();
        new RestoreDialogViewModel(new FakeRestoreService(), open).BrowseCommand.Execute(null);

        save.LastFilter.Should().Be("Backup di Recordum (*.recbak)|*.recbak");
        open.LastFilter.Should().Be("Backup di Recordum (*.recbak)|*.recbak");
    }
}
