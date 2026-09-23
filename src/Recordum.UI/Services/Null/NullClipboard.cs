using Recordum.Presentation.Services;

namespace Recordum.UI.Services.Null;

/// <summary>Solo Diagnostic/Settings copiano negli appunti oggi (nessuno dei due è in scope F1, D12).
/// `TopLevel.Clipboard` è la strada reale (F6): finché non serve, un no-op non rompe nulla che F1
/// mostri a schermo.</summary>
public sealed class NullClipboard : IClipboard
{
    public void SetText(string text) { }
}
