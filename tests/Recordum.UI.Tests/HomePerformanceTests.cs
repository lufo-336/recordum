using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>F1-PERF1: la Home con 300 righe (20 gruppi aperti da 10 figli + 80 abitudini) contro il
/// budget E9/E20 (&lt; 500 ms). Misura in headless, quindi senza il costo del rendering vero: stampa i
/// numeri, e la soglia è il budget. Fuori dalla CI (Category=Performance): sui runner condivisi i tempi
/// oscillano. Prima della lista piatta virtualizzata: caldo ≈ 1,7 s, ricarica ≈ 1,8 s.</summary>
[Trait("Category", "Performance")]
public class HomePerformanceTests(ITestOutputHelper output)
{
    private static HomeTreeRow[] ThreeHundredRows()
    {
        var rows = new List<HomeTreeRow>();
        for (var g = 0; g < 20; g++)
        {
            var groupId = Guid.NewGuid();
            rows.Add(RowFixture.Row(groupId, $"g{g:00}", "#6366F1", type: null, childCount: 10));
            for (var c = 0; c < 10; c++)
                rows.Add(RowFixture.Row(Guid.NewGuid(), $"g{g:00}c{c}", "#E11D48", parent: groupId));
        }
        for (var h = 0; h < 80; h++)
            rows.Add(RowFixture.Row(Guid.NewGuid(), $"h{h:00}", "#10B981"));
        return rows.ToArray();
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static (Window W, HomeViewModel Vm, long Ms) OpenHome()
    {
        var (vm, _, _) = HomeFixture.Build(ThreeHundredRows());
        var watch = Stopwatch.StartNew();
        var w = new Window { Width = 900, Height = 700, Content = new HomeView { DataContext = vm } };
        w.Show();
        vm.LoadCommand.Execute(null);
        Pump();
        watch.Stop();
        vm.Rows.Should().HaveCount(300);
        return (w, vm, watch.ElapsedMilliseconds);
    }

    private static List<HomeRowViewModel> Realised(Window w) => w.GetVisualDescendants()
        .Where(v => v is HabitRow or GroupRow).Select(v => (HomeRowViewModel)((Control)v).DataContext!).ToList();

    [AvaloniaFact]
    public void Three_hundred_rows_open_and_reload_within_the_home_budget()
    {
        var (cold, _, coldMs) = OpenHome();   // paga JIT e caricamento degli XAML
        cold.Close();
        var (w, vm, warmMs) = OpenHome();

        var watch = Stopwatch.StartNew();
        vm.LoadCommand.Execute(null);   // quello che fa ogni registrazione o archiviazione
        Pump();
        watch.Stop();
        output.WriteLine($"300 righe: freddo {coldMs} ms, caldo {warmMs} ms, ricarica {watch.ElapsedMilliseconds} ms, "
                         + $"{w.GetVisualDescendants().Count()} nodi visuali, {Realised(w).Count} righe con una vista");

        // Il tempo da solo non prova niente: un albero che non si costruisce è velocissimo. Le righe
        // con una vista sono una parte delle 300, e scorrendo in fondo nasce l'ultima.
        Realised(w).Count.Should().BeInRange(1, 299);
        var scroller = w.GetVisualDescendants().OfType<ItemsControl>().Single(i => i.Name == "RowsHost")
            .FindAncestorOfType<ScrollViewer>()!;
        scroller.ScrollToEnd();
        Pump();
        Realised(w).Should().Contain(vm.Rows[^1]);

        warmMs.Should().BeLessThan(500);
        watch.ElapsedMilliseconds.Should().BeLessThan(500);
        w.Close();
    }

    [AvaloniaFact]
    public void The_scroll_extent_stays_steady_while_scrolling_through_open_groups()
    {
        // Con l'albero annidato virtualizzato la stima saltava da 42 900 a 3 800 px scorrendo (un gruppo
        // aperto è alto quanto 11 righe): la barra cambiava dimensione sotto il dito. Le righe piatte
        // hanno quasi la stessa altezza.
        var (w, _, _) = OpenHome();
        var scroller = w.GetVisualDescendants().OfType<ItemsControl>().Single(i => i.Name == "RowsHost")
            .FindAncestorOfType<ScrollViewer>()!;
        var extents = new List<double>();
        for (var step = 0; step <= 10; step++)
        {
            scroller.Offset = new Avalonia.Vector(0, scroller.Extent.Height * step / 10);
            Pump();
            extents.Add(scroller.Extent.Height);
        }
        output.WriteLine("estensioni: " + string.Join(", ", extents.Select(e => e.ToString("0", System.Globalization.CultureInfo.InvariantCulture))));
        extents.Max().Should().BeLessThan(extents.Min() * 1.15);
        w.Close();
    }
}
