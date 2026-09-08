using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.Detail;
using Xunit;
using Recordum.Core.Time;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.Detail;

public sealed class HourlyHistogramViewModelTests
{
    [Fact]
    public async Task LoadAsync_populates_24_buckets_and_max()
    {
        var fake = new FakeHourlyDistributionQueryService();
        fake.Buckets = new int[24];
        fake.Buckets[9] = 3;
        fake.Buckets[14] = 5;

        var vm = new HourlyHistogramViewModel(fake, new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero)));
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid() }, DayBoundary.Utc);

        Assert.Equal(24, vm.Buckets.Length);
        Assert.Equal(5, vm.MaxCount);
    }

    [Fact]
    public async Task SetRangeAsync_refetches_with_new_window()
    {
        var fake = new FakeHourlyDistributionQueryService();
        var vm = new HourlyHistogramViewModel(fake, new FakeClock(new DateTimeOffset(2026, 5, 22, 0, 0, 0, TimeSpan.Zero)));
        await vm.LoadAsync(new HabitNode { Id = Guid.NewGuid() }, DayBoundary.Utc);

        await vm.SetRangeAsync(TrendRange.Year1);

        Assert.Equal(TrendRange.Year1, vm.CurrentRange);
    }
}
