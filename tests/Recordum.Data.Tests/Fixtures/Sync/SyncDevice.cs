using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Recordum.Data.Sync;

namespace Recordum.Data.Tests.Fixtures.Sync;

/// <summary>Un dispositivo simulato per i test dell'unione: il suo database, il suo <c>device_id</c>, il suo orologio.
/// <see cref="WriteFileAsync"/> scrive il file cifrato come fa un giro; <see cref="MergeAsync"/> lo legge come un altro
/// dispositivo; <see cref="CanonicalDumpAsync"/> è lo stato confrontabile (righe pubbliche, foto, lapidi).</summary>
public sealed class SyncDevice : IAsyncDisposable
{
    private const string Passphrase = "test-sync-phrase";

    // Chiave fissa al posto di Argon2id: i test dell'unione non provano la busta (lo fa SyncFileCipherTests).
    private static readonly SyncFileCipher Cipher = new((_, _, _, _, _) => Task.FromResult(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()));

    public SyncDevice(string name, long startMs = 1_000, TimeZoneInfo? localTimeZone = null)
    {
        Name = name;
        DeviceId = $"device-{name}";
        Clock = new FakeClock(DateTimeOffset.FromUnixTimeMilliseconds(startMs), localTimeZone);
        Fixture = new DbFixture(Clock);
        Fixture.DeviceIdentity.DeviceName = name;
    }

    public string Name { get; }

    public string DeviceId { get; }

    public FakeClock Clock { get; }

    public DbFixture Fixture { get; }

    public Task EditAsync(long atMs, Action<RecordumDbContext> edit) =>
        EditAsync(atMs, db =>
        {
            edit(db);
            return Task.CompletedTask;
        });

    /// <summary>Una modifica dell'utente all'ora <paramref name="atMs"/>, salvata come fa l'app.</summary>
    public async Task EditAsync(long atMs, Func<RecordumDbContext, Task> edit)
    {
        Clock.Set(DateTimeOffset.FromUnixTimeMilliseconds(atMs));
        await using var db = await Fixture.Factory.CreateDbContextAsync();
        await edit(db);
        await db.SaveChangesAsync();
    }

    public async Task<T> QueryAsync<T>(Func<RecordumDbContext, Task<T>> query)
    {
        await using var db = await Fixture.Factory.CreateDbContextAsync();
        return await query(db);
    }

    public async Task<byte[]> WriteFileAsync(bool includePhotos = true) => await SealAsync(await WriteZipAsync(includePhotos));

    /// <summary>Lo ZIP in chiaro, per i test che fabbricano un file del pari a mano (poi <see cref="SealAsync"/>).</summary>
    public async Task<byte[]> WriteZipAsync(bool includePhotos = true)
    {
        await using var db = await Fixture.Factory.CreateDbContextAsync();
        using var zip = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(
            db, zip, new SyncWriteOptions(DeviceId, Name, "test", includePhotos, Clock.UtcNowMs));
        return zip.ToArray();
    }

    public static async Task<byte[]> SealAsync(byte[] zip)
    {
        using var file = new MemoryStream();
        await Cipher.EncryptAsync(new MemoryStream(zip), file, Passphrase);
        return file.ToArray();
    }

    public async Task<SyncMergeReport> MergeAsync(
        byte[] file, bool receivePhotos = true, Action<string>? afterEntity = null)
    {
        using var zip = new MemoryStream();
        await Cipher.DecryptAsync(new MemoryStream(file), zip, Passphrase);
        zip.Position = 0;
        using var snapshot = SyncArchiveReader.Open(zip);
        await using var db = await Fixture.Factory.CreateDbContextAsync();
        return await SyncMerger.MergeAsync(db, snapshot, DeviceId, receivePhotos, Clock, afterEntity, CancellationToken.None);
    }

    /// <summary>Tutte le righe pubbliche ordinate, l'hash di ogni foto e le lapidi: due dispositivi convergono quando
    /// questo testo è uguale.</summary>
    public async Task<string> CanonicalDumpAsync()
    {
        await using var db = await Fixture.Factory.CreateDbContextAsync();
        using var zip = new MemoryStream();
        await SyncArchiveWriter.WriteAsync(db, zip, new SyncWriteOptions("dump", "dump", "test", true, 0));
        zip.Position = 0;
        using var snapshot = SyncArchiveReader.Open(zip);

        var lines = new List<string>();
        foreach (var entity in SyncCatalog.Entities)
        {
            foreach (var row in snapshot.Rows[entity.PublicName])
            {
                var fields = row.Fields.Keys.Order(StringComparer.Ordinal).Select(k => $"{k}={row.Fields[k].GetRawText()}");
                lines.Add($"{entity.PublicName} {row.Id} {string.Join(",", fields)}");
                if (entity.Fields.Any(f => f.Kind == SyncFieldKind.Photo))
                    lines.Add($"  photos {Hash(snapshot.OpenPhoto(row.Id, false))} {Hash(snapshot.OpenPhoto(row.Id, true))}");
            }
        }

        lines.AddRange(snapshot.Tombstones.Select(t => $"tombstone {t.Entity} {t.EntityId} {t.DeletedAtMs} {t.DeviceId} {t.AliasOf}"));
        return string.Join("\n", lines);
    }

    public async Task ClearBaseAsync()
    {
        await using var db = await Fixture.Factory.CreateDbContextAsync();
        await db.SyncBases.ExecuteDeleteAsync();
    }

    public ValueTask DisposeAsync() => Fixture.DisposeAsync();

    private static string Hash(Stream? stream)
    {
        if (stream is null) return "-";
        using (stream) return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
