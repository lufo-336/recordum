using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Abstractions.Services;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Detail;
using Recordum.UI.Controls;
using Recordum.UI.Converters;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>
/// YearHeatmapControl e StreakTimelineControl. Le prove sono STRUTTURALI (quante forme, dove,
/// di che colore): un PNG vuoto pesa comunque, ed è così che la sonda Live Preview ha prodotto
/// una schermata vuota con un test verde. Gli screenshot restano, per GUARDARE.
/// </summary>
public class ControlsScreenshotTests
{
    private static readonly DateOnly RangeFrom = new(2026, 1, 1);
    private static readonly DateOnly RangeTo = new(2026, 3, 31);

    private static StreakTimelineViewModel NewTimeline()
    {
        // Redraw() legge SOLO RangeFrom, RangeTo e Runs. Le due dipendenze del costruttore servono
        // a LoadAsync, che qui non si chiama: null! è deliberato.
        var vm = new StreakTimelineViewModel(null!, null!) { RangeFrom = RangeFrom, RangeTo = RangeTo };
        vm.Runs.Add(new StreakRun(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 12), 8));
        vm.Runs.Add(new StreakRun(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 3), 3));
        vm.Runs.Add(new StreakRun(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 20), 11));
        return vm;
    }

    // Come il VM vero: 53 colonne x 7 righe = 371 celle; qui l'intensità cicla 0..4 così ogni
    // step compare molte volte. Il VM vero le riempie da LoadAsync (5 servizi), che qui non serve.
    private static YearHeatmapViewModel NewYear()
    {
        var vm = new YearHeatmapViewModel(null!, null!, null!, null!, null!);
        var anchor = new DateOnly(2025, 9, 15);
        for (var i = 0; i < 53 * 7; i++)
            vm.Cells.Add(new HeatmapCellViewModel(anchor.AddDays(i), i % 5, i % 5));
        return vm;
    }

    private static void Pump()
    {
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    // ── Timeline ────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Streak_timeline_draws_one_rectangle_per_run_at_proportional_positions()
    {
        var vm = NewTimeline();
        var sut = new StreakTimelineControl { DataContext = vm, Width = 400 };
        var window = new Window { Content = sut, Width = 420, Height = 60 };
        window.Show();
        Pump();

        var canvas = sut.GetVisualDescendants().OfType<Canvas>().Single();
        var rects = canvas.Children.OfType<Rectangle>().ToList();
        rects.Should().HaveCount(3);

        // 90 giorni nel range (gen 31 + feb 28 + mar 31), Width 400 → dayWidth = 400/90.
        var dayWidth = 400d / 90;
        Canvas.GetLeft(rects[0]).Should().BeApproximately(4 * dayWidth, 0.01); // 5 gen = giorno 4
        rects[0].Width.Should().BeApproximately(8 * dayWidth, 0.01);
        Canvas.GetLeft(rects[1]).Should().BeApproximately(31 * dayWidth, 0.01); // 1 feb = giorno 31
        rects[1].Width.Should().BeApproximately(3 * dayWidth, 0.01);
        Canvas.GetLeft(rects[2]).Should().BeApproximately(68 * dayWidth, 0.01); // 10 mar = giorno 68
        rects.Should().OnlyContain(r => r.Height == 16 && Canvas.GetTop(r) == 4);
        ScreenshotProbeTests.Capture(window, "streak-timeline-light");
    }

    [AvaloniaFact]
    public void Streak_timeline_fills_bars_with_the_accent_of_the_current_theme_variant()
    {
        var sut = new StreakTimelineControl { DataContext = NewTimeline(), Width = 400 };
        var window = new Window { Content = sut, Width = 420, Height = 60, RequestedThemeVariant = ThemeVariant.Dark };
        window.Show();
        Pump();

        var fill = sut.GetVisualDescendants().OfType<Rectangle>().First().Fill;
        fill.Should().BeAssignableTo<ISolidColorBrush>()
            .Which.Color.Should().Be(Color.Parse("#E6E5E1"), "Brush.Accent scuro, non il #1F1F1D chiaro");
        ScreenshotProbeTests.Capture(window, "streak-timeline-dark");
    }

    [AvaloniaFact]
    public void Streak_timeline_redraws_with_the_new_accent_when_the_theme_variant_flips_after_first_draw()
    {
        var sut = new StreakTimelineControl { DataContext = NewTimeline(), Width = 400 };
        var window = new Window { Content = sut, Width = 420, Height = 60, RequestedThemeVariant = ThemeVariant.Light };
        window.Show();
        Pump();

        Color FillColor() => ((ISolidColorBrush)sut.GetVisualDescendants().OfType<Rectangle>().First().Fill!).Color;
        FillColor().Should().Be(Color.Parse("#1F1F1D"));

        window.RequestedThemeVariant = ThemeVariant.Dark; // DOPO il primo disegno
        Pump();

        FillColor().Should().Be(Color.Parse("#E6E5E1"));
    }

    // ── Year heatmap ────────────────────────────────────────────────────────

    private static (YearHeatmapControl Sut, Window Window) ShowYear(ThemeVariant variant)
    {
        // Il converter risolve contro la variante dell'APPLICAZIONE (v. IntensityStepToBrushConverter),
        // quindi è lì che il test la imposta; il chiamante ripristina in finally.
        Application.Current!.RequestedThemeVariant = variant;
        var sut = new YearHeatmapControl { DataContext = NewYear() };
        var window = new Window { Content = sut, Width = 760, Height = 140, RequestedThemeVariant = variant };
        window.Show();
        Pump();
        return (sut, window);
    }

    private static List<Border> Cells(YearHeatmapControl sut)
        => sut.GetVisualDescendants().OfType<Border>().Where(b => b.Width == 11 && b.Height == 11).ToList();

    [AvaloniaTheory]
    [InlineData("Light", new[] { "#EFEFF1", "#9BE9A8", "#40C463", "#30A14E", "#216E39" })]
    [InlineData("Dark", new[] { "#27272A", "#0E4429", "#006D32", "#26A641", "#39D353" })]
    public void Year_heatmap_realizes_371_cells_painted_with_five_distinct_theme_brushes(string variantName, string[] ramp)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        try
        {
            var (sut, window) = ShowYear(variant);

            var cells = Cells(sut);
            cells.Should().HaveCount(371);

            var colors = cells.Select(c => c.Background).OfType<ISolidColorBrush>().Select(b => b.Color).ToList();
            colors.Should().HaveCount(371, "ogni cella deve avere un pennello pieno, non Transparent/null");
            colors.Distinct().Should().HaveCount(5);
            for (var step = 0; step < 5; step++)
            {
                cells.Where((_, i) => i % 5 == step)
                    .Select(c => ((ISolidColorBrush)c.Background!).Color)
                    .Distinct().Should().ContainSingle().Which.Should().Be(Color.Parse(ramp[step]));
            }
            ScreenshotProbeTests.Capture(window, $"year-heatmap-{variantName.ToLowerInvariant()}");
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }
    }

    [AvaloniaFact]
    public void Year_heatmap_grid_is_7_rows_by_53_columns_and_fills_ROW_MAJOR()
    {
        // Documenta la geometria e il riempimento per righe (come nell'originale WPF: UniformGrid è
        // row-major, quindi un anno vero si legge come 7 righe di giorni consecutivi, NON come
        // colonne-settimana stile GitHub — difetto pre-esistente, registrato a parte). Se un giorno
        // il riempimento diventa per colonne, le ultime due asserzioni falliscono: si cambia il test
        // sapendolo, insieme al layout.
        var (sut, _) = ShowYear(ThemeVariant.Light);
        try
        {
            var cells = Cells(sut); // ordine di ItemsControl = ordine di Cells nel VM
            cells.Should().HaveCount(371);

            // Bounds è relativo al ContentPresenter di ogni item: le posizioni vere si prendono
            // rispetto al controllo.
            var pos = cells.Select(c => c.TranslatePoint(new Point(0, 0), sut)!.Value).ToList();

            pos.Select(p => p.X).Distinct().Should().HaveCount(53, "53 colonne");
            pos.Select(p => p.Y).Distinct().Should().HaveCount(7, "7 righe");

            // Row-major: la cella 1 sta a destra della 0 sulla stessa riga; la 53 va a capo, stessa colonna della 0.
            pos[1].X.Should().BeGreaterThan(pos[0].X);
            pos[1].Y.Should().Be(pos[0].Y);
            pos[53].X.Should().Be(pos[0].X);
            pos[53].Y.Should().BeGreaterThan(pos[0].Y);
        }
        finally
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }
    }

    // ── Converter ───────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void Converter_maps_steps_0_to_4_to_five_distinct_brushes_and_everything_else_to_level0()
    {
        var conv = new IntensityStepToBrushConverter();
        object Get(object? v) => conv.Convert(v, typeof(IBrush), null, System.Globalization.CultureInfo.InvariantCulture);

        var brushes = Enumerable.Range(0, 5).Select(i => Get(i)).ToList();
        brushes.Should().AllBeAssignableTo<ISolidColorBrush>();
        brushes.Cast<ISolidColorBrush>().Select(b => b.Color).Distinct().Should().HaveCount(5);

        var level0 = ((ISolidColorBrush)brushes[0]).Color;
        foreach (var other in new object?[] { -1, 5, 99, null, "3", 2.0 })
            ((ISolidColorBrush)Get(other)).Color.Should().Be(level0);
    }
}
