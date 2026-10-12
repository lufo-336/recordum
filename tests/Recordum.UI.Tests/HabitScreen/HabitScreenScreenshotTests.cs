using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.Today;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>F2a-8.4: la schermata dei quattro tipi con fascia, in chiaro e in scuro. I PNG finiscono negli artefatti
/// della CI e si guardano; ogni schermata ha la sua asserzione strutturale (mai il PNG da solo).</summary>
public class HabitScreenScreenshotTests(ITestOutputHelper output)
{
    private static readonly PrivateDbHost Host = new("recordum-ui-shots");
    private static readonly Guid Mood5 = Guid.Parse("00000002-0000-0000-0000-000000000007");

    [AvaloniaTheory]
    [InlineData(HabitType.Action, "Light")]
    [InlineData(HabitType.Action, "Dark")]
    [InlineData(HabitType.Counter, "Light")]
    [InlineData(HabitType.Counter, "Dark")]
    [InlineData(HabitType.Resistance, "Light")]
    [InlineData(HabitType.Resistance, "Dark")]
    [InlineData(HabitType.State, "Light")]
    [InlineData(HabitType.State, "Dark")]
    public async Task The_habit_screen_renders_in_both_themes(HabitType type, string variant)
    {
        var it = CultureInfo.GetCultureInfo("it-IT");
        (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (it, it);
        var now = Host.Clock.Now;
        using var scope = Host.Provider.CreateScope();
        var s = scope.ServiceProvider;
        var id = await s.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = type, Name = $"Esempio {type}", Color = "#E11D48",
        });
        Guid? metricId = null;
        if (type == HabitType.State)
            metricId = await s.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
            {
                HabitNodeId = id, Name = "Umore", Type = MetricType.Integer, ScaleTemplateId = Mood5,
            });
        if (type == HabitType.Resistance)
            await s.GetRequiredService<IAbstinenceService>().StartAsync(id);
        else
            foreach (var d in new[] { 0, 1, 2, 4, 5, 8, 9, 10, 15 })
            {
                var at = now.AddDays(-d);
                await s.GetRequiredService<ICompletionService>().RecordAsync(new Completion
                {
                    HabitNodeId = id, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
                }, metricId is { } m ? [new CompletionValue { MetricDefinitionId = m, ValueInteger = 1 + d % 5 }] : null);
            }

        var vm = s.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window
        {
            Width = 900, Height = 900, Content = view,
            RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        w.Show();
        for (var i = 0; i < 100 && !view.GetVisualDescendants().OfType<Button>().Any(b => b.Classes.Contains("day")); i++)
        { await Task.Delay(20); PrivateDbHost.Pump(); }
        await Task.Delay(200);   // i marcatori arrivano dopo le celle
        PrivateDbHost.Pump();
        try
        {
            output.WriteLine(ScreenshotProbeTests.Capture(w, $"habit-{type.ToString().ToLowerInvariant()}-{variant.ToLowerInvariant()}"));

            view.GetVisualDescendants().OfType<TodayStripView>().Single().IsVisible.Should().BeTrue();
            view.GetVisualDescendants().OfType<Control>().Count(c => c is ActionTodayView or CounterTodayView
                or ResistanceTodayView or StateTodayView).Should().Be(1, "la fascia del tipo");
            view.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "HabitName").Text.Should().Be($"Esempio {type}");
            if (type != HabitType.Resistance)
                vm.Calendar.Cells.Count(c => c.TintStep > 0).Should().BeGreaterThan(3, "il calendario mostra i giorni registrati");
        }
        finally { w.Close(); }
    }
}
