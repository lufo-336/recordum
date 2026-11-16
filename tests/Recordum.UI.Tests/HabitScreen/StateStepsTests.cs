using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>B3 e B6 dell'onda "Giornata": i gradini dello stato portano il loro numero e quello registrato nel giorno
/// scelto è pieno; "+ a mano" della sessione è un secondario a contorno. DB privato, orologio al 28/10/2026 alle 10:00.</summary>
public class StateStepsTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-state-steps");
    private const string Color = "#2F8FB0";

    private sealed record Opened(IServiceScope Scope, HabitScreenViewModel Vm, HabitScreenView View, Window Window, Guid Id)
    {
        public Border Command => Named<Border>(View, "CommandHost");
        public IReadOnlyList<Button> Steps =>
            Named<ItemsControl>(Command, "Steps").GetVisualDescendants().OfType<Button>().ToList();
        public MeasuredQuickAddViewModel QuickAdd => (MeasuredQuickAddViewModel)Vm.QuickAdd!;
    }

    private static async Task<Opened> OpenAsync(HabitType type, Func<IServiceProvider, Guid, Task> setup)
    {
        Host.Clock.Now = new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);
        var scope = Host.Provider.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", "00:00");
        var id = await sp.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = type, Name = $"Gradini {type}", Color = Color,
        });
        await setup(sp, id);
        var vm = sp.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = 900, Height = 800, Content = view };
        w.Show();
        PrivateDbHost.Pump();
        return new Opened(scope, vm, view, w, id);
    }

    private static Task<Opened> OpenStateAsync() => OpenAsync(HabitType.State, async (sp, id) =>
    {
        var scale = (await sp.GetRequiredService<IReferenceDataQueryService>().ListScalesAsync())
            .First(s => s.MaxValue - s.MinValue + 1 is >= 3 and <= 10);
        await sp.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
        {
            HabitNodeId = id, Name = "umore", Type = MetricType.Integer, SortOrder = 0, ScaleTemplateId = scale.Id,
        });
    });

    private static Task<Opened> OpenSessionAsync() => OpenAsync(HabitType.Counter, async (sp, id) =>
    {
        await sp.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
        {
            HabitNodeId = id, Name = "tempo", Type = MetricType.Seconds, SortOrder = 0,
        });
        await sp.GetRequiredService<IHabitConfigService>().UpsertAsync(id, c => { c.HasIntegratedTimer = true; c.TimerMode = TimerMode.Stopwatch; });
    });

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++) { await Task.Delay(20); PrivateDbHost.Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static T Named<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Click(Window w, Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        PrivateDbHost.Pump();
    }

    private static Color Token(Control c, string key) =>
        c.TryFindResource(key, c.ActualThemeVariant, out var r) && r is ISolidColorBrush b
            ? b.Color
            : throw new InvalidOperationException($"{key} non è un pennello pieno");

    private static Color? Fill(TemplatedControl c) => (c.Background as ISolidColorBrush)?.Color;

    private static bool IsEmpty(Color? c) => c is null || c.Value.A == 0;

    [AvaloniaFact]
    public async Task State_steps_show_their_number()
    {
        var o = await OpenStateAsync();
        o.Steps.Should().HaveCountGreaterThanOrEqualTo(3);
        for (var i = 0; i < o.Steps.Count; i++)
        {
            var step = o.Steps[i];
            // Il valore salvato è l'intero, a base 1: è quello che si legge dentro il gradino.
            var text = step.GetVisualDescendants().OfType<TextBlock>().Single();
            text.Text.Should().Be((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            text.FontSize.Should().Be(13);
            text.FontWeight.Should().Be(FontWeight.Medium);
            text.IsEffectivelyVisible.Should().BeTrue();
            text.Bounds.Width.Should().BeGreaterThan(0);
        }
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task No_step_is_filled_on_a_day_without_a_value()
    {
        var o = await OpenStateAsync();
        o.QuickAdd.Steps.Should().NotContain(s => s.IsCurrent);
        foreach (var step in o.Steps)
        {
            step.Classes.Should().NotContain("current");
            IsEmpty(Fill(step)).Should().BeTrue("un gradino non registrato ha solo il contorno");
            ((ISolidColorBrush)step.BorderBrush!).Color.Should().Be(Token(step, "Brush.HabitText"));
            ((ISolidColorBrush)step.Foreground!).Color.Should().Be(Token(step, "Brush.HabitText"));
        }
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task The_recorded_step_is_filled()
    {
        var o = await OpenStateAsync();
        Click(o.Window, o.Steps[2]);
        await UntilAsync(() => o.QuickAdd.Steps[2].IsCurrent);
        PrivateDbHost.Pump();

        o.QuickAdd.Steps.Count(s => s.IsCurrent).Should().Be(1);
        var filled = o.Steps[2];
        filled.Classes.Should().Contain("current");
        Fill(filled).Should().Be(Token(filled, "Brush.Habit"), "il gradino registrato è pieno nel colore dell'abitudine");
        ((ISolidColorBrush)filled.Foreground!).Color.Should().Be(Token(filled, "Brush.OnInk"));
        // Il numero eredita il colore "su colore": non resta nel colore dell'abitudine sul suo stesso fondo.
        ((ISolidColorBrush)filled.GetVisualDescendants().OfType<TextBlock>().Single().Foreground!).Color
            .Should().Be(Token(filled, "Brush.OnInk"));
        foreach (var other in o.Steps.Where(s => s != filled))
        {
            other.Classes.Should().NotContain("current");
            IsEmpty(Fill(other)).Should().BeTrue();
        }
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task A_second_step_the_same_day_moves_the_fill()
    {
        var o = await OpenStateAsync();
        Click(o.Window, o.Steps[2]);
        await UntilAsync(() => o.QuickAdd.Steps[2].IsCurrent);
        Host.Clock.Now = Host.Clock.Now.AddMinutes(5);
        Click(o.Window, o.Steps[0]);
        await UntilAsync(() => o.QuickAdd.Steps[0].IsCurrent);
        // Vale la registrazione più recente del giorno.
        o.QuickAdd.Steps.Count(s => s.IsCurrent).Should().Be(1);
        PrivateDbHost.Pump();
        o.Steps[0].Classes.Should().Contain("current");
        o.Steps[2].Classes.Should().NotContain("current");
        IsEmpty(Fill(o.Steps[2])).Should().BeTrue("il gradino di prima torna a contorno");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task The_fill_follows_the_selected_day()
    {
        var o = await OpenStateAsync();
        Click(o.Window, o.Steps[2]);
        await UntilAsync(() => o.QuickAdd.Steps[2].IsCurrent);

        await o.Vm.SelectDayAsync(new DateOnly(2026, 10, 27));
        PrivateDbHost.Pump();
        o.QuickAdd.Steps.Should().NotContain(s => s.IsCurrent, "ieri non ha un valore");
        o.Steps.Should().NotContain(b => b.Classes.Contains("current"));

        await o.Vm.SelectDayAsync(new DateOnly(2026, 10, 28));
        PrivateDbHost.Pump();
        o.QuickAdd.Steps[2].IsCurrent.Should().BeTrue("tornati a oggi il gradino registrato è di nuovo pieno");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Undoing_the_record_empties_the_step()
    {
        var o = await OpenStateAsync();
        Click(o.Window, o.Steps[2]);
        await UntilAsync(() => o.QuickAdd.Steps[2].IsCurrent);

        var completions = o.Scope.ServiceProvider.GetRequiredService<ICompletionService>();
        await completions.DeleteAsync((await completions.ListByHabitAsync(o.Id)).Single().Id);
        await UntilAsync(() => !o.QuickAdd.Steps.Any(s => s.IsCurrent));
        PrivateDbHost.Pump();
        o.Steps.Should().NotContain(b => b.Classes.Contains("current"));
        IsEmpty(Fill(o.Steps[2])).Should().BeTrue();
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task A_value_already_there_is_filled_on_open()
    {
        // Il gradino pieno non dipende dal clic di questa sessione: si legge dal giorno.
        var o = await OpenStateAsync();
        Click(o.Window, o.Steps[1]);
        await UntilAsync(() => o.QuickAdd.Steps[1].IsCurrent);
        o.Window.Close();

        var vm = o.Scope.ServiceProvider.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(o.Id);
        ((MeasuredQuickAddViewModel)vm.QuickAdd!).Steps.Select(s => s.IsCurrent)
            .Should().StartWith([false, true, false]);
    }

    [AvaloniaFact]
    public async Task Rebuilding_the_command_keeps_the_day_and_the_fill()
    {
        // Dalla review: cambiare le etichette della scala in Configura ricostruisce il comando. Quello nuovo resta sul
        // giorno scelto (non torna a oggi: registrerebbe nel giorno sbagliato) e rilegge il gradino pieno.
        var o = await OpenStateAsync();
        var yesterday = new DateOnly(2026, 10, 27);
        await o.Vm.SelectDayAsync(yesterday);
        PrivateDbHost.Pump();
        Click(o.Window, o.Steps[2]);
        await UntilAsync(() => o.QuickAdd.Steps[2].IsCurrent);
        var before = o.QuickAdd;

        var metrics = o.Scope.ServiceProvider.GetRequiredService<IMetricDefinitionService>();
        var metric = (await metrics.ListByHabitAsync(o.Id)).Single();
        await metrics.UpdateAsync(metric.Id, m => m.LabelsJson = "[\"a\",\"b\",\"c\",\"d\"]");
        await UntilAsync(() => !ReferenceEquals(o.Vm.QuickAdd, before));
        await UntilAsync(() => o.QuickAdd.Steps.Count == 4 && o.QuickAdd.Steps[2].IsCurrent);

        DateOnly.FromDateTime(o.QuickAdd.CompletionDate).Should().Be(yesterday);
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Manual_button_hover_is_visible()
    {
        // Dalla review: il passaggio del mouse deve cambiare qualcosa rispetto allo sfondo della schermata.
        var o = await OpenSessionAsync();
        var manual = Named<ToggleButton>(o.Command, "ManualButton");
        var p = manual.TranslatePoint(new Point(manual.Bounds.Width / 2, manual.Bounds.Height / 2), o.Window)!.Value;
        o.Window.MouseMove(p);
        PrivateDbHost.Pump();
        manual.IsPointerOver.Should().BeTrue();
        Fill(manual).Should().NotBeNull().And.NotBe(Token(manual, "Brush.Surface"));
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Manual_button_is_an_outline_secondary()
    {
        var o = await OpenSessionAsync();
        var manual = Named<ToggleButton>(o.Command, "ManualButton");
        manual.IsEffectivelyVisible.Should().BeTrue();

        // B6: a riposo non sembra acceso. Fondo trasparente, bordo e testo dei token.
        IsEmpty(Fill(manual)).Should().BeTrue();
        ((ISolidColorBrush)manual.BorderBrush!).Color.Should().Be(Token(manual, "Brush.Border"));
        manual.BorderThickness.Should().Be(new Thickness(1));
        ((ISolidColorBrush)manual.Foreground!).Color.Should().Be(Token(manual, "Brush.Text"));
        // Nessun ContentPresenter di Fluent che ridipinge il fondo per conto suo.
        var chrome = manual.GetVisualDescendants().OfType<Border>().First();
        IsEmpty((chrome.Background as ISolidColorBrush)?.Color).Should().BeTrue();

        // Da premuto resta leggibile: testo invariato su una tinta dei token, mai il pieno d'accento.
        manual.IsChecked = true;
        PrivateDbHost.Pump();
        ((ISolidColorBrush)manual.Foreground!).Color.Should().Be(Token(manual, "Brush.Text"));
        Fill(manual).Should().Be(Token(manual, "Brush.Selection"));
        ((ISolidColorBrush)chrome.Background!).Color.Should().Be(Token(manual, "Brush.Selection"));
        Named<Border>(o.Command, "ManualHost").IsVisible.Should().BeTrue();
        o.Window.Close();
    }
}
