namespace Recordum.Core.Abstractions;

/// <summary>Un battito ripetuto consegnato sul thread dell'interfaccia. Un'istanza = un timer.
/// Astrae il DispatcherTimer, che esiste con lo stesso nome e la stessa forma sia in
/// System.Windows.Threading (WPF) sia in Avalonia.Threading, ma sono due tipi diversi in due
/// assembly diversi — e uno dei due non esiste fuori da Windows.</summary>
public interface ITickSource : IDisposable
{
    event EventHandler Tick;

    /// <summary>Avvia il battito. Chiamarlo su una sorgente già avviata non ha effetto.</summary>
    void Start();

    /// <summary>Ferma il battito senza disporre la sorgente: si può riavviare.</summary>
#pragma warning disable CA1716 // Method name 'Stop' conflicts with reserved keyword
    void Stop();
#pragma warning restore CA1716
}

/// <summary>Crea sorgenti di battito. Iniettato al posto di <c>new DispatcherTimer(...)</c>:
/// è la fabbrica, non la sorgente, a essere un servizio, perché ogni consumatore vuole il proprio
/// timer con il proprio intervallo.</summary>
public interface ITickSourceFactory
{
    ITickSource Create(TimeSpan interval);
}
