using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.Charts;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.Charts;

public sealed class ChartListItemViewModelTests
{
    [Fact]
    public void Surfaces_name_kind_and_series_count()
    {
        var chart = new Chart { Id = Guid.NewGuid(), Name = "Weight", ChartType = "line" };
        chart.Series.Add(new ChartSeries());
        chart.Series.Add(new ChartSeries());
        var vm = new ChartListItemViewModel(chart);
        Assert.Equal("Weight", vm.Name);
        Assert.Equal(2, vm.SeriesCount);
        Assert.Equal(chart.Id, vm.ChartId);
    }
}
