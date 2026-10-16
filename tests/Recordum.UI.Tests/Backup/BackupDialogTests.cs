using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels.Backup;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.Backup;
using Xunit;

namespace Recordum.UI.Tests.Backup;

public class BackupDialogTests
{
    private sealed class FixedSave(string path) : IBackupSaveFileDialog
    {
        public Task<string?> PromptAsync(string defaultFileName, string filter) => Task.FromResult<string?>(path);
    }

    private sealed class FixedOpen(string path) : IOpenFileDialog
    {
        public Task<string?> PromptAsync(string filter) => Task.FromResult<string?>(path);
        public Task<string[]> PromptMultipleAsync(string filter) => Task.FromResult<string[]>([path]);
    }

    private const string Pass = "correct horse battery";

    private static string NewTempFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rec-bk-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "x.recbak");
    }

    [AvaloniaFact]
    public async Task Backup_window_creates_a_recbak_and_the_history_has_the_row()
    {
        var file = NewTempFile();
        var host = new PrivateDbHost("bk-dialog", s => s.AddSingleton<IBackupSaveFileDialog>(new FixedSave(file)));
        await using var scope = host.Provider.CreateAsyncScope();
        var vm = scope.ServiceProvider.GetRequiredService<BackupDialogViewModel>();
        var window = new BackupDialog { DataContext = vm };
        try
        {
            window.Show();
            PrivateDbHost.Pump();
            vm.BrowseCommand.Execute(null);
            await Task.Delay(50);
            window.FindControl<TextBox>("PassphraseBox")!.Text = Pass;
            window.FindControl<TextBox>("ConfirmBox")!.Text = Pass;
            PrivateDbHost.Pump();
            vm.Destination.Should().Be(file);
            await vm.RunCommand.ExecuteAsync(null);
            PrivateDbHost.Pump();
            File.Exists(file).Should().BeTrue();
            var rows = await scope.ServiceProvider.GetRequiredService<IBackupLogService>().ListAsync();
            rows.Should().ContainSingle(r => r.Path == file);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Double_create_runs_only_one_backup()
    {
        var file = NewTempFile();
        var host = new PrivateDbHost("bk-double", s => s.AddSingleton<IBackupSaveFileDialog>(new FixedSave(file)));
        await using var scope = host.Provider.CreateAsyncScope();
        var vm = scope.ServiceProvider.GetRequiredService<BackupDialogViewModel>();
        vm.Destination = file; vm.Passphrase = Pass; vm.ConfirmPassphrase = Pass;
        var first = vm.RunCommand.ExecuteAsync(null);
        var second = vm.RunCommand.ExecuteAsync(null);   // ExecuteAsync non consulta CanExecute: serve la guardia nel metodo
        await Task.WhenAll(first, second);
        var rows = await scope.ServiceProvider.GetRequiredService<IBackupLogService>().ListAsync();
        rows.Should().HaveCount(1);
    }

    [AvaloniaFact]
    public async Task Restore_window_previews_with_the_right_passphrase_and_explains_the_wrong_one()
    {
        var file = NewTempFile();
        var host = new PrivateDbHost("bk-restore", s => s.AddSingleton<IOpenFileDialog>(new FixedOpen(file)));
        await using var scope = host.Provider.CreateAsyncScope();
        var backup = scope.ServiceProvider.GetRequiredService<Recordum.Core.Backup.IBackupService>();
        await backup.CreateAsync(file, Pass, new Recordum.Core.Backup.BackupOptions(Recordum.Core.Backup.BackupKind.Manual));
        var vm = scope.ServiceProvider.GetRequiredService<RestoreDialogViewModel>();
        var window = new RestoreDialog { DataContext = vm };
        try
        {
            window.Show();
            PrivateDbHost.Pump();
            vm.BrowseCommand.Execute(null);
            await Task.Delay(50);
            window.FindControl<TextBox>("PassphraseBox")!.Text = "wrong passphrase!!";
            PrivateDbHost.Pump();
            await vm.PreviewCommand.ExecuteAsync(null);
            PrivateDbHost.Pump();
            vm.IsPreviewLoaded.Should().BeFalse();
            window.FindControl<TextBlock>("ErrorText")!.Text.Should().Be(Recordum.Presentation.Resources.Strings.Restore_Errors_InvalidPassphrase);

            window.FindControl<TextBox>("PassphraseBox")!.Text = Pass;
            PrivateDbHost.Pump();
            await vm.PreviewCommand.ExecuteAsync(null);
            PrivateDbHost.Pump();
            vm.IsPreviewLoaded.Should().BeTrue();
            window.FindControl<TextBlock>("SchemaText")!.IsVisible.Should().BeTrue();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Passphrase_fields_are_masked_and_have_an_accessible_name()
    {
        foreach (var (window, names) in new (Window, string[])[]
        {
            (new BackupDialog(), ["PassphraseBox", "ConfirmBox"]),
            (new RestoreDialog(), ["PassphraseBox"]),
        })
        {
            try
            {
                window.Show();
                PrivateDbHost.Pump();
                foreach (var n in names)
                {
                    var box = window.FindControl<TextBox>(n)!;
                    box.PasswordChar.Should().NotBe('\0');
                    Avalonia.Automation.AutomationProperties.GetName(box).Should().NotBeNullOrWhiteSpace();
                }
            }
            finally { window.Close(); }
        }
    }

    private sealed class BlockingRestore : Recordum.Core.Backup.IRestoreService
    {
        public TaskCompletionSource Gate { get; } = new();
        public Task<Recordum.Core.Backup.RestorePreview> PreviewAsync(string sourcePath, string passphrase, CancellationToken ct = default) => throw new NotSupportedException();
        public async Task<Recordum.Core.Backup.RestoreResult> RestoreAsync(string sourcePath, string passphrase, Recordum.Core.Backup.RestoreOptions options, CancellationToken ct = default)
        {
            await Gate.Task;
            return new Recordum.Core.Backup.RestoreResult([]);
        }
    }

    private sealed class CountingLifetime : Recordum.Core.Abstractions.IApplicationLifetime
    {
        public int Shutdowns { get; private set; }
        public void Shutdown() => Shutdowns++;
    }

    [AvaloniaFact]
    public async Task Restore_window_cannot_be_closed_while_running_and_the_launcher_shuts_down_after()
    {
        var restore = new BlockingRestore();
        var lifetime = new CountingLifetime();
        var sc = new ServiceCollection();
        sc.AddSingleton<Recordum.Core.Backup.IRestoreService>(restore);
        sc.AddSingleton<IOpenFileDialog>(new FixedOpen("x.recbak"));
        sc.AddSingleton<Recordum.Core.Abstractions.IApplicationLifetime>(lifetime);
        sc.AddTransient<RestoreDialogViewModel>();
        using var sp = sc.BuildServiceProvider();
        var launcher = new Recordum.UI.Services.AvaloniaBackupDialogLauncher(sp, () => null);
        Window? window = null;
        launcher.WindowCreatedForTests += w =>
        {
            window = w;
            var vm = (RestoreDialogViewModel)w.DataContext!;
            vm.SourcePath = "x.recbak"; vm.Passphrase = Pass; vm.IsPreviewLoaded = true;
        };

        var shown = launcher.ShowRestoreAsync();
        PrivateDbHost.Pump();
        var vm = (RestoreDialogViewModel)window!.DataContext!;
        var run = vm.RunCommand.ExecuteAsync(null);
        vm.IsRunning.Should().BeTrue();

        window.Close();   // X / Esc durante l'operazione
        PrivateDbHost.Pump();
        window.IsVisible.Should().BeTrue("la finestra non si chiude mentre il restore gira");
        shown.IsCompleted.Should().BeFalse();
        lifetime.Shutdowns.Should().Be(0);

        restore.Gate.SetResult();
        await run;
        await shown;
        lifetime.Shutdowns.Should().Be(1);
    }
}
