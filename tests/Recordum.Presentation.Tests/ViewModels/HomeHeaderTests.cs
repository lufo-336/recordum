using System.Collections.Immutable;
using System.Globalization;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Core.Time;
using Recordum.Presentation.Tests.Fixtures;
using Recordum.Presentation.ViewModels;
using Xunit;

namespace Recordum.Presentation.Tests.ViewModels;

/// <summary>La testa della Home (direzione visiva §5): "Oggi" con la data lunga del giorno dell'app, l'intestazione dei
/// giorni che finisce con oggi, l'avanzamento dei gruppi.</summary>
public class HomeHeaderTests
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static readonly DayBoundary Reset5 = new(TimeZoneInfo.Utc, new TimeOnly(5, 0));

    private static HomeTreeRow Row(string name, Guid? parent = null, HabitType? type = HabitType.Action, bool doneToday = false)
        => new(Guid.NewGuid(), parent, name, "#2F7FC1", type, null, 0, false, PinScope.None, 0, false,
               ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty) { IsCompletedToday = doneToday };

    private static async Task<HomeViewModel> Loaded(DateTimeOffset now, params HomeTreeRow[] rows)
    {
        var query = new FakeHabitTreeQueryService();
        query.HomeRows.AddRange(rows);
        var saved = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = It;
        CultureInfo.CurrentUICulture = It;
        try
        {
            var sut = new HomeViewModel(query, new FakeHabitNodeService(), new FakePurgeService(),
                clock: new FakeClock(now), resetTimes: new FakeResetTimeResolver(Reset5));
            await sut.LoadCommand.ExecuteAsync(null);
            return sut;
        }
        finally { (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = saved; }
    }

    private static readonly DateTimeOffset Wed1000 = new(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Today_label_is_the_long_date()
        => (await Loaded(Wed1000, Row("a"))).TodayLabel.Should().Be("mercoledì 28 ottobre");

    [Fact]
    public async Task Strip_header_ends_with_today()
    {
        var header = (await Loaded(Wed1000, Row("a"))).StripHeader;
        header.Should().HaveCount(14);
        header[^1].Should().Be(new StripCell("oggi", false, true));
        header[^2].Glyph.Should().Be("mar");
        header.Take(13).Should().OnlyContain(c => !c.IsToday);
    }

    [Fact]
    public async Task Before_the_reset_today_is_yesterday()
    {
        var sut = await Loaded(new DateTimeOffset(2026, 10, 28, 4, 30, 0, TimeSpan.Zero), Row("a"));
        sut.TodayLabel.Should().Be("martedì 27 ottobre");
        sut.StripHeader[^2].Glyph.Should().Be("lun");
    }

    [Fact]
    public async Task Max_visible_strip_length_is_the_longest_strip()
        => (await Loaded(Wed1000, Row("a"))).MaxVisibleStripLength.Should().Be(7);

    [Fact]
    public async Task Group_counts_children_done_today()
    {
        var group = Row("Mattina", type: null);
        var sub = Row("Igiene", group.HabitNodeId, type: null);
        var sut = await Loaded(Wed1000, group, sub,
            Row("Acqua", group.HabitNodeId, doneToday: true), Row("Corsa", group.HabitNodeId), Row("Meditazione", group.HabitNodeId),
            Row("Denti", sub.HabitNodeId, doneToday: true), Row("Doccia", sub.HabitNodeId, doneToday: true));

        var mattina = sut.Rows.Single(r => r.Name == "Mattina");
        mattina.ChildrenDoneToday.Should().Be(3);
        mattina.ChildrenTotal.Should().Be(5);
        var igiene = sut.Rows.Single(r => r.Name == "Igiene");
        igiene.ChildrenDoneToday.Should().Be(2);
        igiene.ChildrenTotal.Should().Be(2);
    }
}
