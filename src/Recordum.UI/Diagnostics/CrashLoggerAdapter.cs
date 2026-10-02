using Microsoft.Extensions.Logging;

namespace Recordum.UI.Diagnostics;

/// <summary>Un <see cref="ILogger{TCategoryName}"/> che scrive nel log di <see cref="CrashLogger"/>, per i
/// servizi che girano prima della DI (lo sblocco). Passa solo il testo gia' formattato e l'eccezione: i
/// servizi che lo usano non mettono passphrase ne' chiavi nei messaggi, e qui non si aggiunge altro.
/// Solo Warning e oltre: il log e' un diario dei guasti.</summary>
public sealed class CrashLoggerAdapter<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        CrashLogger.Log($"{typeof(T).Name} [{logLevel}]: {formatter(state, exception)}", exception);
    }
}
