using System.Globalization;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Tests.Abstractions;
using Recordum.Presentation.Bootstrap;
using Recordum.Presentation.Resources;
using Recordum.Presentation.Services;
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
        public List<SyncItemToFix> Items { get; set; } = [];
        public List<Guid> AcceptedRepairs { get; } = [];
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

        public Task<IReadOnlyList<SyncItemToFix>> ListItemsToFixAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SyncItemToFix>>(Items.ToList());

        public Task AcceptRepairAsync(Guid periodId, CancellationToken ct)
        {
            AcceptedRepairs.Add(periodId);
            Items.RemoveAll(i => i.EntityId == periodId && i.Kind == SyncItemKind.RepairedPeriod);
            return Task.CompletedTask;
        }

        public Task SetPassphraseAsync(string passphrase, CancellationToken ct)
        {
            LastPassphrase = passphrase;
            return Task.CompletedTask;
        }

        public Task<SyncJoinState> GetJoinStateAsync(CancellationToken ct) => Task.FromResult(JoinState);

        public bool RefuseJoin { get; set; }

        public Task JoinAsync(SyncJoinChoice choice, CancellationToken ct)
        {
            if (RefuseJoin) throw new SyncJoinRefusedException(SyncJoinRefusal.NoReadablePeer, "refused");
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

    private sealed class EmptyScopeFactory : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        public IServiceProvider ServiceProvider => this;
        public IServiceScope CreateScope() => this;
        public object? GetService(Type serviceType) => null;
        public void Dispose() { }
    }

    private sealed class FakeHabitNavigator : IHabitNavigator
    {
        public List<(Guid HabitNodeId, string Title, DateOnly Day)> Calls { get; } = [];
        public Action? OnOpen { get; set; }

        public Task OpenHabitAtDayAsync(Guid habitNodeId, string title, DateOnly day)
        {
            OnOpen?.Invoke();
            Calls.Add((habitNodeId, title, day));
            return Task.CompletedTask;
        }
    }

    private SyncSettingsViewModel CreateVm(
        FakeAppSettingService? settings = null,
        FakeFolderPicker? folderPicker = null,
        FakeOpenFileDialog? openDialog = null,
        FakeBackupSaveFileDialog? saveDialog = null,
        FakeConfirmationDialog? confirm = null,
        FakeClock? clock = null,
        ISyncService? sync = null,
        SyncSchedulerHost? scheduler = null,
        IHabitNavigator? navigator = null)
    {
        var effectiveClock = clock ?? new FakeClock(new DateTimeOffset(2026, 3, 15, 14, 0, 0, TimeSpan.Zero));
        var effectiveScheduler = scheduler ?? new SyncSchedulerHost(
            new EmptyScopeFactory(),
            new FakeTickSourceFactory(),
            effectiveClock,
            NullLogger<SyncSchedulerHost>.Instance);
        return new(
            settings ?? new FakeAppSettingService(),
            folderPicker ?? new FakeFolderPicker(),
            openDialog ?? new FakeOpenFileDialog(),
            saveDialog ?? new FakeBackupSaveFileDialog(),
            confirm ?? new FakeConfirmationDialog(),
            effectiveClock,
            sync ?? new FakeSyncService(),
            effectiveScheduler,
            RecordumOptions.Resolve([RecordumOptions.DataDirectoryArgument, Path.Combine(_tempDir, "data")], _ => null),
            navigator ?? new FakeHabitNavigator());
    }

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
                    ItemsToFix: 3,
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

    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public async Task IncludePhotos_is_on_until_turned_off(string? stored, bool expected)
    {
        var settings = new FakeAppSettingService();
        if (stored is not null) settings.Store[SyncSettingsViewModel.IncludePhotosKey] = stored;
        var vm = CreateVm(settings: settings);

        await vm.LoadAsync();

        vm.IncludePhotos.Should().Be(expected);
    }

    [Fact]
    public async Task IncludePhotos_persists_setting()
    {
        var settings = new FakeAppSettingService();
        var vm = CreateVm(settings: settings);
        await vm.LoadAsync();
        vm.IncludePhotos.Should().BeTrue("photos are on until the user turns them off (spec 9)");

        vm.IncludePhotos = false;
        settings.Store[SyncSettingsViewModel.IncludePhotosKey].Should().Be("false");

        var reloaded = CreateVm(settings: settings);
        await reloaded.LoadAsync();
        reloaded.IncludePhotos.Should().BeFalse();
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
        vm.PlainPassphrase.Should().BeEmpty("la frase in chiaro non resta dopo l'uso");
        Encoding.UTF8.GetString(sync.ExportedPlainBytes!).Should().Be("rcsync-input");
        Encoding.UTF8.GetString(await File.ReadAllBytesAsync(zipOut)).Should().Be("plain-zip-payload");

        open.Result = zipOut;
        save.Result = rcsyncOut;
        vm.PlainPassphrase = "plain-passphrase-123";
        await vm.ReencryptCommand.ExecuteAsync(null);

        vm.PlainPassphrase.Should().BeEmpty();
        Encoding.UTF8.GetString(sync.ReencryptedBytes!).Should().Be("plain-zip-payload");
        Encoding.UTF8.GetString(await File.ReadAllBytesAsync(rcsyncOut)).Should().Be("encrypted-rcsync-payload");
    }

    [Fact]
    public async Task RefusedJoinShowsTranslatedMessage()
    {
        var sync = new FakeSyncService { JoinState = SyncJoinState.NeedsChoice, RefuseJoin = true };
        var vm = CreateVm(sync: sync);
        await vm.LoadAsync();

        await vm.JoinStartFromOthersCommand.ExecuteAsync(null);
        vm.ErrorMessage.Should().Be(Strings.Sync_Join_NoPeerFile);

        vm.ErrorMessage = null;
        await vm.JoinKeepBothCommand.ExecuteAsync(null);
        vm.ErrorMessage.Should().Be(Strings.Sync_Join_NoPeerFile);
        vm.JoinState.Should().Be(SyncJoinState.NeedsChoice);
    }

    private sealed class ReviewStubSyncService : ISyncService
    {
        private TaskCompletionSource? _entered;
        private TaskCompletionSource? _release;
        private int _runCalls;

        public int RunCalls => Volatile.Read(ref _runCalls);
        public int JoinCalls { get; private set; }
        public int DisconnectCalls { get; private set; }
        public int SetPassphraseCalls { get; private set; }
        public SyncJoinState JoinState { get; set; } = SyncJoinState.NeedsChoice;
        public SyncJoinRefusal? RefuseReason { get; set; }
        public Func<Stream, Stream, string, CancellationToken, Task>? OnExportPlain { get; set; }
        public Func<Stream, Stream, string, CancellationToken, Task>? OnReencrypt { get; set; }
        public Task Entered => _entered?.Task ?? Task.CompletedTask;

        public void BlockNextJoin()
        {
            _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void Release() => _release?.TrySetResult();

        public Task<SyncRunResult> RunAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _runCalls);
            return Task.FromResult(new SyncRunResult(true, true, []));
        }

        public Task<SyncStatus> GetStatusAsync(CancellationToken ct) =>
            Task.FromResult(new SyncStatus(null, [], 0, 0));

        public Task<IReadOnlyList<SyncItemToFix>> ListItemsToFixAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SyncItemToFix>>([]);

        public Task AcceptRepairAsync(Guid periodId, CancellationToken ct) => Task.CompletedTask;

        public Task SetPassphraseAsync(string passphrase, CancellationToken ct)
        {
            SetPassphraseCalls++;
            return Task.CompletedTask;
        }

        public Task<SyncJoinState> GetJoinStateAsync(CancellationToken ct) => Task.FromResult(JoinState);

        public async Task JoinAsync(SyncJoinChoice choice, CancellationToken ct)
        {
            JoinCalls++;
            _entered?.TrySetResult();
            if (_release is not null)
                await _release.Task.WaitAsync(ct);
            if (RefuseReason is { } reason)
                throw new SyncJoinRefusedException(reason, "refused");
            JoinState = SyncJoinState.Joined;
        }

        public Task DisconnectAsync(CancellationToken ct)
        {
            DisconnectCalls++;
            JoinState = SyncJoinState.NotConfigured;
            return Task.CompletedTask;
        }

        public Task ExportPlainAsync(Stream rcsync, Stream zipOut, string passphrase, CancellationToken ct) =>
            OnExportPlain?.Invoke(rcsync, zipOut, passphrase, ct) ?? Task.CompletedTask;

        public Task ReencryptAsync(Stream zipIn, Stream rcsyncOut, string passphrase, CancellationToken ct) =>
            OnReencrypt?.Invoke(zipIn, rcsyncOut, passphrase, ct) ?? Task.CompletedTask;
    }

    [Fact]
    public async Task ExportPlain_wrong_passphrase_keeps_existing_destination_intact()
    {
        var dataDir = Path.Combine(_tempDir, "data");
        var options = RecordumOptions.Resolve([RecordumOptions.DataDirectoryArgument, dataDir], _ => null);
        var rcsyncIn = Path.Combine(_tempDir, "in.rcsync");
        var zipOut = Path.Combine(_tempDir, "existing.zip");
        await File.WriteAllTextAsync(rcsyncIn, "rcsync-bytes");
        await File.WriteAllTextAsync(zipOut, "existing-zip-intact");

        string[] tempFilesDuringCall = [];
        var sync = new ReviewStubSyncService
        {
            OnExportPlain = async (_, dst, _, ct) =>
            {
                tempFilesDuringCall = Directory.GetFiles(options.TempDirectory);
                await dst.WriteAsync(Encoding.UTF8.GetBytes("partial"), ct);
                throw new InvalidOperationException("wrong passphrase");
            },
        };
        var clock = new FakeClock(new DateTimeOffset(2026, 3, 15, 14, 0, 0, TimeSpan.Zero));
        using var scheduler = new SyncSchedulerHost(new EmptyScopeFactory(), new FakeTickSourceFactory(), clock, NullLogger<SyncSchedulerHost>.Instance);
        var vm = new SyncSettingsViewModel(
            new FakeAppSettingService(),
            new FakeFolderPicker(),
            new FakeOpenFileDialog { Result = rcsyncIn },
            new FakeBackupSaveFileDialog { Result = zipOut },
            new FakeConfirmationDialog(),
            clock,
            sync,
            scheduler,
            options,
            new FakeHabitNavigator());
        vm.PlainPassphrase = "wrong-passphrase";

        await vm.ExportPlainCommand.ExecuteAsync(null);

        (await File.ReadAllTextAsync(zipOut)).Should().Be("existing-zip-intact");
        tempFilesDuringCall.Should().ContainSingle("il temporaneo in chiaro deve vivere in TempDirectory");
        Directory.GetFiles(options.TempDirectory).Should().BeEmpty("il temporaneo va eliminato se l'operazione fallisce");
        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.PlainPassphrase.Should().BeEmpty();
    }

    [Fact]
    public async Task Reencrypt_wrong_passphrase_keeps_existing_destination_intact()
    {
        var outDir = Path.Combine(_tempDir, "cloud");
        Directory.CreateDirectory(outDir);
        var zipIn = Path.Combine(_tempDir, "in.zip");
        var rcsyncOut = Path.Combine(outDir, "existing.rcsync");
        await File.WriteAllTextAsync(zipIn, "zip-bytes");
        await File.WriteAllTextAsync(rcsyncOut, "existing-rcsync-intact");

        string[] filesInOutDirDuringCall = [];
        var sync = new ReviewStubSyncService
        {
            OnReencrypt = async (_, dst, _, ct) =>
            {
                filesInOutDirDuringCall = Directory.GetFiles(outDir);
                await dst.WriteAsync(Encoding.UTF8.GetBytes("partial"), ct);
                throw new InvalidOperationException("wrong passphrase");
            },
        };
        var vm = CreateVm(
            openDialog: new FakeOpenFileDialog { Result = zipIn },
            saveDialog: new FakeBackupSaveFileDialog { Result = rcsyncOut },
            sync: sync);
        vm.PlainPassphrase = "wrong-passphrase";

        await vm.ReencryptCommand.ExecuteAsync(null);

        (await File.ReadAllTextAsync(rcsyncOut)).Should().Be("existing-rcsync-intact");
        filesInOutDirDuringCall.Should().HaveCount(2, "il temporaneo del .rcsync sta accanto a outPath");
        Directory.GetFiles(outDir).Should().Equal(rcsyncOut);
        vm.ErrorMessage.Should().NotBeNullOrEmpty();
        vm.PlainPassphrase.Should().BeEmpty();
    }

    [Fact]
    public async Task RefusedJoin_distinguishes_MissingConfiguration_and_NoReadablePeer()
    {
        var sync = new ReviewStubSyncService { JoinState = SyncJoinState.NeedsChoice, RefuseReason = SyncJoinRefusal.MissingConfiguration };
        var vm = CreateVm(sync: sync);
        await vm.LoadAsync();

        await vm.JoinKeepBothCommand.ExecuteAsync(null);
        vm.ErrorMessage.Should().Be(Strings.Sync_Join_MissingConfiguration);

        vm.ErrorMessage = null;
        await vm.JoinStartFromOthersCommand.ExecuteAsync(null);
        vm.ErrorMessage.Should().Be(Strings.Sync_Join_MissingConfiguration);

        sync.RefuseReason = SyncJoinRefusal.NoReadablePeer;
        vm.ErrorMessage = null;
        await vm.JoinStartFromOthersCommand.ExecuteAsync(null);
        vm.ErrorMessage.Should().Be(Strings.Sync_Join_NoPeerFile);
    }

    [Fact]
    public async Task ConnectionCommands_share_single_reentrancy_guard()
    {
        var sync = new ReviewStubSyncService { JoinState = SyncJoinState.NeedsChoice };
        sync.BlockNextJoin();
        var vm = CreateVm(sync: sync);
        await vm.LoadAsync();
        vm.NewPassphrase = "valid-passphrase-1234";
        vm.ConfirmPassphrase = "valid-passphrase-1234";

        var first = vm.JoinKeepBothCommand.ExecuteAsync(null);
        await sync.Entered;

        vm.JoinKeepBothCommand.CanExecute(null).Should().BeFalse();
        vm.JoinStartFromOthersCommand.CanExecute(null).Should().BeFalse();
        vm.DisconnectCommand.CanExecute(null).Should().BeFalse();
        vm.SetPassphraseCommand.CanExecute(null).Should().BeFalse();

        // ExecuteAsync non consulta CanExecute: la guardia interna deve bloccare ogni comando concorrente.
        await vm.JoinKeepBothCommand.ExecuteAsync(null);
        await vm.JoinStartFromOthersCommand.ExecuteAsync(null);
        await vm.DisconnectCommand.ExecuteAsync(null);
        await vm.SetPassphraseCommand.ExecuteAsync(null);

        sync.Release();
        await first;

        sync.JoinCalls.Should().Be(1);
        sync.DisconnectCalls.Should().Be(0);
        sync.SetPassphraseCalls.Should().Be(0);
        vm.JoinKeepBothCommand.CanExecute(null).Should().BeTrue();
        vm.JoinStartFromOthersCommand.CanExecute(null).Should().BeTrue();
        vm.DisconnectCommand.CanExecute(null).Should().BeTrue();
        vm.SetPassphraseCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task ConfigChanges_nudge_scheduler_only_when_AutoMerge_is_on()
    {
        var sync = new ReviewStubSyncService();
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

        var picker = new FakeFolderPicker { Result = "/cloud/Recordum" };
        var vm = CreateVm(settings: settings, folderPicker: picker, clock: clock, sync: sync, scheduler: host);
        await vm.LoadAsync();
        vm.AutoMerge.Should().BeTrue();

        await vm.ChooseFolderCommand.ExecuteAsync(null);
        await host.CurrentRun;
        sync.RunCalls.Should().Be(1);

        clock.Advance(TimeSpan.FromMinutes(1));
        vm.NewPassphrase = "correct-horse-battery";
        vm.ConfirmPassphrase = "correct-horse-battery";
        await vm.SetPassphraseCommand.ExecuteAsync(null);
        await host.CurrentRun;
        sync.RunCalls.Should().Be(2);

        clock.Advance(TimeSpan.FromMinutes(1));
        vm.IncludePhotos = false;
        await host.CurrentRun;
        sync.RunCalls.Should().Be(3);

        vm.AutoMerge = false;
        clock.Advance(TimeSpan.FromMinutes(1));
        picker.Result = "/cloud/Recordum2";
        await vm.ChooseFolderCommand.ExecuteAsync(null);
        clock.Advance(TimeSpan.FromMinutes(1));
        vm.NewPassphrase = "another-horse-battery";
        vm.ConfirmPassphrase = "another-horse-battery";
        await vm.SetPassphraseCommand.ExecuteAsync(null);
        clock.Advance(TimeSpan.FromMinutes(1));
        vm.IncludePhotos = true;
        await host.CurrentRun;

        sync.RunCalls.Should().Be(3);
    }

    [Fact]
    public async Task Disconnect_clears_Folder()
    {
        var settings = new FakeAppSettingService();
        settings.Store[SyncSettingsViewModel.FolderKey] = "/cloud/Recordum";
        var confirm = new FakeConfirmationDialog { Result = true };
        var sync = new ReviewStubSyncService { JoinState = SyncJoinState.Joined };
        var vm = CreateVm(settings: settings, confirm: confirm, sync: sync);
        await vm.LoadAsync();
        vm.Folder.Should().Be("/cloud/Recordum");

        await vm.DisconnectCommand.ExecuteAsync(null);

        vm.Folder.Should().BeNull();
    }

    [Fact]
    public async Task ItemsToFix_row_appears_only_when_items_exist()
    {
        var prevCulture = CultureInfo.CurrentCulture;
        var prevUiCulture = CultureInfo.CurrentUICulture;
        var it = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            CultureInfo.CurrentCulture = it;
            CultureInfo.CurrentUICulture = it;

            var sync = new FakeSyncService();
            var vm = CreateVm(sync: sync);
            await vm.LoadAsync();

            vm.HasItemsToFix.Should().BeFalse();
            vm.ItemsToFix.Should().BeEmpty();
            vm.ItemsToFixSummary.Should().BeEmpty();

            var h1 = Guid.NewGuid();
            sync.Items.Add(new SyncItemToFix(SyncItemKind.Note, Guid.NewGuid(), h1, "Corsa", "#3B82F6", new DateOnly(2026, 11, 20), "— Telefono · 2026-11-20 10:00 — testo"));
            await vm.LoadAsync();

            vm.HasItemsToFix.Should().BeTrue();
            vm.ItemsToFix.Should().HaveCount(1);
            vm.ItemsToFixSummary.Should().Be("1 voce da sistemare");

            sync.Items.Add(new SyncItemToFix(SyncItemKind.DiaryEntry, Guid.NewGuid(), h1, "Diario", "#A855F7", new DateOnly(2026, 11, 19), "— Laptop · 2026-11-19 21:00 — voce"));
            await vm.LoadAsync();

            vm.ItemsToFixSummary.Should().Be("2 voci da sistemare");
        }
        finally
        {
            CultureInfo.CurrentCulture = prevCulture;
            CultureInfo.CurrentUICulture = prevUiCulture;
        }
    }

    [Fact]
    public async Task ToggleItemsListCommand_opens_and_closes_the_list()
    {
        var sync = new FakeSyncService();
        sync.Items.Add(new SyncItemToFix(SyncItemKind.Note, Guid.NewGuid(), Guid.NewGuid(), "Corsa", "#3B82F6", new DateOnly(2026, 11, 20), "— A · 2026-11-20 10:00 — x"));
        var vm = CreateVm(sync: sync);
        await vm.LoadAsync();

        vm.IsItemsListOpen.Should().BeFalse();

        vm.ToggleItemsListCommand.Execute(null);
        vm.IsItemsListOpen.Should().BeTrue();

        vm.ToggleItemsListCommand.Execute(null);
        vm.IsItemsListOpen.Should().BeFalse();
    }

    [Fact]
    public async Task OpenItemCommand_raises_CloseRequested_before_navigating()
    {
        var habitId = Guid.NewGuid();
        var day = new DateOnly(2026, 11, 18);
        var sync = new FakeSyncService();
        sync.Items.Add(new SyncItemToFix(SyncItemKind.DiaryEntry, Guid.NewGuid(), habitId, "Diario", "#A855F7", day, "— Telefono · 2026-11-18 22:00 — testo"));

        var trace = new List<string>();
        var navigator = new FakeHabitNavigator { OnOpen = () => trace.Add("navigate") };
        var vm = CreateVm(sync: sync, navigator: navigator);
        vm.CloseRequested += (_, _) => trace.Add("close");
        await vm.LoadAsync();

        await vm.OpenItemCommand.ExecuteAsync(vm.ItemsToFix[0]);

        trace.Should().Equal("close", "navigate");
        navigator.Calls.Should().ContainSingle().Which.Should().Be((habitId, "Diario", day));
    }

    [Fact]
    public async Task AcceptRepairCommand_calls_AcceptRepairAsync_and_reloads()
    {
        var periodId = Guid.NewGuid();
        var habitId = Guid.NewGuid();
        var sync = new FakeSyncService();
        sync.Items.Add(new SyncItemToFix(SyncItemKind.RepairedPeriod, periodId, habitId, "Fumo", "#CC4422", new DateOnly(2026, 11, 17), string.Empty));
        var vm = CreateVm(sync: sync);
        await vm.LoadAsync();
        vm.ToggleItemsListCommand.Execute(null);
        vm.IsItemsListOpen.Should().BeTrue();

        await vm.AcceptRepairCommand.ExecuteAsync(vm.ItemsToFix[0]);

        sync.AcceptedRepairs.Should().Equal(periodId);
        vm.ItemsToFix.Should().BeEmpty();
        vm.HasItemsToFix.Should().BeFalse();
        vm.IsItemsListOpen.Should().BeFalse();
    }
}
