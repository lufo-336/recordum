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

    [Fact]
    public async Task An_empty_group_shows_no_progress()
    {
        // Review M-2: "0 di 0" su un gruppo vuoto non dice niente.
        var sut = await Loaded(Wed1000, Row("Vuoto", type: null));
        sut.Rows.Single().ShowGroupProgress.Should().BeFalse();
    }

    [Fact]
    public async Task A_group_with_habits_shows_its_progress()
    {
        var group = Row("Mattina", type: null);
        var sut = await Loaded(Wed1000, group, Row("Acqua", group.HabitNodeId));
        sut.Rows.Single(r => r.IsGroup).ShowGroupProgress.Should().BeTrue();
    }

    [Fact]
    public async Task A_failing_day_boundary_leaves_the_rows_and_hides_the_header()
    {
        // Review M-6: l'intestazione è un di più: se il confine del giorno non si legge, la Home mostra le righe lo stesso.
        var query = new FakeHabitTreeQueryService();
        query.HomeRows.Add(Row("a"));
        var sut = new HomeViewModel(query, new FakeHabitNodeService(), new FakePurgeService(),
            clock: new FakeClock(Wed1000), resetTimes: new ThrowingResolver());
        await sut.LoadCommand.ExecuteAsync(null);
        sut.State.Should().Be(HomeState.Loaded);
        sut.Rows.Should().ContainSingle();
        sut.ShowStripHeader.Should().BeFalse();
    }

    [Fact]
    public async Task The_header_shows_only_with_loaded_rows()
    {
        var sut = await Loaded(Wed1000, Row("a"));
        sut.ShowStripHeader.Should().BeTrue();
        sut.State = HomeState.Error;
        sut.ShowStripHeader.Should().BeFalse("in errore o in caricamento l'intestazione non resta sola");
    }

    private sealed class ThrowingResolver : Recordum.Core.Abstractions.Services.IResetTimeResolver
    {
        public ValueTask<TimeOnly> GetEffectiveResetTimeAsync(Guid habitNodeId, CancellationToken ct = default) => throw new InvalidOperationException("db");
        public ValueTask<DayBoundary> GetDayBoundaryAsync(Guid habitNodeId, CancellationToken ct = default) => throw new InvalidOperationException("db");
        public ValueTask<IReadOnlyDictionary<Guid, DayBoundary>> GetDayBoundariesAsync(IReadOnlyCollection<Guid> habitNodeIds, CancellationToken ct = default) => throw new InvalidOperationException("db");
        public ValueTask<DayBoundary> GetAppDayBoundaryAsync(CancellationToken ct = default) => throw new InvalidOperationException("db");
    }
}
