using Recordum.Core.Abstractions.Security;

namespace Recordum.Data.Security;

/// <summary>Deriva la chiave <b>una volta</b> e la serve dalla memoria. Serve perché
/// <c>SqlCipherKeyConnectionInterceptor</c> chiama <c>GetKeyAsync</c> a OGNI connessione aperta, e con
/// <see cref="PassphraseKeyProvider"/> ogni chiamata è un Argon2id da 0,7-0,9 s (misurato): senza
/// cache un DbContext costa quasi un secondo, e la migrazione di un DB nuovo 9 secondi. DPAPI non ha
/// il problema (un file letto e un'Unprotect), ed è per questo che nessuno se n'era accorto.
/// <para><b>Restituisce sempre una copia.</b> <see cref="KeyMaterial.Dispose"/> azzera i byte, e i
/// chiamanti lo fanno (<c>using var key = …</c>): se la cache consegnasse il proprio array, la seconda
/// connessione riceverebbe 32 zeri. I byte veri restano privati e vengono azzerati da
/// <see cref="Dispose"/>.</para></summary>
public sealed class CachingKeyProvider(IDbKeyProvider inner) : IDbKeyProvider, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private byte[]? _raw;
    private bool _disposed;

    public string ProviderId => inner.ProviderId;

    public async ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default)
    {
        // ConfigureAwait(false) ovunque: l'interceptor sincrono (ConnectionOpened) blocca su questo
        // metodo con GetAwaiter().GetResult(), e una continuazione che tornasse sul contesto UI
        // andrebbe in deadlock contro quel blocco.
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_raw is null)
            {
                using var fresh = await inner.GetKeyAsync(ct).ConfigureAwait(false);
                _raw = (byte[])fresh.RawKey.Clone();   // copia PRIMA che fresh.Dispose() azzeri l'originale
            }
            return new KeyMaterial((byte[])_raw.Clone());
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Wait();
        try
        {
            if (_disposed) return;
            _disposed = true;
            if (_raw is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(_raw);
        }
        finally
        {
            _gate.Release();
        }
        _gate.Dispose();
    }
}
