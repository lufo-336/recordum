using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Presentation.Services;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Home;
using Recordum.Presentation.ViewModels.Quick;
using Recordum.UI.Controls;
using Recordum.UI.Converters;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Tests.Services;
using Recordum.UI.Theming;
using Recordum.UI.Views;
using Recordum.UI.Views.Home;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>Le righe della Home in tre versioni (onda Giornata, Task 9): Larga è la Home di prima, Compatta abbassa le
/// righe e scrive le iniziali, Segni disegna la settimana a quadratini. Le misure stanno in <see cref="HomeRowsMetrics"/>.</summary>
public class HomeRowsTests
{
    private const string Blue = "#2F7FC1";
    private static readonly Guid GroupId = Guid.NewGuid();

    private static HomeTreeRow Row(string name, HabitType? type = HabitType.Action, Guid? parent = null, int days = 14,
        decimal?[]? values = null)
    {
        var counts = values is null
            ? Enumerable.Range(0, days).Select(i => i % 2).ToArray()
            : values.Select(v => v is null ? 0 : 1).ToArray();
        return new HomeTreeRow(Guid.NewGuid(), parent, name, Blue, type, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(counts[^Math.Min(7, counts.Length)..]), ImmutableArray<Guid>.Empty,
            CompletionsLast14Days: ImmutableArray.Create(counts),
            ValuesLast14Days: values is null ? default : ImmutableArray.Create(values));
    }

    private static (Window W, HomeView V, HomeViewModel Vm, HomeRowsPreference Pref) Show(double width, HomeRows style, params HomeTreeRow[] rows)
    {
        var query = new FakeHabitTreeQueryService();
        query.Rows.AddRange(rows);
        var pref = new HomeRowsPreference(new FakeAppSettingService(), style);
        var vm = new HomeViewModel(query, new FakeHabitNodeService(), new FakePurgeService(),
            quickActions: new QuickActionHandlerSelector([]),
            clock: new PrivateDbHost.FakeClock { Now = new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero) },
            rows: pref);
        var view = new HomeView { DataContext = vm };
        var w = new Window { Width = width, Height = 600, Content = view };
        w.Show();
        vm.LoadCommand.Execute(null);
        Pump();
        return (w, view, vm, pref);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        Dispatcher.UIThread.RunJobs();
    }

    private static Rect BoundsIn(Visual v, Visual root) =>
        new(v.TranslatePoint(new Point(0, 0), root)!.Value, v.Bounds.Size);

    private static List<Button> HeaderDays(HomeView view) =>
        view.FindControl<DayHeaderView>("DayHeader")!.GetVisualDescendants().OfType<Button>().ToList();

    private static HeatmapStrip Strip(HabitRow row) => row.GetVisualDescendants().OfType<HeatmapStrip>().Single();

    private static List<HabitRow> HabitRows(HomeView view) => view.GetVisualDescendants().OfType<HabitRow>().ToList();

    /// <summary>I quadratini di Segni: 14×14, raggio 4. Il contorno del giorno scelto è più largo e non è fra questi.</summary>
    private static List<Border> Squares(HeatmapStrip strip) =>
        strip.GetVisualDescendants().OfType<Border>().Where(b => b.Width == 14 && b.Height == 14).ToList();

    /// <summary>Le colonne di una striscia, dal giorno più vecchio a oggi: il testo dei valori, o i quadratini di Segni.</summary>
    private static List<Control> Columns(HeatmapStrip strip)
    {
        var squares = Squares(strip);
        return squares.Count > 0 ? [.. squares] : [.. strip.GetVisualDescendants().OfType<TextBlock>()];
    }

    private static Color Fill(Border b) => ((ISolidColorBrush)b.Background!).Color;

    private static T Token<T>(string key) where T : class =>
        HabitTextBrushConverter.Resource<T>(Application.Current!, key, ThemeVariant.Light)!;

    [AvaloniaFact]
    public void Comfortable_keeps_today_measures()
    {
        using var culture = TestCulture.Pin();
        var (w, view, vm, _) = Show(900, HomeRows.Comfortable,
            Row("Acqua"), Row("Gruppo", type: null) with { HabitNodeId = GroupId }, Row("Figlia", parent: GroupId));

        vm.RowsStyle.Should().Be(HomeRows.Comfortable);
        var row = HabitRows(view)[0];
        row.FindControl<Grid>("MenuHost")!.Bounds.Height.Should().Be(36);
        view.GetVisualDescendants().OfType<GroupRow>().Single().FindControl<Grid>("MenuHost")!.Bounds.Height.Should().Be(36);
        var quick = row.FindControl<Button>("QuickLog")!;
        (quick.Bounds.Width, quick.Bounds.Height).Should().Be((28, 26));
        var cells = Strip(row).GetVisualDescendants().OfType<TextBlock>().ToList();
        cells.Should().HaveCount(14).And.OnlyContain(t => t.Bounds.Width == 26);
        HeaderDays(view).Last().Content.Should().Be("oggi");
        HeaderDays(view).Should().OnlyContain(b => b.Bounds.Width == 26 && b.Bounds.Height == (b.Classes.Contains("selected") ? 18 : 22));
        w.Close();
    }

    [AvaloniaFact]
    public void Compact_rows_are_30_and_header_shows_initials()
    {
        using var culture = TestCulture.Pin();
        var (w, view, _, _) = Show(900, HomeRows.Compact,
            Row("Acqua"), Row("Gruppo", type: null) with { HabitNodeId = GroupId }, Row("Figlia", parent: GroupId));

        var row = HabitRows(view)[0];
        row.FindControl<Grid>("MenuHost")!.Bounds.Height.Should().Be(30);
        view.GetVisualDescendants().OfType<GroupRow>().Single().FindControl<Grid>("MenuHost")!.Bounds.Height.Should().Be(30);
        var quick = row.FindControl<Button>("QuickLog")!;
        (quick.Bounds.Width, quick.Bounds.Height).Should().Be((24, 22));
        Strip(row).GetVisualDescendants().OfType<TextBlock>().Should().HaveCount(14).And.OnlyContain(t => t.Bounds.Width == 26);

        var days = HeaderDays(view);
        days.Should().HaveCount(14);
        days.Should().OnlyContain(b => ((string)b.Content!).Length == 1, "le iniziali, non \"mer\" e \"oggi\"");
        // Mercoledì 28 ottobre 2026.
        days.Last().Content.Should().Be("m");
        days.Last().Classes.Should().Contain("selected");
        w.Close();
    }

    [AvaloniaFact]
    public void Marks_draws_squares_and_no_text()
    {
        using var culture = TestCulture.Pin();
        var (w, view, vm, _) = Show(460, HomeRows.Marks, Row("Sì o no"), Row("Astinenza", HabitType.Resistance));

        foreach (var row in HabitRows(view))
        {
            var strip = Strip(row);
            strip.GetVisualDescendants().OfType<TextBlock>().Should().BeEmpty("in Segni la striscia non scrive");
            var squares = Squares(strip);
            squares.Should().HaveCount(7);
            squares.Should().OnlyContain(b => b.CornerRadius == new CornerRadius(4) && b.Bounds.Width == 14 && b.Bounds.Height == 14);
            // Colonne da 14 con 3 di spazio.
            for (var i = 1; i < squares.Count; i++)
                (BoundsIn(squares[i], view).Left - BoundsIn(squares[i - 1], view).Left).Should().BeApproximately(17, 0.01);

            var outline = strip.GetVisualDescendants().OfType<Border>().Single(b => b.Name == HeatmapStrip.SelectedCellName);
            // GIO-16 (scelta A2 dell'autore): sottile e grigio, non 2 px d'inchiostro.
            outline.BorderThickness.Should().Be(new Thickness(1));
            ((ISolidColorBrush)outline.BorderBrush!).Color.Should().Be(Token<ISolidColorBrush>("Brush.TextMuted").Color);
            var around = BoundsIn(squares[^1], view).Inflate(2);
            BoundsIn(outline, view).Should().Be(around, "il contorno sta 1 px fuori dal quadratino di oggi, spesso 1");
        }

        // Il giorno scelto si sposta: il contorno lo segue.
        vm.SelectDayCommand.Execute(2);
        Pump();
        var first = Strip(HabitRows(view)[0]);
        var moved = first.GetVisualDescendants().OfType<Border>().Single(b => b.Name == HeatmapStrip.SelectedCellName);
        BoundsIn(moved, view).Center.X.Should().BeApproximately(BoundsIn(Squares(first)[^3], view).Center.X, 0.01);
        w.Close();
    }

    private static HomeTreeRow Lapsed() => Row("Astinenza", HabitType.Resistance) with
    {
        CompletionsLast14Days = ImmutableArray.Create(4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 0, 4),
        LapsesLast14Days = ImmutableArray.Create(Enumerable.Range(0, 14).Select(i => i == 12).ToArray()),
    };

    /// <summary>GIO-17 (scelta B3): il giorno scelto in testa è un contorno d'inchiostro, non una pillola piena.</summary>
    [AvaloniaFact]
    public void The_selected_header_day_is_an_outline()
    {
        using var culture = TestCulture.Pin();
        var (w, view, _, _) = Show(900, HomeRows.Comfortable, Row("Acqua"));
        var selected = HeaderDays(view).Last();
        selected.Classes.Should().Contain("selected");
        ((ISolidColorBrush)selected.Background!).Color.A.Should().Be(0, "niente riempimento");
        selected.BorderThickness.Should().Be(new Thickness(1));
        ((ISolidColorBrush)selected.BorderBrush!).Color.Should().Be(Token<ISolidColorBrush>("Brush.Ink").Color);
        ((ISolidColorBrush)selected.Foreground!).Color.Should().Be(Token<ISolidColorBrush>("Brush.Text").Color);
        selected.Bounds.Height.Should().Be(18);
        w.Close();
    }

    /// <summary>GIO-18 (scelta C2 per Segni): i giorni puliti di fila si fondono in un nastro, che la ricaduta strappa.</summary>
    [AvaloniaFact]
    public void An_abstinence_in_Marks_is_a_band_torn_by_the_lapse()
    {
        using var culture = TestCulture.Pin();
        var (w, view, _, _) = Show(900, HomeRows.Marks, Lapsed(), Row("Sì o no"));
        var strip = Strip(HabitRows(view)[0]);
        var squares = Squares(strip);
        squares.Should().HaveCount(14);
        squares[0].CornerRadius.Should().Be(new CornerRadius(4, 0, 0, 4), "il nastro comincia");
        squares[5].CornerRadius.Should().Be(new CornerRadius(0), "in mezzo al nastro");
        squares[11].CornerRadius.Should().Be(new CornerRadius(0, 4, 4, 0), "il nastro finisce prima della ricaduta");
        squares[12].CornerRadius.Should().Be(new CornerRadius(4), "la ricaduta resta un quadratino");
        squares[13].CornerRadius.Should().Be(new CornerRadius(4), "oggi ricomincia da solo");

        var bridges = strip.GetVisualDescendants().OfType<Border>().Where(b => b.Name == HeatmapStrip.BandBridgeName).ToList();
        bridges.Should().HaveCount(11, "undici spazi fra dodici giorni di fila");
        for (var i = 0; i < 11; i++)
        {
            var bridge = BoundsIn(bridges[i], view);
            bridge.Left.Should().BeApproximately(BoundsIn(squares[i], view).Right, 0.01);
            bridge.Right.Should().BeApproximately(BoundsIn(squares[i + 1], view).Left, 0.01);
            Fill(bridges[i]).Should().Be(Fill(squares[i]));
        }

        var action = Strip(HabitRows(view)[1]);
        action.GetVisualDescendants().OfType<Border>().Should().NotContain(b => b.Name == HeatmapStrip.BandBridgeName, "solo l'astinenza è un nastro");
        Squares(action).Should().OnlyContain(b => b.CornerRadius == new CornerRadius(4));
        w.Close();
    }

    /// <summary>GIO-18 (scelta C1 per Larga e Compatta): una linea sotto le spunte, che la ricaduta interrompe.</summary>
    [AvaloniaTheory]
    [InlineData(HomeRows.Comfortable)]
    [InlineData(HomeRows.Compact)]
    public void An_abstinence_with_checks_has_a_line_under_them(HomeRows style)
    {
        using var culture = TestCulture.Pin();
        var (w, view, _, _) = Show(900, style, Lapsed(), Row("Sì o no"));
        var strip = Strip(HabitRows(view)[0]);
        var texts = strip.GetVisualDescendants().OfType<TextBlock>().ToList();
        var lines = strip.GetVisualDescendants().OfType<Border>().Where(b => b.Name == HeatmapStrip.BandLineName).ToList();
        lines.Should().HaveCount(13, "dodici giorni puliti e oggi; sotto la ricaduta niente");
        var lapse = BoundsIn(texts[12], view);
        lines.Should().NotContain(l => BoundsIn(l, view).Left < lapse.Right && BoundsIn(l, view).Right > lapse.Left);
        for (var i = 0; i < 11; i++)
            BoundsIn(lines[i], view).Right.Should().BeApproximately(BoundsIn(lines[i + 1], view).Left, 0.01, "la linea è continua");
        BoundsIn(lines[11], view).Right.Should().BeLessThan(BoundsIn(texts[11], view).Right, "prima della ricaduta la linea si ferma dentro la cella");
        BoundsIn(lines[12], view).Left.Should().BeGreaterThan(BoundsIn(texts[13], view).Left, "e dopo riparte dentro la cella");
        foreach (var line in lines)
        {
            line.Bounds.Height.Should().Be(2);
            BoundsIn(line, view).Top.Should().BeGreaterThan(BoundsIn(texts[0], view).Top + 14, "sta sotto la spunta");
            Fill(line).Should().Be(((ISolidColorBrush)texts[0].Foreground!).Color, "nel colore delle spunte");
        }
        Strip(HabitRows(view)[1]).GetVisualDescendants().OfType<Border>().Should().NotContain(b => b.Name == HeatmapStrip.BandLineName);
        w.Close();
    }

    [AvaloniaTheory]
    [InlineData(HomeRows.Marks)]
    [InlineData(HomeRows.Comfortable)]
    public void The_day_of_a_lapse_is_a_faint_cross(HomeRows style)
    {
        // GIO-9: ieri una ricaduta. Segni: quadratino vuoto con la × dentro. Larga: una × nel testo attenuato.
        using var culture = TestCulture.Pin();
        var lapsed = Lapsed();
        var (w, view, _, _) = Show(900, style, lapsed);
        var strip = Strip(HabitRows(view)[0]);

        if (style == HomeRows.Marks)
        {
            var squares = Squares(strip);
            var mark = strip.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(p => p.Name == HeatmapStrip.LapseMarkName);
            mark.Parent.Should().BeSameAs(squares[^2]);
            Fill(squares[^2]).Should().Be(Token<ISolidColorBrush>("Brush.CellEmpty").Color);
            mark.Bounds.Width.Should().BeGreaterThan(4).And.BeLessThan(14);
            ((ISolidColorBrush)mark.Stroke!).Color.Should().Be(Token<ISolidColorBrush>("Brush.TextMuted").Color);
            strip.GetVisualDescendants().OfType<TextBlock>().Should().BeEmpty("in Segni la striscia non scrive");
        }
        else
        {
            var texts = strip.GetVisualDescendants().OfType<TextBlock>().ToList();
            texts[^2].Text.Should().Be("×");
            // GIO-25: un gradino più scura del testo tenue dei giorni vuoti, come la × di Segni; nel tenue era un puntino.
            ((ISolidColorBrush)texts[^2].Foreground!).Color.Should().Be(Token<ISolidColorBrush>("Brush.TextMuted").Color);
            texts[^3].Text.Should().Be("✓");
        }
        ScreenshotProbeTests.Capture(w, $"home-ricaduta-{style.ToString().ToLowerInvariant()}-light");
        w.Close();
    }

    [AvaloniaFact]
    public void Marks_fill_follows_the_value_in_three_steps()
    {
        using var culture = TestCulture.Pin();
        // Massimo 10 nella settimana: 10 → pieno, 5 → 65 %, 2 → 35 %, niente → vuoto, 7 → pieno (oltre i due terzi).
        var counter = Row("Pagine", HabitType.Counter,
            values: [null, null, null, null, null, null, null, 10, 5, 2, null, 10, 4, 7]);
        var (w, view, _, _) = Show(460, HomeRows.Marks, counter, Row("Sì o no"));

        var habit = Color.Parse(Blue);
        var pane = Token<ISolidColorBrush>("Brush.Pane").Color;
        var empty = Token<ISolidColorBrush>("Brush.CellEmpty").Color;
        var fills = Squares(Strip(HabitRows(view)[0])).Select(Fill).ToList();
        fills.Should().Equal(
            habit, HabitColors.Blend(habit, 0.65, pane), HabitColors.Blend(habit, 0.35, pane), empty,
            habit, HabitColors.Blend(habit, 0.65, pane), habit);

        // Un tipo sì/no è sempre pieno o vuoto.
        Squares(Strip(HabitRows(view)[1])).Select(Fill).Should().OnlyContain(c => c == habit || c == empty)
            .And.Contain(habit).And.Contain(empty);
        w.Close();
    }

    [AvaloniaTheory]
    [InlineData(HomeRows.Comfortable, 900, 14)]
    [InlineData(HomeRows.Comfortable, 360, 5)]
    [InlineData(HomeRows.Compact, 900, 14)]
    [InlineData(HomeRows.Compact, 360, 5)]
    [InlineData(HomeRows.Marks, 900, 14)]
    [InlineData(HomeRows.Marks, 360, 7)]
    public void Header_sits_over_columns_in_every_style(HomeRows style, int width, int columns)
    {
        using var culture = TestCulture.Pin();
        var (w, view, _, _) = Show(width, style, Row("Quattordici", HabitType.Resistance),
            Row("Gruppo", type: null) with { HabitNodeId = GroupId }, Row("Figlia", parent: GroupId));

        var days = HeaderDays(view);
        var cells = Columns(Strip(HabitRows(view).Single(r => ((HomeRowViewModel)r.DataContext!).Name == "Quattordici")));
        days.Should().HaveCount(columns);
        cells.Should().HaveCount(columns);
        for (var i = 0; i < columns; i++)
            BoundsIn(days[i], view).Center.X.Should().BeApproximately(BoundsIn(cells[i], view).Center.X, 1.0, $"colonna {i}");
        BoundsIn(days[^1], view).Right.Should().BeLessThanOrEqualTo(width + 0.5);
        w.Close();
    }

    [AvaloniaTheory]
    [InlineData(HomeRows.Comfortable, 900, 14)]
    [InlineData(HomeRows.Comfortable, 640, 14)]
    [InlineData(HomeRows.Comfortable, 639, 7)]
    [InlineData(HomeRows.Comfortable, 400, 7)]
    [InlineData(HomeRows.Comfortable, 399, 5)]
    [InlineData(HomeRows.Comfortable, 340, 5)]
    [InlineData(HomeRows.Comfortable, 339, 4)]
    [InlineData(HomeRows.Compact, 640, 14)]
    [InlineData(HomeRows.Compact, 399, 5)]
    [InlineData(HomeRows.Compact, 280, 4)]
    [InlineData(HomeRows.Marks, 640, 14)]
    [InlineData(HomeRows.Marks, 639, 7)]
    [InlineData(HomeRows.Marks, 399, 7)]
    [InlineData(HomeRows.Marks, 280, 7)]
    public void Visible_days_table(HomeRows style, double width, int days) =>
        HomeRowsMetrics.VisibleDays(style, width).Should().Be(days);

    [AvaloniaFact]
    public void Metrics_table()
    {
        HomeRowsMetrics.For(HomeRows.Comfortable).Should().Be(new HomeRowsMetrics(36, 28, 26, 0, HeaderInitials: false, Marks: false));
        HomeRowsMetrics.For(HomeRows.Compact).Should().Be(new HomeRowsMetrics(30, 24, 26, 0, HeaderInitials: true, Marks: false));
        HomeRowsMetrics.For(HomeRows.Marks).Should().Be(new HomeRowsMetrics(30, 24, 14, 3, HeaderInitials: true, Marks: true));
        // Un valore fuori dai tre (un cast da un file di un'altra versione) si disegna come Larga, senza eccezioni.
        HomeRowsMetrics.For((HomeRows)42).Should().Be(HomeRowsMetrics.For(HomeRows.Comfortable));
    }

    [AvaloniaFact]
    public async Task Changing_style_redraws_open_home()
    {
        // Review Focus 5: un cambio dalle Impostazioni con la Home aperta si vede subito, senza riavvio.
        using var culture = TestCulture.Pin();
        var (w, view, vm, pref) = Show(900, HomeRows.Comfortable, Row("Acqua"), Row("Astinenza", HabitType.Resistance));
        HabitRows(view)[0].FindControl<Grid>("MenuHost")!.Bounds.Height.Should().Be(36);

        await pref.SetAsync(HomeRows.Marks);
        Pump();

        vm.RowsStyle.Should().Be(HomeRows.Marks);
        foreach (var row in HabitRows(view))
        {
            row.FindControl<Grid>("MenuHost")!.Bounds.Height.Should().Be(30);
            Strip(row).GetVisualDescendants().OfType<TextBlock>().Should().BeEmpty();
        }
        var days = HeaderDays(view);
        days.Should().HaveCount(14).And.OnlyContain(b => b.Bounds.Width == 14);
        var squares = Squares(Strip(HabitRows(view)[1]));
        squares.Should().HaveCount(14);
        for (var i = 0; i < 14; i++)
            BoundsIn(days[i], view).Center.X.Should().BeApproximately(BoundsIn(squares[i], view).Center.X, 1.0, $"colonna {i}");

        // E ritorno: Larga è di nuovo la Home di prima.
        await pref.SetAsync(HomeRows.Comfortable);
        Pump();
        HabitRows(view)[0].FindControl<Grid>("MenuHost")!.Bounds.Height.Should().Be(36);
        HeaderDays(view).Last().Content.Should().Be("oggi");
        Strip(HabitRows(view)[1]).GetVisualDescendants().OfType<TextBlock>().Should().HaveCount(14);
        w.Close();
    }

    [AvaloniaFact]
    public void Marks_keeps_seven_days_when_narrow_and_the_day_can_go_back_six()
    {
        using var culture = TestCulture.Pin();
        var (w, view, vm, _) = Show(300, HomeRows.Marks, Row("Astinenza", HabitType.Resistance));

        HeaderDays(view).Should().HaveCount(7);
        vm.MaxDaysBack.Should().Be(6);
        BoundsIn(HeaderDays(view)[0], view).Left.Should().BeGreaterThan(0);
        w.Close();
    }
}
