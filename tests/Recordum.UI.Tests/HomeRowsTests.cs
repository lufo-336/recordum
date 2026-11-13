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
        cells.Should().HaveCount(7).And.OnlyContain(t => t.Bounds.Width == 26);
        HeaderDays(view).Last().Content.Should().Be("oggi");
        HeaderDays(view).Should().OnlyContain(b => b.Bounds.Width == 26 && b.Bounds.Height == 22);
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
        Strip(row).GetVisualDescendants().OfType<TextBlock>().Should().HaveCount(7).And.OnlyContain(t => t.Bounds.Width == 26);

        var days = HeaderDays(view);
        days.Should().HaveCount(7);
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
            outline.BorderThickness.Should().Be(new Thickness(2));
            ((ISolidColorBrush)outline.BorderBrush!).Color.Should().Be(Token<ISolidColorBrush>("Brush.Ink").Color);
            var around = BoundsIn(squares[^1], view).Inflate(3);
            BoundsIn(outline, view).Should().Be(around, "il contorno sta 1 px fuori dal quadratino di oggi, spesso 2");
        }

        // Il giorno scelto si sposta: il contorno lo segue.
        vm.SelectDayCommand.Execute(2);
        Pump();
        var first = Strip(HabitRows(view)[0]);
        var moved = first.GetVisualDescendants().OfType<Border>().Single(b => b.Name == HeatmapStrip.SelectedCellName);
        BoundsIn(moved, view).Center.X.Should().BeApproximately(BoundsIn(Squares(first)[^3], view).Center.X, 0.01);
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
        var empty = Token<ISolidColorBrush>("Brush.Border").Color;
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
