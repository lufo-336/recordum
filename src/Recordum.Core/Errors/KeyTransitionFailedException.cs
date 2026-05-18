namespace Recordum.Core.Errors;

/// <summary>
/// Thrown when a key transition (e.g., DPAPI → passphrase) fails.
/// </summary>
public sealed class KeyTransitionFailedException(string fromProvider, string toProvider, Exception inner)
    : RecordumDomainException($"Key transition {fromProvider} -> {toProvider} failed: {inner.Message}", inner)
{
    public string FromProvider { get; } = fromProvider;
    public string ToProvider { get; } = toProvider;
}
