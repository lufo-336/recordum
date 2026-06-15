using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Services;
using Xunit;

namespace Recordum.Core.Tests.Services;

public class EntriesPeriodRangeTests
{
    // 2026-06-17 è un mercoledì: il lunedì della settimana ISO (15) è ≠ today, così l'arrotondamento è davvero esercitato.
    private static readonly DateOnly Wed = new(2026, 6, 17);

    [Fact]
    public void Settimana_is_iso_week_monday_to_sunday()
    {
        EntriesPeriodRange.Resolve(EntriesPeriod.Settimana, Wed)
            .Should().Be(((DateOnly?)new DateOnly(2026, 6, 15), (DateOnly?)new DateOnly(2026, 6, 21)));
    }

    [Fact]
    public void Mese_is_first_to_last_day_of_current_month()
    {
        EntriesPeriodRange.Resolve(EntriesPeriod.Mese, Wed)
            .Should().Be(((DateOnly?)new DateOnly(2026, 6, 1), (DateOnly?)new DateOnly(2026, 6, 30)));
    }

    [Fact]
    public void Anno_is_jan1_to_dec31_of_current_year()
    {
        EntriesPeriodRange.Resolve(EntriesPeriod.Anno, Wed)
            .Should().Be(((DateOnly?)new DateOnly(2026, 1, 1), (DateOnly?)new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Tutto_is_unbounded_both_sides()
    {
        EntriesPeriodRange.Resolve(EntriesPeriod.Tutto, Wed).Should().Be(((DateOnly?)null, (DateOnly?)null));
    }

    [Fact]
    public void Personalizzato_passes_custom_bounds_through()
    {
        var from = new DateOnly(2026, 3, 1);
        var to = new DateOnly(2026, 3, 31);
        EntriesPeriodRange.Resolve(EntriesPeriod.Personalizzato, Wed, from, to)
            .Should().Be(((DateOnly?)from, (DateOnly?)to));
    }

    [Fact]
    public void Personalizzato_keeps_one_side_unbounded_when_null()
    {
        var from = new DateOnly(2026, 3, 1);
        EntriesPeriodRange.Resolve(EntriesPeriod.Personalizzato, Wed, from, null)
            .Should().Be(((DateOnly?)from, (DateOnly?)null));
    }
}
