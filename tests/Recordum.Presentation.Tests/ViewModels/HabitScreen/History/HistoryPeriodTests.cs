using System.Globalization;
using FluentAssertions;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels.HabitScreen.History;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels.HabitScreen.History;

/// <summary>Il selettore di periodo della Cronologia (spec §6.1). Oggi è mercoledì 28/10/2026.</summary>
public sealed class HistoryPeriodTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture, _uiCulture = CultureInfo.CurrentUICulture;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero));

    public HistoryPeriodTests() => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("it-IT");

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    private HistoryPeriodViewModel Vm(DateOnly? created = null)
    {
        var vm = new HistoryPeriodViewModel(_clock);
        vm.SetContext(DayBoundary.Utc, created ?? new DateOnly(2024, 3, 5));
        return vm;
    }

    [Fact]
    public void Month_is_the_default()
    {
        var vm = Vm();
        vm.Period.Should().Be(HistoryPeriod.Month);
        (vm.From, vm.To).Should().Be((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Week_is_monday_to_sunday()
    {
        var vm = Vm();
        vm.Period = HistoryPeriod.Week;
        (vm.From, vm.To).Should().Be((new DateOnly(2026, 10, 26), new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void Month_steps_across_the_year()
    {
        var vm = Vm();
        for (var i = 0; i < 10; i++) vm.PreviousCommand.Execute(null);
        vm.From.Should().Be(new DateOnly(2025, 12, 1));
        vm.To.Should().Be(new DateOnly(2025, 12, 31));
        vm.NextCommand.Execute(null);
        vm.From.Should().Be(new DateOnly(2026, 1, 1));
    }

    [Fact]
    public void Next_stops_at_today()
    {
        var vm = Vm();
        vm.NextCommand.CanExecute(null).Should().BeFalse();
        vm.PreviousCommand.Execute(null);
        vm.NextCommand.CanExecute(null).Should().BeTrue();
        vm.TodayCommand.Execute(null);
        vm.From.Should().Be(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public void Three_months_is_the_month_and_two_before()
    {
        var vm = Vm();
        vm.Period = HistoryPeriod.ThreeMonths;
        (vm.From, vm.To).Should().Be((new DateOnly(2026, 8, 1), new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void Year_is_the_calendar_year()
    {
        var vm = Vm();
        vm.Period = HistoryPeriod.Year;
        (vm.From, vm.To).Should().Be((new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void All_starts_on_january_of_the_creation_year()
    {
        var vm = Vm(new DateOnly(2024, 3, 5));
        vm.Period = HistoryPeriod.All;
        (vm.From, vm.To).Should().Be((new DateOnly(2024, 1, 1), new DateOnly(2026, 10, 28)));
        vm.PreviousCommand.CanExecute(null).Should().BeFalse();
        vm.NextCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Custom_range_is_ordered()
    {
        var vm = Vm();
        vm.SetCustom(new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 3));
        vm.Period.Should().Be(HistoryPeriod.Custom);
        (vm.From, vm.To).Should().Be((new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 12)));
    }

    [Fact]
    public void Changed_fires_on_every_move()
    {
        var vm = Vm();
        var n = 0;
        vm.Changed += (_, _) => n++;
        vm.Period = HistoryPeriod.Week;
        vm.PreviousCommand.Execute(null);
        vm.SetCustom(new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 12));
        n.Should().Be(3);
    }

    [Fact]
    public void Labels()
    {
        var vm = Vm();
        vm.Label.Should().Be("ottobre 2026");
        vm.Period = HistoryPeriod.Week;
        vm.Label.Should().Be("26 ott – 1 nov 2026");
        vm.PreviousCommand.Execute(null);
        vm.Label.Should().Be("19–25 ott 2026");
        vm.Period = HistoryPeriod.ThreeMonths;
        vm.Label.Should().Be("ago – ott 2026");
        vm.Period = HistoryPeriod.Year;
        vm.Label.Should().Be("2026");
        vm.Period = HistoryPeriod.All;
        vm.Label.Should().Be("Tutto");
        vm.SetCustom(new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 12));
        vm.Label.Should().Be("3–12 ott 2026");
    }
}
