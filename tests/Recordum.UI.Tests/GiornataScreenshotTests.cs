using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
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

    [AvaloniaTheory]
    [InlineData("light", 900)]
    [InlineData("light", 360)]
    [InlineData("dark", 900)]
    [InlineData("dark", 360)]
    public void Home_full(string theme, int width)
    {
        using var culture = TestCulture.Pin();
        var rows = ManifestHome.Rows(ManifestHome.Today);
        var vm = ManifestHome.Home(rows);
        var view = new HomeView { DataContext = vm };
        var w = new Window
        {
            Width = width, Height = 700, Content = view,
            RequestedThemeVariant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        w.Show();
        vm.LoadCommand.Execute(null);
        Pump();

        var habitRows = view.GetVisualDescendants().OfType<HabitRow>().ToList();
        habitRows.Should().HaveCount(9, "nove abitudini");
        view.GetVisualDescendants().OfType<GroupRow>().Should().ContainSingle("la Routine del mattino");

        var header = view.FindControl<HeatmapStrip>("StripHeader")!;
        header.IsVisible.Should().BeTrue("l'intestazione dei giorni si vede");
        header.Bounds.Width.Should().BeGreaterThan(0);
        var headerToday = header.GetVisualDescendants().OfType<TextBlock>().Last();
        headerToday.Text.Should().Be("oggi");
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
}
