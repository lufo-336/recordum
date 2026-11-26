using Avalonia.Headless.XUnit;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.UI.Bootstrap;
using Recordum.UI.Services;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.Backup;
using Xunit;

namespace Recordum.UI.Tests.Startup;

/// <summary>Gli avvii di App (portachiavi vero, DB vero, cartella dati privata) senza finestra: le stesse unita' che App
/// compone. Ogni test usa la sua cartella, quindi le sue voci nel portachiavi, e le cancella in <c>finally</c>.</summary>
[Trait("Category", "Keyring")]
public class KeyringStartupIntegrationTests
{
    private const string Pass = "correct horse battery";

    private sealed record Start(ServiceProvider Services, bool UnlockWindowShown);

    private sealed class CountingLifetime : IApplicationLifetime
    {
        public int Shutdowns { get; private set; }
        public void Shutdown() => Shutdowns++;
    }

    private sealed class YesDialog : IConfirmationDialog
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
    }

    private static RecordumOptions Options(string dir) => RecordumOptions.Resolve([RecordumOptions.DataDirectoryArgument, dir], _ => null);

    /// <summary>Un avvio: chiave ricordata, altrimenti "la finestra" (la passphrase data) con la scelta <paramref name="remember"/>.</summary>
    private static async Task<Start> StartAsync(string dir, RememberChoice remember, Action<IServiceCollection>? configure = null)
    {
        var options = Options(dir);
        var secrets = AppComposition.CreateSecretStore(options);
        var unlock = AppComposition.CreateUnlockService(options, secrets);
        unlock.ReconcileOrphans();
        var state = unlock.DetectState();

        IDbKeyProvider? key = null;
        var window = false;
        if (state == DbUnlockState.NeedsPassphrase)
            key = (await StoredKeyUnlock.RunAsync(unlock, state, StoredKeyUnlock.DefaultCap, new FakeAppHead().MissingKeyringReason)).Provider;
        if (key is null)
        {
            window = true;
            var result = await unlock.UnlockAsync(state, Pass, remember);
            result.Outcome.Should().Be(UnlockOutcome.Unlocked);
            key = result.Provider!;
        }

        var sp = AppComposition.BuildServices(options, key, secrets, new FakeAppHead(), configure);
        await AppComposition.InitializeAsync(sp);
        return new Start(sp, window);
    }

    private static async Task StopAsync(Start start)
    {
        await start.Services.DisposeAsync();
        SqliteConnection.ClearAllPools();
    }

    private static void Cleanup(string dir)
    {
        var secrets = AppComposition.CreateSecretStore(Options(dir));
        try { secrets.Delete(SecretKind.DbKey); secrets.Delete(SecretKind.AutoBackup); }
        catch (SecretStoreUnavailableException) { }
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
    }

    private static string NewDir() => Path.Combine(Path.GetTempPath(), $"rec-keyring-{Guid.NewGuid():N}");

    /// <summary>Su Linux il Secret Service c'e' (CI compresa): se manca e' un guasto, non un motivo per non provare.
    /// Altrove la categoria Keyring e' esclusa dalla CI, quindi si esce senza provare.</summary>
    private static bool NotLinux(string dir)
    {
        if (!OperatingSystem.IsLinux()) return true;
        Assert.True(AppComposition.CreateSecretStore(Options(dir)).IsAvailable, "Secret Service non disponibile: il portachiavi di prova non risponde");
        return false;
    }

    [AvaloniaFact]
    public async Task Remember_then_restart_without_window_then_forget_then_window_again()
    {
        var dir = NewDir();
        try
        {
            if (NotLinux(dir)) return;

            var first = await StartAsync(dir, RememberChoice.Remember);
            first.UnlockWindowShown.Should().BeTrue("la prima volta si crea con la passphrase");
            await StopAsync(first);

            var second = await StartAsync(dir, RememberChoice.LeaveAsIs, s => s.AddSingleton<IConfirmationDialog>(new YesDialog()));
            second.UnlockWindowShown.Should().BeFalse("la chiave ricordata apre il database");
            var page = ActivatorUtilities.CreateInstance<DataAndBackupPageViewModel>(second.Services, true);
            await page.LoadAsync();
            page.IsDeviceKeyRemembered.Should().BeTrue();
            await page.ForgetDeviceKeyCommand.ExecuteAsync(null);
            page.IsDeviceKeyRemembered.Should().BeFalse();
            await StopAsync(second);

            var third = await StartAsync(dir, RememberChoice.LeaveAsIs);
            third.UnlockWindowShown.Should().BeTrue("dimenticata la chiave si torna alla passphrase");
            await StopAsync(third);
        }
        finally { Cleanup(dir); }
    }

    [AvaloniaFact]
    public async Task Backup_now_then_restore_the_same_file_keeps_the_data_and_the_next_start_needs_no_passphrase()
    {
        var dir = NewDir();
        var file = Path.Combine(dir, "export", "it.recbak");
        try
        {
            if (NotLinux(dir)) return;

            var lifetime = new CountingLifetime();
            void Configure(IServiceCollection s)
            {
                s.AddSingleton<IBackupSaveFileDialog>(new FixedSave(file));
                s.AddSingleton<IOpenFileDialog>(new FixedOpen(file));
                s.AddSingleton<IApplicationLifetime>(lifetime);
                s.AddSingleton<IConfirmationDialog>(new YesDialog());
                s.AddSingleton<IBackupDialogLauncher>(sp => new AvaloniaBackupDialogLauncher(sp, () => null));
            }

            var first = await StartAsync(dir, RememberChoice.Remember, Configure);
            await StopAsync(first);
            var second = await StartAsync(dir, RememberChoice.LeaveAsIs, Configure);
            second.UnlockWindowShown.Should().BeFalse();
            var sp = second.Services;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            await sp.GetRequiredService<IAppSettingService>().SetAsync("test.marker", "before-backup");

            var launcher = (AvaloniaBackupDialogLauncher)sp.GetRequiredService<IBackupDialogLauncher>();
            Avalonia.Controls.Window? shown = null;
            launcher.WindowCreatedForTests += w => shown = w;
            var page = ActivatorUtilities.CreateInstance<DataAndBackupPageViewModel>(sp, true);
            await page.LoadAsync();

            // Impostazioni -> "Backup ora"
            var backup = page.BackupNowCommand.ExecuteAsync(null);
            PrivateDbHost.Pump();
            var backupVm = (BackupDialogViewModel)shown!.DataContext!;
            backupVm.Destination = file; backupVm.Passphrase = Pass; backupVm.ConfirmPassphrase = Pass;
            await backupVm.RunCommand.ExecuteAsync(null);
            shown.Close();
            await backup;
            File.Exists(file).Should().BeTrue();
            page.History.Rows.Should().ContainSingle();

            // dato cambiato dopo il backup: il restore deve riportare quello salvato
            await sp.GetRequiredService<IAppSettingService>().SetAsync("test.marker", "after-backup");

            // Impostazioni -> "Ripristina..."
            var restore = page.RestoreCommand.ExecuteAsync(null);
            PrivateDbHost.Pump();
            var restoreVm = (RestoreDialogViewModel)shown!.DataContext!;
            restoreVm.SourcePath = file; restoreVm.Passphrase = Pass;
            await restoreVm.PreviewCommand.ExecuteAsync(null);
            await restoreVm.RunCommand.ExecuteAsync(null);
            shown.Close();
            await restore;
            lifetime.Shutdowns.Should().Be(1, "un restore riuscito chiude l'app");
            await StopAsync(second);

            var fourth = await StartAsync(dir, RememberChoice.LeaveAsIs, Configure);
            try
            {
                fourth.UnlockWindowShown.Should().BeFalse("la chiave ricordata apre ancora il database ripristinato");
                (await fourth.Services.GetRequiredService<IAppSettingService>().GetAsync("test.marker")).Should().Be("before-backup");
            }
            finally { await StopAsync(fourth); }
        }
        finally { Cleanup(dir); }
    }

    private sealed class FixedSave(string path) : IBackupSaveFileDialog
    {
        public Task<string?> PromptAsync(string defaultFileName, string filter) => Task.FromResult<string?>(path);
    }

    private sealed class FixedOpen(string path) : IOpenFileDialog
    {
        public Task<string?> PromptAsync(string filter) => Task.FromResult<string?>(path);
        public Task<string[]> PromptMultipleAsync(string filter) => Task.FromResult<string[]>([path]);
    }
}
