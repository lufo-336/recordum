using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recordum.Core.Abstractions.Security;
using Recordum.Data.Backup;
using Recordum.Data.Tests.Fixtures;
using Xunit;

namespace Recordum.Data.Tests.Backup;

/// <summary>Lo scheduler chiede la passphrase a un <see cref="IAutoBackupKeyStore"/> invece di aprire il file con
/// DPAPI da sé (Passo 0, task 0.7): con un key store in memoria questi test girano anche su Linux. DPAPI è coperto
/// da <c>DpapiAutoBackupKeyStoreTests</c>.</summary>
public class BackupSchedulerTests
{
    private const string Pass = "passphrase-1234567890";

    private sealed class MemoryKeyStore(string? key) : IAutoBackupKeyStore
    {
        public bool HasKey() => key is not null;
        public void WriteKey(string passphrase) => key = passphrase;
        public void DeleteKey() => key = null;
        public string? ReadKey() => key;
    }

    private sealed class ListLogger : ILogger<BackupScheduler>
    {
        public List<LogLevel> Levels { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }

    [Fact]
    public async Task RunDueAsync_runs_when_overdue_and_updates_last_run()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            // last_run = empty (overdue)
            var sched = fixture.NewScheduler(new MemoryKeyStore(Pass));
            await sched.RunDueAsync();

            await using var db = await fixture.DbFactory.CreateDbContextAsync();
            var last = await db.AppSettings.AsNoTracking().FirstAsync(x => x.Key == "backup.auto.last_run_utc");
            last.Value.Should().NotBeEmpty();
            Directory.EnumerateFiles(fixture.Options.BackupsDirectory, "auto-*.recbak").Should().NotBeEmpty();
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task RunDueAsync_skips_when_not_due()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
            {
                var s = await db.AppSettings.FirstAsync(x => x.Key == "backup.auto.last_run_utc");
                s.Value = fixture.Clock.UtcNow.ToString("O");
                await db.SaveChangesAsync();
            }
            var sched = fixture.NewScheduler(new MemoryKeyStore(Pass));
            await sched.RunDueAsync();

            Directory.EnumerateFiles(fixture.Options.BackupsDirectory, "auto-*.recbak").Should().BeEmpty();
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task Without_a_key_the_auto_backup_switches_itself_off_and_says_so()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var log = new ListLogger();
            var sched = fixture.NewScheduler(new MemoryKeyStore(null), log);
            await sched.RunDueAsync();

            await using var db = await fixture.DbFactory.CreateDbContextAsync();
            var enabled = await db.AppSettings.AsNoTracking().FirstAsync(x => x.Key == "backup.auto.enabled");
            enabled.Value.Should().Be("false");
            sched.IsEnabled.Should().BeFalse();
            (await db.AppSettings.AsNoTracking().SingleAsync(x => x.Key == BackupScheduler.DisabledReasonKey))
                .Value.Should().Be("no_passphrase", "la pagina Impostazioni deve poter dire che e' stato lo scheduler");
            log.Levels.Should().ContainSingle().Which.Should().Be(LogLevel.Warning, "prima si spegneva in silenzio (RV-5)");
            Directory.EnumerateFiles(fixture.Options.BackupsDirectory, "auto-*.recbak").Should().BeEmpty();
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task A_run_with_a_passphrase_leaves_no_disabled_reason()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            await fixture.NewScheduler(new MemoryKeyStore(Pass)).RunDueAsync();

            await using var db = await fixture.DbFactory.CreateDbContextAsync();
            (await db.AppSettings.AsNoTracking().AnyAsync(x => x.Key == BackupScheduler.DisabledReasonKey)).Should().BeFalse();
        }
        finally { await fixture.DisposeAsync(); }
    }

    private sealed class CountingKeyStore(Func<string?> read) : IAutoBackupKeyStore
    {
        public int Reads;
        public bool HasKey() => true;
        public void WriteKey(string passphrase) { }
        public void DeleteKey() { }
        public string? ReadKey() { Reads++; return read(); }
    }

    [Fact]
    public async Task An_unavailable_keyring_skips_the_tick_and_leaves_auto_backup_on()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            var log = new ListLogger();
            var store = new CountingKeyStore(() => throw new SecretStoreUnavailableException("keyring down"));
            var sched = fixture.NewScheduler(store, log);
            await sched.RunDueAsync();

            store.Reads.Should().Be(1);
            await using var db = await fixture.DbFactory.CreateDbContextAsync();
            (await db.AppSettings.AsNoTracking().FirstAsync(x => x.Key == "backup.auto.enabled")).Value.Should().Be("true");
            (await db.AppSettings.AsNoTracking().AnyAsync(x => x.Key == BackupScheduler.DisabledReasonKey)).Should().BeFalse();
            sched.IsEnabled.Should().BeTrue();
            log.Levels.Should().ContainSingle();
            Directory.EnumerateFiles(fixture.Options.BackupsDirectory, "auto-*.recbak").Should().BeEmpty();
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task The_passphrase_is_not_read_when_no_backup_is_due()
    {
        var fixture = await EncryptedDbFixture.SeedAsync();
        try
        {
            await using (var db = await fixture.DbFactory.CreateDbContextAsync())
            {
                var s = await db.AppSettings.FirstAsync(x => x.Key == "backup.auto.last_run_utc");
                s.Value = fixture.Clock.UtcNow.ToString("O");
                await db.SaveChangesAsync();
            }
            var store = new CountingKeyStore(() => Pass);
            await fixture.NewScheduler(store).RunDueAsync();

            store.Reads.Should().Be(0);
        }
        finally { await fixture.DisposeAsync(); }
    }
}
