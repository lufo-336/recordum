namespace Recordum.Core.Errors;

/// <summary>
/// Thrown when a database encryption key is unavailable from its provider.
/// </summary>
public sealed class KeyUnavailableException(string providerId, string reason)
    : RecordumDomainException($"Key unavailable from provider '{providerId}': {reason}")
{
    public string ProviderId { get; } = providerId;
    public string Reason { get; } = reason;
}
