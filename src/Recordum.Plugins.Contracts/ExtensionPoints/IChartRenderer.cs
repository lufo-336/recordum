using Recordum.Plugins.Contracts.Charts;

namespace Recordum.Plugins.Contracts.ExtensionPoints;

/// <summary>
/// Extension registered under <see cref="WellKnownExtensionPoints.ChartRenderer"/>.
/// <see cref="CreateView"/> returns the platform view object (a WPF FrameworkElement in
/// Fase 3); the host casts it. Returning <see cref="object"/> keeps Contracts UI-free.
/// </summary>
public interface IChartRenderer : IExtensionPoint
{
    bool CanRender(ChartKind kind);
    object CreateView(ChartRenderModel model);
}
