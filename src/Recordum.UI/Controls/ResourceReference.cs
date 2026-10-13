using Avalonia;
using Avalonia.Controls;

namespace Recordum.UI.Controls;

/// <summary>Il gemello in codice di <c>{DynamicResource key}</c>: la proprietà segue la risorsa (tema
/// compreso) per tutta la vita del controllo. Serve ai controlli costruiti in code-behind.</summary>
internal static class ResourceReference
{
    public static void SetResourceReference(this Control control, AvaloniaProperty property, string key) =>
        control.Bind(property, control.GetResourceObservable(key));
}
