using Recordum.Plugins.Contracts.Charts;
using Recordum.Plugins.Contracts.ExtensionPoints;

namespace Recordum.WPF.Tests.Fixtures;

public sealed class FakeChartRenderer : IChartRenderer
{
    public bool CreateViewCalled { get; private set; }
    public ExtensionPointId PointId => WellKnownExtensionPoints.ChartRenderer;
    public string PluginId => "test.renderer";
    public bool CanRender(ChartKind kind) => true;
    public object CreateView(ChartRenderModel model)
    {
        CreateViewCalled = true;
        return new object();   // host casts to FrameworkElement in prod; tests assert non-null
    }
}
