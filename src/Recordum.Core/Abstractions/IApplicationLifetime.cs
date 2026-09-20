namespace Recordum.Core.Abstractions;

/// <summary>Ciò che un ViewModel può chiedere all'applicazione ospitante. Oggi solo l'uscita:
/// prima era `Application.Current?.Shutdown()`, che lega il ViewModel a WPF per una riga.</summary>
public interface IApplicationLifetime
{
    void Shutdown();
}

/// <summary>Quale tinta un marker del calendario chiede, senza sapere di che colore sia. La view
/// la risolve contro il tema corrente. Il guadagno è architetturale (nessun <c>Application.Current</c>
/// in un ViewModel): il re-tint a tema cambiato a caldo NON è consegnato — il convertitore WPF legge
/// <c>Brush.Danger</c> in modo imperativo dentro un MultiBinding le cui sorgenti non cambiano allo
/// scambio di tema, quindi la tinta resta quella vecchia fino alla prossima ricarica. Servirebbe una
/// sottoscrizione a <c>IThemeService.EffectiveThemeChanged</c> che forzi la ricarica (tracciato per
/// l'Onda 3).</summary>
public enum MarkerTint
{
    /// <summary>La tinta di completamento della propria abitudine (il default del navigatore).</summary>
    Default,

    /// <summary>Il giorno di una ricaduta.</summary>
    Danger,
}
