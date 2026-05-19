namespace Recordum.Core.Abstractions.Security;

public interface IDbKeyProvider
{
    string ProviderId { get; }

    ValueTask<KeyMaterial> GetKeyAsync(CancellationToken ct = default);
}
