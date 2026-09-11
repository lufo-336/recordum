namespace Recordum.Core.Abstractions;

/// <summary>Ciò che un ViewModel può chiedere all'applicazione ospitante. Oggi solo l'uscita:
/// prima era `Application.Current?.Shutdown()`, che lega il ViewModel a WPF per una riga.</summary>
public interface IApplicationLifetime
{
    void Shutdown();
}

/// <summary>Quale tinta un marker del calendario chiede, senza sapere di che colore sia. La view
/// la risolve contro il tema corrente, quindi un cambio di tema si vede subito invece che alla
/// prossima ricarica.</summary>
public enum MarkerTint
{
    /// <summary>La tinta di completamento della propria abitudine (il default del navigatore).</summary>
    Default,

    /// <summary>Il giorno di una ricaduta.</summary>
    Danger,
}
