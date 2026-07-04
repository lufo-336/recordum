using Recordum.Plugins.Contracts.Charts;
using Recordum.Plugins.Contracts.ExtensionPoints;
using Recordum.Plugins.Contracts.Hosting;

namespace Recordum.WPF.ViewModels.Charts;

/// <summary>
/// Shared renderer lookup for chart views: finds the first registered <see cref="IChartRenderer"/>
/// that can render <paramref name="model"/>'s kind and asks it for a platform view, or <c>null</c>
/// when none matches. Used by both the workspace (persisted charts) and the builder (live preview).
/// </summary>
internal static class ChartRendererLocator
{
    public static object? CreateView(IExtensionPointRegistry registry, ChartRenderModel model) =>
        registry.GetExtensions(WellKnownExtensionPoints.ChartRenderer)
            .OfType<IChartRenderer>()
            .FirstOrDefault(r => r.CanRender(model.Kind))
            ?.CreateView(model);
}
