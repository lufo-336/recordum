using System.Globalization;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Tests.Abstractions;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.Settings;

namespace Recordum.Presentation.Tests.ViewModels.Settings;

public sealed class SyncSettingsViewModelTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"rec-sync-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private sealed class FakeSyncService : ISyncService
    {
        private TaskCompletionSource? _entered;
        private TaskCompletionSource? _release;

        public int RunCalls { get; private set; }
        public string? LastPassphrase { get; private set; }
        public List<SyncJoinChoice> JoinChoices { get; } = [];
        public int DisconnectCalls { get; private set; }
        public byte[]? ExportedPlainBytes { get; private set; }
        public byte[]? ReencryptedBytes { get; private set; }
        public string? LastPlainPassphrase { get; private set; }
        public SyncJoinState JoinState { get; set; } = SyncJoinState.NotConfigured;
        public SyncStatus Status { get; set; } = new(null, [], 0, 0);
        public Task Entered => _entered?.Task ?? Task.CompletedTask;

        public void BlockNextRun()
        {
            _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void Release() => _release?.TrySetResult();

        public async Task<SyncRunResult> RunAsync(CancellationToken ct)
        {
            RunCalls++;
            _entered?.TrySetResult();
            if (_release is not null)
                await _release.Task.WaitAsync(ct);
            return new SyncRunResult(true, true, Status.Peers);
        }

        public Task<SyncStatus> GetStatusAsync(CancellationToken ct) => Task.FromResult(Status);

        public Task SetPassphraseAsync(string passphrase, CancellationToken ct)
        {
            LastPassphrase = passphrase;
            return Task.CompletedTask;
        }

        public Task<SyncJoinState> GetJoinStateAsync(CancellationToken ct) => Task.FromResult(JoinState);

        public Task JoinAsync(SyncJoinChoice choice, CancellationToken ct)
        {
            JoinChoices.Add(choice);
            JoinState = SyncJoinState.Joined;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct)
        {
            DisconnectCalls++;
            JoinState = SyncJoinState.NotConfigured;
            return Task.CompletedTask;
        }

        public async Task ExportPlainAsync(Stream rcsync, Stream zipOut, string passphrase, CancellationToken ct)
        {
            LastPlainPassphrase = passphrase;
            using var ms = new MemoryStream();
            await rcsync.CopyToAsync(ms, ct);
            ExportedPlainBytes = ms.ToArray();
            var output = Encoding.UTF8.GetBytes("plain-zip-payload");
            await zipOut.WriteAsync(output, ct);
        }

        public async Task ReencryptAsync(Stream zipIn, Stream rcsyncOut, string passphrase, CancellationToken ct)
        {
            LastPlainPassphrase = passphrase;
            using var ms = new MemoryStream();
            await zipIn.CopyToAsync(ms, ct);
            ReencryptedBytes = ms.ToArray();
            var output = Encoding.UTF8.GetBytes("encrypted-rcsync-payload");
            await rcsyncOut.WriteAsync(output, ct);
        }
    }

    private static SyncSettingsViewModel CreateVm(
        FakeAppSettingService? settings = null,
        FakeFolderPicker? folderPicker = null,
        FakeOpenFileDialog? openDialog = null,
        FakeBackupSaveFileDialog? saveDialog = null,
        FakeConfirmationDialog? confirm = null,
        FakeClock? clock = null,
        ISyncService? sync = null,
        SyncSchedulerHost? scheduler = null) =>
        new(
            settings ?? new FakeAppSettingService(),
            folderPicker ?? new FakeFolderPicker(),
            openDialog ?? new FakeOpenFileDialog(),
            saveDialog ?? new FakeBackupSaveFileDialog(),
            confirm ?? new FakeConfirmationDialog(),
            clock ?? new FakeClock(new DateTimeOffset(2026, 3, 15, 14, 0, 0, TimeSpan.Zero)),
            sync,
            scheduler);

    [Fact]
    public async Task AutoMergeOffPersistsAndStopsScheduler()
    {
        var sync = new FakeSyncService();
        var settings = new FakeAppSettingService();
        var ticks = new FakeTickSourceFactory();
        var clock = new FakeClock(new DateTimeOffset(2026, 3, 15, 14, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        services.AddScoped<ISyncService>(_ => sync);
        services.AddScoped<IAppSettingService>(_ => settings);
        await using var provider = services.BuildServiceProvider();
        using var host = new SyncSchedulerHost(
            provider.GetRequiredService<IServiceScopeFactory>(),
            ticks,
            clock,
            NullLogger<SyncSchedulerHost>.Instance);

        await host.StartAsync(CancellationToken.None);
        sync.RunCalls.Should().Be(1);

        var vm = CreateVm(settings: settings, clock: clock, sync: sync, scheduler: host);
        await vm.LoadAsync();
        vm.AutoMerge.Should().BeTrue();

        vm.AutoMerge = false;
        settings.Store[SyncSchedulerHost.EnabledSettingKey].Should().Be("false");

        ticks.TickAll();
        await host.CurrentRun;
        clock.Advance(TimeSpan.FromMinutes(1));
        host.Nudge();
        await host.CurrentRun;

        sync.RunCalls.Should().Be(1);
    }

    [Fact]
    public async Task ChooseFolderSavesSetting()
    {
        var settings = new FakeAppSettingService();
        var picker = new FakeFolderPicker { Result = "/cloud/Recordum" };
        var vm = CreateVm(settings: settings, folderPicker: picker);
        await vm.LoadAsync();

        await vm.ChooseFolderCommand.ExecuteAsync(null);

        vm.Folder.Should().Be("/cloud/Recordum");
        settings.Store[SyncSettingsViewModel.FolderKey].Should().Be("/cloud/Recordum");

        picker.Result = null;
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        vm.Folder.Should().Be("/cloud/Recordum");
    }

    [Fact]
    public async Task MergeNowRunsOnceAndRefreshesStatus()
    {
        var sync = new FakeSyncService
        {
            Status = new SyncStatus(null, [], 0, 0),
        };
        sync.BlockNextRun();
        var clock = new FakeClock(new DateTimeOffset(2026, 3, 15, 14, 0, 0, TimeSpan.Zero));
        var vm = CreateVm(clock: clock, sync: sync);
        await vm.LoadAsync();

        var first = vm.MergeNowCommand.ExecuteAsync(null);
        vm.MergeNowCommand.CanExecute(null).Should().BeFalse();
        var second = vm.MergeNowCommand.ExecuteAsync(null); // ExecuteAsync non consulta CanExecute: guardia interna
        await sync.Entered;

        sync.Status = new SyncStatus(
            new DateTimeOffset(2026, 3, 15, 13, 39, 0, TimeSpan.Zero),
            [new SyncPeerStatus("Fedora", new DateTimeOffset(2026, 3, 15, 13, 35, 0, TimeSpan.Zero), null)],
            0,
            2048);
        sync.Release();
        await Task.WhenAll(first, second);

        sync.RunCalls.Should().Be(1);
        vm.MergeNowCommand.CanExecute(null).Should().BeTrue();
        vm.Status.Should().Contain(line => line.Contains("Fedora", StringComparison.Ordinal));
    }

    [Fact]
    public async Task JoinStartFromOthersAsksConfirmationFirst()
    {
        var confirm = new FakeConfirmationDialog { Result = false };
        var sync = new FakeSyncService { JoinState = SyncJoinState.NeedsChoice };
        var vm = CreateVm(confirm: confirm, sync: sync);
        await vm.LoadAsync();
        vm.NeedsJoinChoice.Should().BeTrue();

        await vm.JoinStartFromOthersCommand.ExecuteAsync(null);

        confirm.Calls.Should().Be(1);
        confirm.LastTitle.Should().Be(Strings.Sync_Join_Confirm_Title);
        confirm.LastMessage.Should().Be(Strings.Sync_Join_Confirm_Message);
        sync.JoinChoices.Should().BeEmpty();
        vm.JoinState.Should().Be(SyncJoinState.NeedsChoice);

        confirm.Result = true;
        await vm.JoinStartFromOthersCommand.ExecuteAsync(null);

        sync.JoinChoices.Should().Equal(SyncJoinChoice.StartFromOthers);
        vm.JoinState.Should().Be(SyncJoinState.Joined);
        vm.NeedsJoinChoice.Should().BeFalse();
    }

    [Fact]
    public async Task StatusLinesUseLocalizedTexts()
    {
        var prevCulture = CultureInfo.CurrentCulture;
        var prevUiCulture = CultureInfo.CurrentUICulture;
        var it = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            CultureInfo.CurrentCulture = it;
            CultureInfo.CurrentUICulture = it;

            var clock = new FakeClock(new DateTimeOffset(2026, 3, 15, 14, 0, 0, TimeSpan.Zero));
            var sync = new FakeSyncService
            {
                Status = new SyncStatus(
                    new DateTimeOffset(2026, 3, 15, 13, 39, 0, TimeSpan.Zero),
                    [
                        new SyncPeerStatus("Fedora", new DateTimeOffset(2026, 3, 15, 13, 35, 0, TimeSpan.Zero), null),
                        new SyncPeerStatus("Portatile", null, SyncPeerProblem.CannotOpen),
                        new SyncPeerStatus("Tablet", null, SyncPeerProblem.NewerFormat),
                        new SyncPeerStatus("Vecchio", null, SyncPeerProblem.BrokenFile),
                        new SyncPeerStatus("Clone", null, SyncPeerProblem.SameIdentity),
                    ],
                    TextConflicts: 3,
                    OwnFileBytes: 2048),
            };

            var vm = CreateVm(clock: clock, sync: sync);
            await vm.LoadAsync();

            vm.Status.Should().Equal(
                "Ultima unione: oggi 13:39",
                "Fedora — file delle 13:35",
                "Il file di Portatile non si apre: la frase è cambiata o il file non è ancora arrivato del tutto.",
                "Aggiorna Recordum su Tablet.",
                "Il file di Vecchio è rovinato: è stato modificato a mano?",
                "Due dispositivi hanno la stessa identità: su uno dei due ripristina un backup o scollegalo.",
                "Voci da sistemare: 3",
                $"Il tuo file: {2.0:F1} KB");

            sync.Status = new SyncStatus(null, [], 0, 2 * 1024 * 1024);
            await vm.LoadAsync();

            vm.Status.Should().Equal(
                Strings.Sync_Status_Never,
                $"Il tuo file: {2.0:F1} MB");
        }
        finally
        {
            CultureInfo.CurrentCulture = prevCulture;
            CultureInfo.CurrentUICulture = prevUiCulture;
        }
    }

    [Fact]
    public async Task IncludePhotos_persists_setting()
    {
        var settings = new FakeAppSettingService();
        var vm = CreateVm(settings: settings);
        await vm.LoadAsync();
        vm.IncludePhotos.Should().BeFalse();

        vm.IncludePhotos = true;
        settings.Store[SyncSettingsViewModel.IncludePhotosKey].Should().Be("true");
    }

    [Fact]
    public async Task SetPassphrase_validates_and_saves()
    {
        var sync = new FakeSyncService();
        var vm = CreateVm(sync: sync);
        vm.NewPassphrase = "short";
        vm.ConfirmPassphrase = "short";
        await vm.SetPassphraseCommand.ExecuteAsync(null);
        vm.PassphraseMessage.Should().Be(Strings.Backup_Settings_PassphraseTooShort);
        sync.LastPassphrase.Should().BeNull();

        vm.NewPassphrase = "correct-horse-battery";
        vm.ConfirmPassphrase = "different-horse-battery";
        await vm.SetPassphraseCommand.ExecuteAsync(null);
        vm.PassphraseMessage.Should().Be(Strings.Backup_Settings_PassphraseMismatch);
        sync.LastPassphrase.Should().BeNull();

        vm.ConfirmPassphrase = "correct-horse-battery";
        await vm.SetPassphraseCommand.ExecuteAsync(null);
        sync.LastPassphrase.Should().Be("correct-horse-battery");
        vm.NewPassphrase.Should().BeEmpty();
        vm.ConfirmPassphrase.Should().BeEmpty();
        vm.PassphraseMessage.Should().Be(Strings.Backup_Settings_Saved);
    }

    [Fact]
    public async Task Disconnect_and_JoinKeepBoth_follow_confirmation_rules()
    {
        var confirm = new FakeConfirmationDialog { Result = false };
        var sync = new FakeSyncService { JoinState = SyncJoinState.NeedsChoice };
        var vm = CreateVm(confirm: confirm, sync: sync);
        await vm.LoadAsync();

        await vm.JoinKeepBothCommand.ExecuteAsync(null);
        confirm.Calls.Should().Be(0);
        sync.JoinChoices.Should().Equal(SyncJoinChoice.KeepThisDeviceToo);
        vm.JoinState.Should().Be(SyncJoinState.Joined);

        await vm.DisconnectCommand.ExecuteAsync(null);
        confirm.Calls.Should().Be(1);
        sync.DisconnectCalls.Should().Be(0);

        confirm.Result = true;
        await vm.DisconnectCommand.ExecuteAsync(null);
        sync.DisconnectCalls.Should().Be(1);
        vm.JoinState.Should().Be(SyncJoinState.NotConfigured);
    }

    [Fact]
    public async Task ExportPlain_and_Reencrypt_use_open_and_save_dialogs()
    {
        Directory.CreateDirectory(_tempDir);
        var rcsyncIn = Path.Combine(_tempDir, "in.rcsync");
        var zipOut = Path.Combine(_tempDir, "out.zip");
        var rcsyncOut = Path.Combine(_tempDir, "out.rcsync");
        await File.WriteAllBytesAsync(rcsyncIn, Encoding.UTF8.GetBytes("rcsync-input"));

        var open = new FakeOpenFileDialog { Result = rcsyncIn };
        var save = new FakeBackupSaveFileDialog { Result = zipOut };
        var sync = new FakeSyncService();
        var vm = CreateVm(openDialog: open, saveDialog: save, sync: sync);
        vm.PlainPassphrase = "plain-passphrase-123";

        await vm.ExportPlainCommand.ExecuteAsync(null);

        sync.LastPlainPassphrase.Should().Be("plain-passphrase-123");
        Encoding.UTF8.GetString(sync.ExportedPlainBytes!).Should().Be("rcsync-input");
        Encoding.UTF8.GetString(await File.ReadAllBytesAsync(zipOut)).Should().Be("plain-zip-payload");

        open.Result = zipOut;
        save.Result = rcsyncOut;
        await vm.ReencryptCommand.ExecuteAsync(null);

        Encoding.UTF8.GetString(sync.ReencryptedBytes!).Should().Be("plain-zip-payload");
        Encoding.UTF8.GetString(await File.ReadAllBytesAsync(rcsyncOut)).Should().Be("encrypted-rcsync-payload");
    }
}
