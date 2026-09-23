using Recordum.Presentation.Services;

namespace Recordum.UI.Services.Null;

/// <summary>L'editor Markdown ricco (WebView2 in WPF) non è nello scope di F1 (D12) — e sulla shell
/// Avalonia sparisce comunque (AvaloniaEdit + Markdown.Avalonia, nessun WebView2). "Non disponibile"
/// è `IsAvailable == false`: chi lo consulta sa già di dover ripiegare senza doverlo scoprire da
/// un'eccezione. `RuntimeVersion` usa il default dell'interfaccia (`Strings.Diagnostic_NotAvailable`).</summary>
public sealed class NullEditorAvailability : IEditorAvailability
{
    public bool IsAvailable => false;

    public Task ProbeAsync() => Task.CompletedTask;
}
