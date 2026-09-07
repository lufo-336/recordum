using System.Globalization;
using FluentAssertions;
using Recordum.Core.Abstractions;
using Recordum.Core.Time;
using Xunit;

namespace Recordum.Core.Tests.Time;

public class DayBoundaryTests
{
    private static DayBoundary Rome(int resetHour = 0)
        => new(TestTimeZones.RomeLike, new TimeOnly(resetHour, 0));

    /// <summary>Orologio finto per <see cref="DayBoundary.Today"/>: nessuna dipendenza dal
    /// tempo reale della macchina che esegue il test.</summary>
    private sealed class FixedClock(DateTimeOffset now, TimeZoneInfo timeZone) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
        public long UtcNowMs { get; } = now.ToUnixTimeMilliseconds();
        public TimeZoneInfo LocalTimeZone { get; } = timeZone;
    }

    // --- DayOf: il caso che ha aperto R9 ---

    [Fact]
    public void Half_past_midnight_local_belongs_to_the_local_day_not_the_UTC_one()
    {
        // 2026-08-18T22:30Z = 2026-08-19 00:30 a Roma (UTC+2 d'estate).
        var instant = new DateTimeOffset(2026, 8, 18, 22, 30, 0, TimeSpan.Zero);
        Rome().DayOf(instant).Should().Be(new DateOnly(2026, 8, 19));
    }

    [Theory]
    // reset 00:00: il confine è mezzanotte locale
    [InlineData(0, "2026-08-18T21:59:59Z", "2026-08-18")]
    [InlineData(0, "2026-08-18T22:00:00Z", "2026-08-19")]
    // reset 04:00: le 02:30 locali appartengono al giorno prima, le 04:00 in punto al giorno dopo
    [InlineData(4, "2026-08-19T00:30:00Z", "2026-08-18")] // 02:30 locali
    [InlineData(4, "2026-08-19T01:59:59Z", "2026-08-18")] // 03:59:59 locali
    [InlineData(4, "2026-08-19T02:00:00Z", "2026-08-19")] // 04:00:00 locali — inclusivo in avanti
    public void DayOf_honours_the_reset_time(int resetHour, string instantIso, string expectedDay)
        => Rome(resetHour).DayOf(DateTimeOffset.Parse(instantIso, CultureInfo.InvariantCulture))
            .Should().Be(DateOnly.Parse(expectedDay, CultureInfo.InvariantCulture));

    [Fact]
    public void Negative_offset_zone_is_handled_too()
    {
        // UTC-5: le 20:00 locali del 18 sono l'01:00Z del 19.
        var utcMinus5 = new DayBoundary(TestTimeZones.Fixed(-5), TimeOnly.MinValue);
        utcMinus5.DayOf(new DateTimeOffset(2026, 8, 19, 1, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateOnly(2026, 8, 18));
    }

    // --- Gli altri overload di DayOf, e Today ---

    [Fact]
    public void DayOf_unixMs_honours_local_day_the_same_way_as_DayOf_DateTimeOffset()
    {
        // Stesso istante di Half_past_midnight_local_belongs_to_the_local_day_not_the_UTC_one,
        // passato come millisecondo Unix (la forma di completed_at_ms/started_at_ms/ended_at_ms).
        var unixMs = new DateTimeOffset(2026, 8, 18, 22, 30, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        Rome().DayOf(unixMs).Should().Be(new DateOnly(2026, 8, 19));
    }

    [Theory]
    // I due orari sotto catturano assieme qualunque fuso macchina non-UTC, in entrambe le
    // direzioni: EF materializza CreatedAt/StreakBrokenAt con Kind=Unspecified, e senza lo
    // SpecifyKind(Utc) interno a DayOf(DateTime) quel valore verrebbe reinterpretato nel fuso
    // *locale della macchina* (TimeZoneInfo.Local), non in UTC — un giorno diverso in silenzio,
    // e diverso a seconda di dove gira il processo. Il primo caso (inizio giornata) è sbagliato
    // sotto qualunque fuso macchina A EST di UTC (offset positivo, es. l'Italia: coperto da
    // questa VM); il secondo (fine giornata) sotto qualunque fuso A OVEST (offset negativo,
    // es. le Americhe). Solo una macchina configurata esattamente su UTC (offset zero) non
    // discrimina nessuno dei due — vedi il report per la verifica fatta su questa VM.
    [InlineData("2026-08-19T00:00:00")]
    [InlineData("2026-08-19T23:00:00")]
    public void DayOf_DateTime_treats_Unspecified_kind_as_UTC_not_as_the_machines_local_zone(string utcTimestampIso)
    {
        var utcTimestamp = DateTime.Parse(utcTimestampIso, CultureInfo.InvariantCulture, DateTimeStyles.None);
        DayBoundary.Utc.DayOf(utcTimestamp).Should().Be(new DateOnly(2026, 8, 19));
    }

    [Fact]
    public void Today_returns_DayOf_of_the_clocks_UtcNow()
    {
        // Stesso istante di Half_past_midnight_local_belongs_to_the_local_day_not_the_UTC_one,
        // ma passato attraverso IClock invece che direttamente.
        var clock = new FixedClock(
            new DateTimeOffset(2026, 8, 18, 22, 30, 0, TimeSpan.Zero), TestTimeZones.RomeLike);
        Rome().Today(clock).Should().Be(new DateOnly(2026, 8, 19));
    }

    [Fact]
    public void Today_guards_against_a_null_clock()
    {
        var act = () => Rome().Today(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // --- RangeOf: la finestra e il giorno devono essere la stessa cosa ---

    [Fact]
    public void RangeOf_and_DayOf_agree_on_every_boundary()
    {
        var b = Rome(4);
        var day = new DateOnly(2026, 8, 19);
        var (from, toExclusive) = b.RangeOf(day);

        b.DayOf(from).Should().Be(day);
        b.DayOf(from.AddTicks(-1)).Should().Be(day.AddDays(-1));
        b.DayOf(toExclusive.AddTicks(-1)).Should().Be(day);
        b.DayOf(toExclusive).Should().Be(day.AddDays(1));
    }

    [Fact]
    public void RangeOf_spans_from_the_first_day_to_the_end_of_the_last()
    {
        var b = Rome();
        var (from, toExclusive) = b.RangeOf(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));
        from.Should().Be(b.StartOf(new DateOnly(2026, 8, 1)));
        toExclusive.Should().Be(b.StartOf(new DateOnly(2026, 9, 1)));
    }

    // --- Ora legale: la classe di bug che questo piano può introdurre ---

    [Fact]
    public void Spring_forward_day_is_23_hours_long()
    {
        var (from, to) = Rome().RangeOf(new DateOnly(2026, 3, 29));
        (to - from).Should().Be(TimeSpan.FromHours(23));
    }

    [Fact]
    public void Fall_back_day_is_25_hours_long()
    {
        var (from, to) = Rome().RangeOf(new DateOnly(2026, 10, 25));
        (to - from).Should().Be(TimeSpan.FromHours(25));
    }

    [Fact]
    public void A_reset_inside_the_skipped_hour_starts_the_day_at_the_transition()
    {
        // Il 29 marzo l'orologio salta da 02:00 a 03:00: le 02:00-02:59 locali non esistono.
        // Il giorno comincia all'istante della transizione (01:00Z), non un'ora prima o dopo.
        // Il reset è 02:30 (dentro l'ora saltata, non al suo bordo): un reset esattamente a
        // 02:00 non discrimina, perché l'offset "prima della transizione" che GetUtcOffset
        // restituisce su un orario invalido (+01:00, applicato a 02:00) e l'offset "dopo",
        // applicato al primo orario valido dopo il buco (+02:00 a 03:00), producono per
        // coincidenza lo stesso istante — 02:30 no.
        new DayBoundary(TestTimeZones.RomeLike, new TimeOnly(2, 30))
            .StartOf(new DateOnly(2026, 3, 29))
            .Should().Be(new DateTimeOffset(2026, 3, 29, 1, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_reset_inside_the_repeated_hour_starts_the_day_at_its_first_pass()
    {
        // Il 25 ottobre le 02:30 locali capitano due volte (00:30Z con +2, 01:30Z con +1).
        // Il giorno comincia alla prima, così entrambe le occorrenze gli appartengono.
        var b = new DayBoundary(TestTimeZones.RomeLike, new TimeOnly(2, 30));
        b.StartOf(new DateOnly(2026, 10, 25))
            .Should().Be(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero));
        b.DayOf(new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero))
            .Should().Be(new DateOnly(2026, 10, 25));
    }

    [Fact]
    public void Every_instant_of_a_DST_day_lands_in_exactly_one_day()
    {
        // La prova di non-sovrapposizione e non-buco: passo di 10 minuti su entrambe le transizioni.
        var b = new DayBoundary(TestTimeZones.RomeLike, new TimeOnly(2, 30));
        foreach (var d in new[] { new DateOnly(2026, 3, 29), new DateOnly(2026, 10, 25) })
        {
            var (from, to) = b.RangeOf(d);
            for (var t = from; t < to; t = t.AddMinutes(10))
                b.DayOf(t).Should().Be(d, $"{t:O} sta dentro la finestra di {d}");
        }
    }

    // --- NoonOf: dove atterrano le retroattive (Task 10 e 11) ---

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(6)]
    public void Noon_always_belongs_to_the_day_it_names(int resetHour)
    {
        // È la ragione per cui la scelta è "mezzogiorno" e non "mezzanotte": sta dentro il giorno
        // per qualunque reset plausibile (00:00-06:00).
        var b = Rome(resetHour);
        var day = new DateOnly(2026, 8, 15);
        b.DayOf(b.NoonOf(day)).Should().Be(day);
    }

    [Fact]
    public void Noon_belongs_to_its_day_even_when_the_day_is_23_or_25_hours_long()
    {
        var b = Rome();
        foreach (var d in new[] { new DateOnly(2026, 3, 29), new DateOnly(2026, 10, 25) })
            b.DayOf(b.NoonOf(d)).Should().Be(d);
    }

    // --- L'identità: è ciò che rende la suite esistente ancora vera ---

    [Fact]
    public void Utc_boundary_is_the_identity_of_todays_behaviour()
    {
        var instant = new DateTimeOffset(2026, 8, 18, 23, 30, 0, TimeSpan.Zero);
        DayBoundary.Utc.DayOf(instant).Should().Be(DateOnly.FromDateTime(instant.UtcDateTime));
    }
}
