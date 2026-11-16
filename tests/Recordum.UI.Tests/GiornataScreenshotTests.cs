using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Home;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Home;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>I PNG dell'onda Giornata sulla Home piena (fixture <see cref="ManifestHome"/>). Un PNG da solo non prova
/// niente: ogni schermata ha le sue asserzioni strutturali.</summary>
public class GiornataScreenshotTests
{
    private static void Pump()
    {
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        Dispatcher.UIThread.RunJobs();
    }

    private static Rect BoundsIn(Visual v, Visual root) =>
        new(v.TranslatePoint(new Point(0, 0), root)!.Value, v.Bounds.Size);

    private static (Window W, HomeView V, HomeViewModel Vm, IReadOnlyList<Recordum.Core.Abstractions.Services.Dtos.HomeTreeRow> Rows) ShowManifest(string theme, int width, HomeRows? style = null)
    {
        var rows = ManifestHome.Rows(ManifestHome.Today);
        var vm = ManifestHome.Home(rows, style);
        var view = new HomeView { DataContext = vm };
        var w = new Window
        {
            Width = width, Height = 700, Content = view,
            RequestedThemeVariant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        w.Show();
        vm.LoadCommand.Execute(null);
        Pump();
        return (w, view, vm, rows);
    }

    [AvaloniaTheory]
    [InlineData("light", 900)]
    [InlineData("light", 360)]
    [InlineData("dark", 900)]
    [InlineData("dark", 360)]
    public void Home_full(string theme, int width)
    {
        using var culture = TestCulture.Pin();
        var (w, view, _, rows) = ShowManifest(theme, width);

        var habitRows = view.GetVisualDescendants().OfType<HabitRow>().ToList();
        habitRows.Should().HaveCount(9, "nove abitudini");
        view.GetVisualDescendants().OfType<GroupRow>().Should().ContainSingle("la Routine del mattino");
        view.FindControl<TextBlock>("DayCount")!.Text.Should().Contain("4 di 9");

        var header = view.FindControl<DayHeaderView>("DayHeader")!;
        header.IsVisible.Should().BeTrue("l'intestazione dei giorni si vede");
        header.Bounds.Width.Should().BeGreaterThan(0);
        var headerToday = header.GetVisualDescendants().OfType<Button>().Last();
        headerToday.Content.Should().Be("oggi");
        headerToday.Classes.Should().Contain("selected", "su oggi il giorno scelto è oggi");
        var column = BoundsIn(headerToday, view);

        // La prima striscia di una riga di primo livello: sta sotto "oggi" entro 1 px (il centro della cella).
        var topLevelIds = rows.Where(r => r.ParentId is null).Select(r => r.HabitNodeId).ToHashSet();
        var firstTop = habitRows.First(r => r.DataContext is HomeRowViewModel h && topLevelIds.Contains(h.HabitNodeId));
        var lastCell = BoundsIn(firstTop.GetVisualDescendants().OfType<HeatmapStrip>().Single()
            .GetVisualDescendants().OfType<TextBlock>().Last(), view);
        lastCell.Center.X.Should().BeApproximately(column.Center.X, 1.0, "la colonna di oggi sta sopra l'ultima cella");

        // Le figlie del gruppo sono rientrate (cornice del gruppo): la loro ultima cella resta dentro la colonna di oggi.
        var firstStrip = habitRows[0].GetVisualDescendants().OfType<HeatmapStrip>().Single();
        BoundsIn(firstStrip.GetVisualDescendants().OfType<TextBlock>().Last(), view).Center.X
            .Should().BeInRange(column.Left, column.Right);

        ScreenshotProbeTests.Capture(w, $"home-manifesto-{theme}-{width}");
        w.Close();
    }

    /// <summary>Un giorno passato: titolo col giorno, la fascia d'avviso, la colonna di ieri marcata su ogni riga.</summary>
    [AvaloniaTheory]
    [InlineData("light", 900)]
    [InlineData("light", 360)]
    [InlineData("dark", 900)]
    [InlineData("dark", 360)]
    public void Home_on_yesterday(string theme, int width)
    {
        using var culture = TestCulture.Pin();
        var (w, view, vm, _) = ShowManifest(theme, width);
        vm.PreviousDayCommand.Execute(null);
        Pump();

        view.FindControl<TextBlock>("TodayTitle")!.Text.Should().Be("Lunedì 9 novembre");
        var band = view.FindControl<Border>("PastDayBand")!;
        band.IsVisible.Should().BeTrue("su un giorno passato c'è la fascia");
        BoundsIn(band, view).Right.Should().BeLessThanOrEqualTo(width + 0.5);
        BoundsIn(view.FindControl<Button>("NextDayButton")!, view).Right.Should().BeLessThanOrEqualTo(width + 0.5, "‹ › restano a schermo");

        var days = view.FindControl<DayHeaderView>("DayHeader")!.GetVisualDescendants().OfType<Button>().ToList();
        var selected = days.Single(b => b.Classes.Contains("selected"));
        selected.Should().BeSameAs(days[^2], "ieri è la penultima colonna");
        var column = BoundsIn(selected, view);
        var marked = view.GetVisualDescendants().OfType<HabitRow>()
            .SelectMany(r => r.GetVisualDescendants().OfType<HeatmapStrip>().Single().GetVisualDescendants().OfType<Border>()
                .Where(b => b.Name == HeatmapStrip.SelectedCellName)).ToList();
        marked.Should().HaveCount(9, "una cella marcata per riga");
        marked.Should().OnlyContain(b => BoundsIn(b, view).Center.X >= column.Left && BoundsIn(b, view).Center.X <= column.Right);

        ScreenshotProbeTests.Capture(w, $"home-manifesto-ieri-{theme}-{width}");
        w.Close();
    }

    /// <summary>Le tre versioni delle righe (Task 9) sulla Home piena, larga e da telefono: dodici PNG.</summary>
    [AvaloniaTheory]
    [InlineData(HomeRows.Compact, "light", 900)]
    [InlineData(HomeRows.Compact, "light", 360)]
    [InlineData(HomeRows.Compact, "dark", 900)]
    [InlineData(HomeRows.Compact, "dark", 360)]
    [InlineData(HomeRows.Marks, "light", 900)]
    [InlineData(HomeRows.Marks, "light", 360)]
    [InlineData(HomeRows.Marks, "dark", 900)]
    [InlineData(HomeRows.Marks, "dark", 360)]
    [InlineData(HomeRows.Comfortable, "light", 900)]
    [InlineData(HomeRows.Comfortable, "light", 360)]
    [InlineData(HomeRows.Comfortable, "dark", 900)]
    [InlineData(HomeRows.Comfortable, "dark", 360)]
    public void Home_in_three_versions(HomeRows style, string theme, int width)
    {
        using var culture = TestCulture.Pin();
        var (w, view, _, rows) = ShowManifest(theme, width, style);
        var metrics = HomeRowsMetrics.For(style);
        var columns = HomeRowsMetrics.VisibleDays(style, width);

        var habitRows = view.GetVisualDescendants().OfType<HabitRow>().ToList();
        habitRows.Should().HaveCount(9);
        habitRows.Should().OnlyContain(r => r.FindControl<Grid>("MenuHost")!.Bounds.Height == metrics.RowHeight);
        view.FindControl<TextBlock>("DayCount")!.Text.Should().Contain("4 di 9");

        var days = view.FindControl<DayHeaderView>("DayHeader")!.GetVisualDescendants().OfType<Button>().ToList();
        days.Should().HaveCount(columns);
        days[^1].Content.Should().Be(metrics.HeaderInitials ? "m" : "oggi");   // martedì 10 novembre
        BoundsIn(days[0], view).Left.Should().BeGreaterThanOrEqualTo(0, "l'intestazione sta nella finestra");

        // Ogni riga di primo livello ha le sue colonne sotto i giorni; le figlie del gruppo, rientrate, dentro la colonna.
        var topLevelIds = rows.Where(r => r.ParentId is null).Select(r => r.HabitNodeId).ToHashSet();
        foreach (var row in habitRows)
        {
            var strip = row.GetVisualDescendants().OfType<HeatmapStrip>().Single();
            var cells = metrics.Marks
                ? strip.GetVisualDescendants().OfType<Border>().Where(b => b.Width == metrics.Column).Cast<Control>().ToList()
                : strip.GetVisualDescendants().OfType<TextBlock>().Cast<Control>().ToList();
            // Le azioni hanno sette giorni, l'astinenza quattordici: si confrontano le colonne da oggi all'indietro.
            cells.Should().HaveCount(Math.Min(columns, ((HomeRowViewModel)row.DataContext!).StripCells.Length));
            var topLevel = topLevelIds.Contains(((HomeRowViewModel)row.DataContext!).HabitNodeId);
            for (var back = 1; back <= cells.Count; back++)
            {
                var center = BoundsIn(cells[^back], view).Center.X;
                var column = BoundsIn(days[^back], view);
                if (topLevel)
                    center.Should().BeApproximately(column.Center.X, 1.0, $"{back - 1} giorni fa");
                else
                    center.Should().BeInRange(column.Left - metrics.Gap, column.Right + metrics.Gap, $"{back - 1} giorni fa");
            }
            // Il nome resta leggibile: la riga non è mangiata dalle colonne.
            BoundsIn(strip, view).Left.Should().BeGreaterThan(width * 0.3);
        }

        ScreenshotProbeTests.Capture(w, $"home-manifesto-{style.ToString().ToLowerInvariant()}-{theme}-{width}");
        w.Close();
    }

    /// <summary>Finding B9: la Home Larga coi valori in inchiostro invece che nel colore dell'abitudine, da mettere
    /// accanto a <c>home-manifesto-comfortable-light-900.png</c>. Solo un'immagine per la decisione dell'autore: la
    /// variante si ottiene ridipingendo le celle nel test, nessun codice di prodotto.</summary>
    [AvaloniaFact]
    public void Home_with_values_in_ink_B9()
    {
        using var culture = TestCulture.Pin();
        var (w, view, _, _) = ShowManifest("light", 900, HomeRows.Comfortable);
        Application.Current!.TryGetResource("Brush.Ink", ThemeVariant.Light, out var res).Should().BeTrue();
        var ink = (Avalonia.Media.IBrush)res!;

        var on = view.GetVisualDescendants().OfType<HeatmapStrip>()
            .SelectMany(s => s.GetVisualDescendants().OfType<TextBlock>())
            .Where(t => t.FontWeight == Avalonia.Media.FontWeight.SemiBold).ToList();
        on.Should().NotBeEmpty("la Home piena ha valori accesi");
        on.Should().Contain(t => !Equals(t.Foreground, ink), "di serie i valori hanno il colore dell'abitudine");
        foreach (var t in on) t.Foreground = ink;
        Pump();

        ScreenshotProbeTests.Capture(w, "home-manifesto-comfortable-light-900-inchiostro");
        w.Close();
    }
}
