using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Charts;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.Charts;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.Charts;

public sealed class ChartBuilderViewModelTests
{
    [Fact]
    public void AddSeries_then_RemoveSeries_mutates_collection()
    {
        var vm = new ChartBuilderViewModel(new FakeChartService());
        vm.LoadNew();
        vm.AddSeriesCommand.Execute(null);
        vm.AddSeriesCommand.Execute(null);
        Assert.Equal(2, vm.Series.Count);
        vm.RemoveSeriesCommand.Execute(vm.Series[0]);
        Assert.Single(vm.Series);
    }

    [Fact]
    public async Task Save_persists_chart_and_series_with_selected_kind()
    {
        var svc = new FakeChartService();
        var vm = new ChartBuilderViewModel(svc);
        vm.LoadNew();
        vm.Name = "My chart";
        vm.SelectedKind = ChartKind.Bar;
        vm.AddSeriesCommand.Execute(null);
        vm.Series[0].HabitNodeId = Guid.NewGuid();
        vm.Series[0].Label = "S1";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(svc.Created);
        Assert.Equal("bar", svc.Created[0].chart.ChartType);
        Assert.Equal("My chart", svc.Created[0].chart.Name);
        Assert.Single(svc.Created[0].series);
    }

    [Fact]
    public async Task Save_existing_updates_and_replaces_series()
    {
        var svc = new FakeChartService();
        var existing = new Chart { Id = Guid.NewGuid(), Name = "Old", ChartType = "line" };
        svc.Store[existing.Id] = existing;
        var vm = new ChartBuilderViewModel(svc);
        await vm.LoadAsync(existing.Id);
        vm.Name = "New";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("New", svc.Store[existing.Id].Name);
        Assert.Contains(existing.Id, svc.SeriesReplaced);
    }
}
