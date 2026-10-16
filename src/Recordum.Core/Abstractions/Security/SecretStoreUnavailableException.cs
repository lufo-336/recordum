namespace Recordum.Core.Abstractions.Security;

/// <summary>Ogni guasto del portachiavi (libreria assente, servizio irraggiungibile, collezione bloccata) esce così.
/// Il messaggio non contiene mai il valore di un segreto.</summary>
public sealed class SecretStoreUnavailableException : Exception
{
    public SecretStoreUnavailableException(string message) : base(message) { }

    public SecretStoreUnavailableException(string message, Exception inner) : base(message, inner) { }
}
