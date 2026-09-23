using System.Diagnostics;
using System.Reflection;
using Recordum.Core.Abstractions.Security;
using Recordum.Core.Errors;
using Recordum.Data.Security;
using Xunit;
using Xunit.Abstractions;

namespace Recordum.Data.Tests.Security;

public class CachingKeyProviderTests(ITestOutputHelper output)
{
    private static byte[] Key(byte seed)
    {
        var k = new byte[KeyMaterial.LengthBytes];
        for (var i = 0; i < k.Length; i++) k[i] = (byte)(seed + i);
        return k;
    }

    private sealed class CountingProvider(byte[] key) : IDbKeyProvider
    {
        private int _calls;
        public int Calls => _calls;
        public string ProviderId => "counting";

        public async ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            await Task.Yield();                    // un punto di sospensione vero: la corsa fra chiamate concorrenti è possibile
            return new KeyMaterial((byte[])key.Clone());
        }
    }

    [Fact]
    public async Task Derives_once_and_serves_every_later_call_from_the_cache()
    {
        var inner = new CountingProvider(Key(1));
        using var sut = new CachingKeyProvider(inner);

        for (var i = 0; i < 5; i++)
        {
            using var k = await sut.GetKeyAsync();
            Assert.Equal(Key(1), k.RawKey);
        }

        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Concurrent_first_calls_derive_once()
    {
        var inner = new CountingProvider(Key(2));
        using var sut = new CachingKeyProvider(inner);

        var keys = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => sut.GetKeyAsync().AsTask()));

        Assert.Equal(1, inner.Calls);
        Assert.All(keys, k => Assert.Equal(Key(2), k.RawKey));
    }

    [Fact]
    public async Task Callers_get_copies_so_disposing_one_cannot_corrupt_the_cache()
    {
        // KeyMaterial.Dispose AZZERA i byte, e SqlCipherKeyConnectionInterceptor lo fa a ogni
        // connessione (`using var key = ...`). Se la cache restituisse la propria istanza, la seconda
        // connessione riceverebbe 32 zeri e cifrerebbe/leggerebbe con una chiave sbagliata.
        using var sut = new CachingKeyProvider(new CountingProvider(Key(3)));

        var first = await sut.GetKeyAsync();
        var alsoFirst = await sut.GetKeyAsync();
        Assert.NotSame(first.RawKey, alsoFirst.RawKey);     // copie distinte, non un array condiviso

        first.Dispose();                                    // azzera SOLO la sua copia
        Assert.All(first.RawKey, b => Assert.Equal(0, b));  // (sanity: lo zeroing è avvenuto davvero)

        using var second = await sut.GetKeyAsync();
        Assert.Equal(Key(3), second.RawKey);                // la cache è intatta
        Assert.Equal(Key(3), alsoFirst.RawKey);             // e nemmeno l'altra copia è stata toccata
        alsoFirst.Dispose();
    }

    [Fact]
    public async Task A_failed_derivation_is_not_cached()
    {
        var attempts = 0;
        var inner = new FlakyProvider(() => ++attempts == 1 ? throw new KeyUnavailableException("t", "boom") : Key(4));
        using var sut = new CachingKeyProvider(inner);

        await Assert.ThrowsAsync<KeyUnavailableException>(() => sut.GetKeyAsync().AsTask());
        using var k = await sut.GetKeyAsync();

        Assert.Equal(Key(4), k.RawKey);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task Dispose_zeroes_the_private_cache_and_refuses_further_use()
    {
        var sut = new CachingKeyProvider(new CountingProvider(Key(5)));
        using (await sut.GetKeyAsync()) { }
        var field = typeof(CachingKeyProvider).GetField("_raw", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.True(field is not null, "il campo privato _raw è stato rinominato: aggiornare questo test");
        var cache = (byte[])field!.GetValue(sut)!;
        Assert.Equal(Key(5), cache);                        // prima: la cache tiene la chiave

        sut.Dispose();

        Assert.All(cache, b => Assert.Equal(0, b));         // dopo: quei byte sono stati azzerati
        await Assert.ThrowsAsync<ObjectDisposedException>(() => sut.GetKeyAsync().AsTask());
    }

    [Fact]
    public async Task With_real_Argon2id_the_second_call_is_fast()
    {
        // La ragione d'essere della classe: Argon2id (m=64 MiB, t=4, p=2) costa 0,7-0,9 s e
        // SqlCipherKeyConnectionInterceptor chiama GetKeyAsync a OGNI connessione aperta.
        var saltPath = Path.Combine(Path.GetTempPath(), $"recordum-salt-{Guid.NewGuid():N}.bin");
        try
        {
            await File.WriteAllBytesAsync(saltPath, Key(9)[..16]);
            var raw = new PassphraseKeyProvider("correct horse battery staple", saltPath);
            using var sut = new CachingKeyProvider(raw);

            var sw = Stopwatch.StartNew();
            using var first = await sut.GetKeyAsync();
            var coldMs = sw.Elapsed.TotalMilliseconds;
            sw.Restart();
            for (var i = 0; i < 20; i++) { using var again = await sut.GetKeyAsync(); }
            var warmMs = sw.Elapsed.TotalMilliseconds / 20;

            output.WriteLine($"Argon2id: prima chiamata {coldMs:F0} ms, successive {warmMs:F3} ms/chiamata");
            using var uncached = await raw.GetKeyAsync();
            Assert.Equal(uncached.RawKey, first.RawKey);    // la cache non cambia la chiave, la memorizza
            Assert.True(warmMs < 50, $"le chiamate dopo la prima devono essere quasi gratis, erano {warmMs:F1} ms");
        }
        finally { if (File.Exists(saltPath)) File.Delete(saltPath); }
    }

    private sealed class FlakyProvider(Func<byte[]> produce) : IDbKeyProvider
    {
        public string ProviderId => "flaky";
        public ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(new KeyMaterial(produce()));
    }
}
