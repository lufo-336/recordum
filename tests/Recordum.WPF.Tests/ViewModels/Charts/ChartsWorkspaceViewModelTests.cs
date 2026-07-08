using Recordum.Core.Models;
using Recordum.Plugins.ExtensionPoints;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Charts;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Charts;

public sealed class ChartsWorkspaceViewModelTests
{
    private static ChartsWorkspaceViewModel Build(FakeChartService svc, FakeChartRenderer renderer)
    {
        var registry = new ExtensionPointRegistry();
        registry.Register(new Recordum.Plugins.Contracts.ExtensionPoints.ExtensionRegistration(
            renderer.PluginId, renderer.PointId, renderer));
        return new ChartsWorkspaceViewModel(svc, new FakeChartQueryService(), registry,
            new ChartBuilderViewModel(svc, new FakeHabitTreeQueryService(), new FakeMetricDefinitionService(),
                new FakeChartQueryService(), registry));
    }

    [Fact]
    public async Task LoadAsync_populates_chart_list()
    {
        var svc = new FakeChartService();
        svc.Store[Guid.NewGuid()] = new Chart { Id = Guid.NewGuid(), Name = "A", ChartType = "line" };
        var vm = Build(svc, new FakeChartRenderer());
        await vm.LoadAsync();
        Assert.Single(vm.Charts);
    }

    [Fact]
    public async Task Selecting_chart_renders_a_view_via_registry()
    {
        var svc = new FakeChartService();
        var id = Guid.NewGuid();
        svc.Store[id] = new Chart { Id = id, Name = "A", ChartType = "line" };
        var renderer = new FakeChartRenderer();
        var vm = Build(svc, renderer);
        await vm.LoadAsync();

        await vm.SelectAsync(vm.Charts[0]);

        Assert.True(renderer.CreateViewCalled);
        Assert.NotNull(vm.RenderedView);
    }

    [Fact]
    public async Task Delete_removes_chart_and_clears_rendered_view()
    {
        var svc = new FakeChartService();
        var id = Guid.NewGuid();
        svc.Store[id] = new Chart { Id = id, Name = "A", ChartType = "line" };
        var vm = Build(svc, new FakeChartRenderer());
        await vm.LoadAsync();
        await vm.SelectAsync(vm.Charts[0]);

        await vm.DeleteCommand.ExecuteAsync(vm.Charts[0]);

        Assert.Empty(vm.Charts);
        Assert.Null(vm.RenderedView);
    }
}
