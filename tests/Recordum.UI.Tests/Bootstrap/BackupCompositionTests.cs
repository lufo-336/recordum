using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Backup;
using Recordum.Data;
using Recordum.Data.Security;
using Recordum.Presentation.Bootstrap;
using Recordum.UI.Bootstrap;
using Xunit;

namespace Recordum.UI.Tests.Bootstrap;

/// <summary>F2c Task 4: l'auto-backup acceso sulla composizione Avalonia, con la passphrase dal deposito giusto.</summary>
public sealed class BackupCompositionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"recordum-bkc-{Guid.NewGuid():N}");

    private sealed class FakeSecrets(string? autoBackup) : ISecretStore
    {
        public bool IsAvailable => true;
        public string? TryRead(SecretKind kind) => kind == SecretKind.AutoBackup ? autoBackup : null;
        public void Write(SecretKind kind, string value) { }
        public void Delete(SecretKind kind) { }
    }

    private sealed class FixedKey : IDbKeyProvider
    {
        public string ProviderId => "fixed-test-key";
        public ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(new KeyMaterial([.. Enumerable.Range(0, 32).Select(i => (byte)(i * 5 + 3))]));
    }

    private RecordumOptions Options() => RecordumOptions.Resolve([RecordumOptions.DataDirectoryArgument, _dir], _ => null);

    private async Task<ServiceProvider> BuildAsync(ISecretStore secrets, bool windows)
    {
        var sp = AppComposition.BuildServices(Options(), new FixedKey(), secrets, windows);
        await sp.GetRequiredService<CoreInitializer>().InitializeAsync();
        return sp;
    }

    [Fact]
    public void On_Linux_the_keyring_queues_the_operations_on_the_same_secret()
    {
        // F2c-4: senza la fila, un "Dimentica" puo' essere annullato da una scrittura dello sblocco tardiva.
        var store = AppComposition.CreateSecretStore(Options());
        if (OperatingSystem.IsLinux()) store.Should().BeOfType<SerializedSecretStore>();
        else store.Should().BeOfType<NullSecretStore>();
    }

    private static async Task SetAsync(ServiceProvider sp, string key, string value)
    {
        await using var db = await sp.GetRequiredService<IDbContextFactory<RecordumDbContext>>().CreateDbContextAsync();
        var row = await db.AppSettings.AsTracking().SingleAsync(x => x.Key == key);
        row.Value = value;
        await db.SaveChangesAsync();
    }

    [Fact]
    public void Linux_branch_registers_the_keyring_store_and_the_same_secret_store_instance()
    {
        var secrets = new FakeSecrets("pw");
        var services = new ServiceCollection();
        AppComposition.AddBackupKeyStores(services, Options(), secrets, windows: false);
        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IAutoBackupKeyStore>().Should().BeOfType<SecretStoreAutoBackupKeyStore>();
        sp.GetRequiredService<ISecretStore>().Should().BeSameAs(secrets);
        sp.GetRequiredService<IAutoBackupKeyStore>().ReadKey().Should().Be("pw");
    }

    [Fact]
    public void Windows_branch_registers_dpapi()
    {
        var services = new ServiceCollection();
        AppComposition.AddBackupKeyStores(services, Options(), new NullSecretStore(), windows: true);
        using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<IAutoBackupKeyStore>().Should().BeOfType<DpapiAutoBackupKeyStore>();
    }

    [Fact]
    public async Task Due_auto_backup_with_a_stored_passphrase_writes_a_recbak_and_a_history_row()
    {
        using var sp = await BuildAsync(new FakeSecrets("correct horse"), windows: false);
        await SetAsync(sp, "backup.auto.enabled", "true");
        await using (var scope = sp.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IBackupScheduler>().RunDueAsync();

        Directory.GetFiles(Options().BackupsDirectory, "*.recbak").Should().HaveCount(1);
        await using var db = await sp.GetRequiredService<IDbContextFactory<RecordumDbContext>>().CreateDbContextAsync();
        (await db.BackupLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Without_a_passphrase_the_auto_backup_switches_itself_off()
    {
        using var sp = await BuildAsync(new FakeSecrets(null), windows: false);
        await SetAsync(sp, "backup.auto.enabled", "true");
        await using var scope = sp.CreateAsyncScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IBackupScheduler>();
        await scheduler.RunDueAsync();

        scheduler.IsEnabled.Should().BeFalse();
        await using var db = await sp.GetRequiredService<IDbContextFactory<RecordumDbContext>>().CreateDbContextAsync();
        (await db.AppSettings.SingleAsync(x => x.Key == "backup.auto.enabled")).Value.Should().Be("false");
        Directory.GetFiles(Options().BackupsDirectory, "*.recbak").Should().BeEmpty();
    }

    [Fact]
    public async Task StartBackupScheduler_runs_the_loop_off_the_calling_thread_and_Dispose_stops_it()
    {
        var threads = new List<int>();
        var services = new ServiceCollection();
        services.AddRecordumCore(Options());
        services.AddScoped<IBackupScheduler>(_ => new ThreadRecorder(threads));   // dopo AddRecordumCore: vince
        services.AddSingleton(new BackupSchedulerOptions { InitialDelay = TimeSpan.FromMilliseconds(20), Interval = TimeSpan.FromMilliseconds(20) });
        var sp = services.BuildServiceProvider();

        // Un thread dedicato, vivo per tutto il test, fa da thread dell'interfaccia (un thread del pool potrebbe coincidere).
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var callerThread = 0;
        var caller = new Thread(() =>
        {
            callerThread = Environment.CurrentManagedThreadId;
            AppComposition.StartBackupScheduler(sp);
            started.Set();
            release.Wait();
        });
        caller.Start();
        started.Wait();
        await Task.Delay(500);
        sp.Dispose();
        release.Set();
        caller.Join();

        threads.Should().NotBeEmpty();
        threads.Should().NotContain(callerThread);
    }

    [Fact]
    public async Task InitializeAsync_starts_the_backup_host_after_the_core_initializer()
    {
        var threads = new List<int>();
        var services = new ServiceCollection();
        services.AddRecordumCore(Options());
        services.AddRecordumAvaloniaShell();
        services.AddSingleton<IDbKeyProvider>(new FixedKey());
        AppComposition.AddBackupKeyStores(services, Options(), new FakeSecrets("pw"), windows: false);
        services.AddScoped<IBackupScheduler>(_ => new ThreadRecorder(threads));
        services.AddSingleton(new BackupSchedulerOptions { InitialDelay = TimeSpan.FromMilliseconds(20), Interval = TimeSpan.FromMilliseconds(20) });
        using var sp = services.BuildServiceProvider();

        await AppComposition.InitializeAsync(sp);
        await Task.Delay(500);

        lock (threads) threads.Should().NotBeEmpty();
    }

    private sealed class ThreadRecorder(List<int> threads) : IBackupScheduler
    {
        public Task<BackupRunOutcome> RunDueAsync(CancellationToken ct = default) { lock (threads) threads.Add(Environment.CurrentManagedThreadId); return Task.FromResult(BackupRunOutcome.NotDue); }
        public DateTime? NextRunUtc => null;
        public bool IsEnabled => true;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }
}
