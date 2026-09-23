namespace Recordum.Presentation.Bootstrap;

/// <summary>Un passo di avvio che una shell aggiunge dopo le migrazioni. Serve a ciò che
/// <c>Recordum.Presentation</c> non può nominare: gli attivatori dei plugin first-party
/// (<c>IChartEngineActivator</c> vive in un assembly <c>net10.0-windows</c>, <c>IExportEngineActivator</c>
/// in uno che porta QuestPDF) e la sonda WebView2. La shell registra un adattatore per ciascuno
/// (<c>services.AddSingleton&lt;IStartupStep, …&gt;()</c>) e <see cref="CoreInitializer"/> li esegue in
/// ordine di registrazione. Nessuna registrazione = nessun passo, che è il caso di F1.</summary>
public interface IStartupStep
{
    Task RunAsync(CancellationToken ct = default);
}
