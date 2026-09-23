using Recordum.Core.Abstractions.Security;

namespace Recordum.Presentation.Tests.Fixtures;

/// <summary>Una chiave DB fissa per i test che aprono un SQLCipher vero in una cartella temporanea:
/// niente DPAPI (Windows-only) e niente Argon2 (0,7 s per connessione). Restituisce ogni volta una
/// COPIA, perché <see cref="KeyMaterial.Dispose"/> azzera i byte e i chiamanti lo fanno.</summary>
public sealed class FixedKeyProvider(byte[] key) : IDbKeyProvider
{
    public string ProviderId => "fixed";

    public ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(new KeyMaterial((byte[])key.Clone()));
}
