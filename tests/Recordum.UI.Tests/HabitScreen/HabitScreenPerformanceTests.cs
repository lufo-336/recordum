using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.Group;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>F2a-8.3: quanto costa aprire il tab di un'abitudine con un anno di registrazioni (365 giorni, due al giorno).
/// Si registra il numero, come in F1-PERF1; la soglia e' larga. Fuori dalla CI (Category=Performance).</summary>
[Trait("Category", "Performance")]
public class HabitScreenPerformanceTests(ITestOutputHelper output)
{
    private static readonly PrivateDbHost Host = new("recordum-ui-perf");

    [AvaloniaFact]
    public async Task Opening_a_habit_with_a_year_of_records_is_measured()
    {
        var now = Host.Clock.Now;
        using var seedScope = Host.Provider.CreateScope();
        var s = seedScope.ServiceProvider;
        var id = await s.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = HabitType.Counter, Name = "Anno", Color = "#E11D48",
        });
        var completions = s.GetRequiredService<ICompletionService>();
        for (var d = 0; d < 365; d++)
            for (var k = 0; k < 2; k++)
            {
                var at = now.AddDays(-d).AddHours(-k);
                await completions.RecordAsync(new Completion
                {
                    HabitNodeId = id, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
                });
            }

        async Task<long> OpenOnceAsync()
        {
            var watch = Stopwatch.StartNew();
            using var scope = Host.Provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<HabitScreenViewModel>();
            var view = new HabitScreenView { DataContext = vm };
            var w = new Window { Width = 900, Height = 900, Content = view };
            w.Show();
            await vm.LoadAsync(id);
            for (var i = 0; i < 200 && !view.GetVisualDescendants().OfType<Button>().Any(b => b.Classes.Contains("day")); i++)
            { await Task.Delay(10); PrivateDbHost.Pump(); }
            PrivateDbHost.Pump();
            watch.Stop();
            vm.Calendar.Cells.Any(c => c.TintStep > 0).Should().BeTrue("il calendario deve mostrare le registrazioni");
            w.Close();
            return watch.ElapsedMilliseconds;
        }

        var cold = await OpenOnceAsync();   // paga JIT e caricamento degli XAML
        var warm = await OpenOnceAsync();
        output.WriteLine($"Tab con 730 registrazioni in un anno: freddo {cold} ms, caldo {warm} ms");
        warm.Should().BeLessThan(5000);
    }

    /// <summary>Onda 2a, Task 10: un'azione con tre anni di registrazioni giornaliere. Si misura LoadAsync con la
    /// Cronologia aperta, poi il passaggio a "Tutto" fino alle tre heatmap d'anno disegnate. Riferimento: la Home con 300
    /// righe sta sotto i 500 ms.</summary>
    [AvaloniaFact]
    public async Task Three_years_of_daily_records_and_the_whole_history_are_measured()
    {
        var now = Host.Clock.Now;
        using var seedScope = Host.Provider.CreateScope();
        var s = seedScope.ServiceProvider;
        Host.Clock.Now = now.AddDays(-3 * 365);   // creata tre anni fa: "Tutto" parte dall'anno di creazione
        var id = await s.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = "Tre anni", Color = "#2F8FB0",
        });
        Host.Clock.Now = now;
        var completions = s.GetRequiredService<ICompletionService>();
        for (var d = 0; d < 3 * 365; d++)
        {
            var at = now.AddDays(-d).AddHours(-1);
            await completions.RecordAsync(new Completion
            {
                HabitNodeId = id, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
            });
        }

        async Task<(long Load, long All)> OpenOnceAsync()
        {
            using var scope = Host.Provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<HabitScreenViewModel>();
            var view = new HabitScreenView { DataContext = vm };
            var w = new Window { Width = 1000, Height = 1000, Content = view };
            w.Show();
            vm.IsCronologiaOpen = true;
            var watch = Stopwatch.StartNew();
            await vm.LoadAsync(id);
            for (var i = 0; i < 300 && vm.Map.Cells.Count == 0; i++) { await Task.Delay(5); PrivateDbHost.Pump(); }
            PrivateDbHost.Pump();
            var load = watch.ElapsedMilliseconds;

            watch.Restart();
            vm.Period.Period = Recordum.Presentation.ViewModels.HabitScreen.History.HistoryPeriod.All;
            bool Drawn() => view.GetVisualDescendants().OfType<Recordum.UI.Views.HabitScreen.History.HeatGrid>()
                .Count(c => c.Classes.Contains("year") && c.Bounds.Width > 0 && c.IsEffectivelyVisible) >= 3;
            for (var i = 0; i < 600 && vm.Map.Years.Count < 4; i++) { await Task.Delay(5); PrivateDbHost.Pump(); }
            var data = watch.ElapsedMilliseconds;
            for (var i = 0; i < 600 && !Drawn(); i++) { await Task.Delay(5); PrivateDbHost.Pump(); }
            var all = watch.ElapsedMilliseconds;
            output.WriteLine($"  \"Tutto\": dati {data} ms, disegno {all - data} ms");
            Drawn().Should().BeTrue("tre anni, tre heatmap (quattro anni di calendario)");
            vm.Map.Years.Count.Should().BeGreaterThanOrEqualTo(3);
            w.Close();
            return (load, all);
        }

        var cold = await OpenOnceAsync();
        var warm = await OpenOnceAsync();
        output.WriteLine($"Tre anni giornalieri: LoadAsync freddo {cold.Load} ms, caldo {warm.Load} ms; " +
                         $"\"Tutto\" freddo {cold.All} ms, caldo {warm.All} ms");
        warm.Load.Should().BeLessThan(5000);
        warm.All.Should().BeLessThan(5000);
    }

    /// <summary>Onda 2b, Task 9: un gruppo di 12 figlie con tre anni di registrazioni giornaliere ciascuna (13 140). Si misura
    /// LoadAsync con la Panoramica aperta fino a "Insieme" in Anno (il periodo del gruppo) e alla mappa disegnati, poi il
    /// passaggio a "Tutto" (Insieme disegna tutti i giorni per tutte le righe, senza ritaglio). Riferimento: la schermata
    /// dell'abitudine con tre anni, ~0,35 s per "Tutto".</summary>
    [AvaloniaFact]
    public async Task A_group_of_twelve_with_three_years_each_is_measured()
    {
        var now = Host.Clock.Now;
        using var seedScope = Host.Provider.CreateScope();
        var s = seedScope.ServiceProvider;
        Host.Clock.Now = now.AddDays(-3 * 365);
        var nodes = s.GetRequiredService<IHabitNodeService>();
        var group = await nodes.CreateAsync(new HabitNode { NodeType = NodeType.Group, Name = "Dodici", Color = "#2F8FB0" });
        var children = new List<Guid>();
        for (var c = 0; c < 12; c++)
            children.Add(await nodes.CreateAsync(new HabitNode
            {
                NodeType = NodeType.Habit, HabitType = HabitType.Action, Name = $"Figlia {c}", Color = "#2F8FB0", ParentId = group,
            }));
        Host.Clock.Now = now;
        await using (var db = await s.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Recordum.Data.RecordumDbContext>>()
                         .CreateDbContextAsync())
        {
            foreach (var (child, c) in children.Select((x, i) => (x, i)))
                for (var d = 0; d < 3 * 365; d++)
                {
                    if ((d + c) % 4 == 0) continue;   // giorni vuoti diversi per figlia: la mappa ha gradini
                    var at = now.AddDays(-d).AddHours(-1);
                    db.Completions.Add(new Completion
                    {
                        Id = Guid.NewGuid(), HabitNodeId = child, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
                    });
                }
            await db.SaveChangesAsync();
        }

        async Task<(long Load, long All)> OpenOnceAsync()
        {
            using var scope = Host.Provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<HabitScreenViewModel>();
            var view = new HabitScreenView { DataContext = vm };
            var w = new Window { Width = 1000, Height = 1200, Content = view };
            w.Show();
            vm.IsPanoramicaOpen = true;
            TogetherGrid Grid() => view.GetVisualDescendants().OfType<TogetherGrid>().Single(g => g.Name == "Together");
            bool Drawn(int days) => vm.Panoramica.Together.Rows.Count == 12 && (days > 365 ? vm.Panoramica.Map.Years.Count >= 3 : vm.Panoramica.Map.Weeks.Count > 0)
                && view.GetVisualDescendants().OfType<TogetherGrid>().Any(g => g.Days >= days && g.Bounds.Height > 0);
            var watch = Stopwatch.StartNew();
            await vm.LoadAsync(group);
            var awaited = watch.ElapsedMilliseconds;
            for (var i = 0; i < 600 && !Drawn(365); i++) { await Task.Delay(5); PrivateDbHost.Pump(); }
            PrivateDbHost.Pump();
            var load = watch.ElapsedMilliseconds;
            output.WriteLine($"  LoadAsync atteso {awaited} ms, poi Insieme e mappa disegnati a {load} ms");
            Drawn(365).Should().BeTrue("Insieme in Anno e la mappa");
            vm.Period.Period.Should().Be(Recordum.Presentation.ViewModels.HabitScreen.History.HistoryPeriod.Year);

            watch.Restart();
            vm.Period.Period = Recordum.Presentation.ViewModels.HabitScreen.History.HistoryPeriod.All;
            for (var i = 0; i < 600 && !Drawn(3 * 365); i++) { await Task.Delay(5); PrivateDbHost.Pump(); }
            PrivateDbHost.Pump();
            var all = watch.ElapsedMilliseconds;
            Drawn(3 * 365).Should().BeTrue("tre anni in Insieme e nella mappa");
            Grid().Rows!.Should().OnlyContain(r => r.Cells.Count >= 3 * 365);
            w.Close();
            return (load, all);
        }

        var cold = await OpenOnceAsync();
        var warm = await OpenOnceAsync();
        output.WriteLine($"Gruppo di 12 x tre anni: LoadAsync+Anno freddo {cold.Load} ms, caldo {warm.Load} ms; " +
                         $"\"Tutto\" freddo {cold.All} ms, caldo {warm.All} ms");
        warm.Load.Should().BeLessThan(5000);
        warm.All.Should().BeLessThan(5000);
    }
}
