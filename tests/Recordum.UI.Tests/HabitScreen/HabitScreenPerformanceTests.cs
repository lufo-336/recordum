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
}
