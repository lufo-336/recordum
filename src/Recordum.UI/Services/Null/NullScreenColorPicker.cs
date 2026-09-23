using Recordum.Presentation.Services;

namespace Recordum.UI.Services.Null;

/// <summary>Il contagocce resta Windows-only per contratto (l'interfaccia lo documenta). "Annullato"
/// per un color picker è null, esattamente come Esc/tasto-destro nell'implementazione Win32.</summary>
public sealed class NullScreenColorPicker : IScreenColorPicker
{
    public Task<string?> PickAsync() => Task.FromResult<string?>(null);
}
