using System.Windows;
using Recordum.Core.Models;
using Recordum.Plugins.ChartEngine;
using Recordum.Plugins.ExtensionPoints;
using Recordum.Plugins.Hosting;
using Recordum.Plugins.Management;
using Recordum.Plugins.Permissions;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Charts;
using Xunit;

namespace Recordum.WPF.Tests.E2E;

public sealed class ChartsWorkspaceE2ETests
{
    [WpfFact]
    public async Task Selecting_a_chart_renders_a_FrameworkElement_via_the_real_engine()
    {
        var registry = new ExtensionPointRegistry();
        var manager = new PluginManager(new InProcessPluginHost(), new AlwaysAllowPermissionBroker(), registry);
        await new ChartEngineActivator(manager, registry).ActivateAsync();

        var svc = new FakeChartService();
        var id = Guid.NewGuid();
        svc.Store[id] = new Chart { Id = id, Name = "A", ChartType = "line" };
        var vm = new ChartsWorkspaceViewModel(svc, new FakeChartQueryService(), registry,
            new ChartBuilderViewModel(svc, new FakeHabitTreeQueryService(), new FakeMetricDefinitionService(),
                new FakeChartQueryService(), registry, null));

        await vm.LoadAsync();
        await vm.SelectAsync(vm.Charts[0]);

        Assert.IsAssignableFrom<FrameworkElement>(vm.RenderedView);
    }
}
