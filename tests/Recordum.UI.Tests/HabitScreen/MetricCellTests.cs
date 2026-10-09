using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
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
using Recordum.UI.Views.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>Le celle delle metriche e il quick-add misurato. Il numero scritto con l'altro separatore e' la
/// condizione di Review Focus 1: mai un valore moltiplicato in silenzio.</summary>
public class MetricCellTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-mc");
    private static void Pump() => PrivateDbHost.Pump();

    private sealed record Opened(IServiceScope Scope, HabitScreenViewModel Vm, HabitScreenView View, Window Window, Guid Id)
    {
        public async Task<IReadOnlyList<CompletionValue>> SavedValuesAsync() =>
            (await Scope.ServiceProvider.GetRequiredService<ICompletionService>().ListByHabitAsync(Id)).SelectMany(c => c.Values).ToList();
    }

    private static async Task<Opened> OpenAsync(HabitType type, Func<IServiceScope, Guid, Task> setup)
    {
        var scope = Host.Provider.CreateScope();
        var id = await scope.ServiceProvider.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = type, Name = $"MC-{Guid.NewGuid():N}", Color = "#0EA5E9",
        });
        await setup(scope, id);
        var vm = scope.ServiceProvider.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = 900, Height = 900, Content = view };
        w.Show();
        Pump();
        return new Opened(scope, vm, view, w, id);
    }

    /// <summary>Cerca dentro il pannello di registrazione della Cronologia: la fascia Oggi ha controlli omonimi.</summary>
    private static T Named<T>(HabitScreenView view, string name) where T : Control =>
        Named<T>((Visual)view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "QuickAddHost"), name);

    private static Border Quick(HabitScreenView view) => view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "QuickAddHost");

    private static T Named<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++) { await Task.Delay(20); Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static void Click(Window w, Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();
    }

    private static async Task WithCulture(string name, Func<Task> body)
    {
        var old = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(name);
        try { await body(); }
        finally { (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = old; }
    }

    private static MetricDefinition Metric(MetricType type) => new() { Id = Guid.NewGuid(), Name = "M", Type = type };

    // ---- 5.1 ----------------------------------------------------------------------------------------------

    /// <summary>Misura (decisione 8): senza convertitore il binding compilato rifiuta il separatore dell'altra
    /// lingua e il raggruppamento; con il valore gia' presente (7) il rifiuto lascia il ViewModel invariato.</summary>
    [AvaloniaTheory]
    [InlineData("it-IT", "2,5", 2.5, false)]
    [InlineData("it-IT", "2.5", null, true)]
    [InlineData("it-IT", "1.500", null, true)]
    [InlineData("it-IT", "1,500", 1.5, false)]
    [InlineData("en-US", "2.5", 2.5, false)]
    [InlineData("en-US", "2,5", null, true)]
    [InlineData("en-US", "1,500", null, true)]
    [InlineData("en-US", "1.500", 1.5, false)]
    public Task A_number_with_the_other_separator_is_rejected_not_multiplied(string culture, string text, double? expected, bool rejected) =>
        WithCulture(culture, () =>
        {
            var vm = new SeriesCellViewModel(Metric(MetricType.Decimal)) { DecimalValue = 7m };
            var w = new Window { Content = new MetricCellView { DataContext = vm } };
            w.Show();
            Pump();
            try
            {
                var box = Named<TextBox>(w, "DecimalBox");
                box.Text = text;
                Pump();
                if (rejected)
                {
                    DataValidationErrors.GetHasErrors(box).Should().BeTrue($"\"{text}\" in {culture} va rifiutato in modo visibile");
                    vm.DecimalValue.Should().BeNull("R10: un rifiuto svuota la cella, Salva non registra il valore vecchio");
                    box.Text.Should().Be(text, "la casella continua a mostrare il testo rifiutato");
                }
                else
                {
                    DataValidationErrors.GetHasErrors(box).Should().BeFalse();
                    vm.DecimalValue.Should().Be((decimal)expected!.Value);
                }
                vm.DecimalValue.Should().NotBe(7m).And.NotBe(1500m).And.NotBe(25m).And.NotBe(15m);
            }
            finally { w.Close(); }
            return Task.CompletedTask;
        });

    [AvaloniaTheory]
    [InlineData("it-IT", "2.5")]
    [InlineData("en-US", "2,5")]
    [InlineData("it-IT", "1.500")]
    public Task An_integer_cell_rejects_what_is_not_an_integer(string culture, string text) =>
        WithCulture(culture, () =>
        {
            var vm = new SeriesCellViewModel(Metric(MetricType.Integer)) { IntegerValue = 7 };
            var w = new Window { Content = new MetricCellView { DataContext = vm } };
            w.Show();
            Pump();
            try
            {
                var box = Named<TextBox>(w, "IntegerBox");
                box.Text = text;
                Pump();
                DataValidationErrors.GetHasErrors(box).Should().BeTrue();
                vm.IntegerValue.Should().BeNull();
                box.Text.Should().Be(text);
            }
            finally { w.Close(); }
            return Task.CompletedTask;
        });

    [AvaloniaFact]
    public Task A_corrected_then_rejected_number_records_nothing() => WithCulture("it-IT", async () =>
    {
        var o = await OpenAsync(HabitType.State, async (scope, id) =>
            await scope.ServiceProvider.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
            {
                HabitNodeId = id, Name = "Weight", Type = MetricType.Decimal,
            }));
        try
        {
            var box = Named<TextBox>(o.View, "DecimalBox");
            box.Text = "2";
            Pump();
            box.Text = "2.5";   // rifiutato in it-IT
            Pump();
            DataValidationErrors.GetHasErrors(box).Should().BeTrue();
            box.Text.Should().Be("2.5");
            Click(o.Window, Named<Button>(o.View, "SaveButton"));
            await Task.Delay(100);
            Pump();
            (await o.SavedValuesAsync()).Should().BeEmpty("la casella mostra un errore: non si registra il 2 di prima");
            ((MeasuredQuickAddViewModel)o.Vm.QuickAdd!).ErrorMessage.Should().NotBeNullOrEmpty();
        }
        finally { o.Window.Close(); }
    });

    [AvaloniaFact]
    public Task A_series_with_a_rejected_number_records_nothing() => WithCulture("it-IT", async () =>
    {
        var o = await OpenSeriesAsync();
        try
        {
            var box = Named<TextBox>(o.View, "IntegerBox");
            box.Text = "3";
            Pump();
            box.Text = "3.5";   // non e' un intero
            Pump();
            DataValidationErrors.GetHasErrors(box).Should().BeTrue();
            Click(o.Window, Named<Button>(o.View, "SaveButton"));
            await Task.Delay(100);
            Pump();
            (await o.SavedValuesAsync()).Should().BeEmpty();
            ((CounterSeriesQuickAddViewModel)o.Vm.QuickAdd!).ErrorMessage.Should().NotBeNullOrEmpty();

            box.Text = "3";   // corretto: ora si registra
            Pump();
            Click(o.Window, Named<Button>(o.View, "SaveButton"));
            await UntilAsync(() => o.SavedValuesAsync().GetAwaiter().GetResult().Count == 1);
            (await o.SavedValuesAsync()).Single().ValueInteger.Should().Be(3);
        }
        finally { o.Window.Close(); }
    });

    // ---- 5.2 / 5.3 ----------------------------------------------------------------------------------------

    [AvaloniaFact]
    public async Task The_scale_saves_the_step_integer()
    {
        var o = await OpenAsync(HabitType.State, async (scope, id) =>
        {
            var scales = await scope.ServiceProvider.GetRequiredService<IReferenceDataQueryService>().ListScalesAsync();
            await scope.ServiceProvider.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
            {
                HabitNodeId = id, Name = "Mood", Type = MetricType.Integer, ScaleTemplateId = scales[0].Id,
                LabelsJson = "[\"Awful\",\"Poor\",\"Fair\",\"Good\",\"Great\"]",   // etichette diverse dai numeri
            });
        });
        try
        {
            var slider = Named<Slider>(o.View, "ScaleSlider");
            slider.Maximum.Should().Be(4);
            slider.Value = 2;   // il terzo passo
            Pump();
            Named<TextBlock>(o.View, "ScaleCaption").Text.Should().Be("Fair");
            Click(o.Window, Named<Button>(o.View, "SaveButton"));
            await UntilAsync(() => o.SavedValuesAsync().GetAwaiter().GetResult().Count == 1);
            (await o.SavedValuesAsync()).Single().ValueInteger.Should().Be(3, "il passo 3 salva l'intero 3, non l'etichetta ne' l'indice");
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_duration_in_hours_is_saved_in_seconds()
    {
        var o = await OpenAsync(HabitType.State, async (scope, id) =>
            await scope.ServiceProvider.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
            {
                HabitNodeId = id, Name = "Sleep", Type = MetricType.Seconds,
            }));
        try
        {
            await WithCulture("en-US", async () =>
            {
                // Il caso del parametro muto: l'unita' predefinita e' i minuti, quindi si sceglie un'altra.
                Named<ComboBox>(o.View, "DurationUnitBox").SelectedIndex = 0;   // Hours
                Named<TextBox>(o.View, "DurationBox").Text = "1.5";
                Pump();
                Click(o.Window, Named<Button>(o.View, "SaveButton"));
                await UntilAsync(() => o.SavedValuesAsync().GetAwaiter().GetResult().Count == 1);
                (await o.SavedValuesAsync()).Single().ValueSeconds.Should().Be(5400);
            });
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task A_duration_in_minutes_is_saved_in_seconds()
    {
        var o = await OpenAsync(HabitType.State, async (scope, id) =>
            await scope.ServiceProvider.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
            {
                HabitNodeId = id, Name = "Sleep", Type = MetricType.Seconds,
            }));
        try
        {
            Named<Button>(o.View, "ChronoButton").Command.Should().BeSameAs(((MeasuredQuickAddViewModel)o.Vm.QuickAdd!).ToggleChronoCommand,
                "il cronometro della cella e' quello condiviso del quick-add");
            Named<ComboBox>(o.View, "DurationUnitBox").SelectedIndex = 1;   // Minutes
            Named<TextBox>(o.View, "DurationBox").Text = "2";
            Pump();
            Click(o.Window, Named<Button>(o.View, "SaveButton"));
            await UntilAsync(() => o.SavedValuesAsync().GetAwaiter().GetResult().Count == 1);
            (await o.SavedValuesAsync()).Single().ValueSeconds.Should().Be(120);
        }
        finally { o.Window.Close(); }
    }

    // ---- 5.4 / 5.5 ----------------------------------------------------------------------------------------

    private static Task<Opened> OpenSeriesAsync() => OpenAsync(HabitType.Counter, async (scope, id) =>
    {
        await scope.ServiceProvider.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
        {
            HabitNodeId = id, Name = "Reps", Type = MetricType.Integer,
        });
        await scope.ServiceProvider.GetRequiredService<IHabitConfigService>().UpsertAsync(id, c =>
        {
            c.Repeatable = true;
            c.SeriesLabelsJson = "[\"Night\"]";
        });
    });

    [AvaloniaFact]
    public async Task Esc_in_a_series_rename_keeps_the_old_name_even_after_focus_loss()
    {
        var o = await OpenSeriesAsync();
        try
        {
            var vm = (CounterSeriesQuickAddViewModel)o.Vm.QuickAdd!;
            var chip = vm.Chips.Single();
            chip.Name.Should().Be("Night");
            Click(o.Window, Named<Button>(o.View, "RenameButton"));
            chip.IsEditing.Should().BeTrue();
            var box = Named<TextBox>(o.View, "RenameBox");
            box.Focus();
            box.Text = "Discarded";
            Pump();

            o.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            chip.IsEditing.Should().BeFalse("Esc annulla la rinomina");
            chip.Name.Should().Be("Night");

            // Il fuoco esce adesso: non c'e' piu' niente da salvare.
            Named<Button>(o.View, "SaveButton").Focus();
            Pump();
            await Task.Delay(50);
            Pump();
            chip.Name.Should().Be("Night", "il nome scartato non deve essere salvato dal LostFocus");
            var cfg = await o.Scope.ServiceProvider.GetRequiredService<IHabitConfigService>().GetAsync(o.Id);
            cfg!.SeriesLabelsJson.Should().Be("[\"Night\"]");

            // E Invio, al contrario, conferma e salva.
            Click(o.Window, Named<Button>(o.View, "RenameButton"));
            box = Named<TextBox>(o.View, "RenameBox");
            box.Focus();
            box.Text = "Evening";
            Pump();
            o.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await UntilAsync(() => chip.Name == "Evening");
            await UntilAsync(() => o.Scope.ServiceProvider.GetRequiredService<IHabitConfigService>().GetAsync(o.Id).AsTask().GetAwaiter().GetResult()!
                .SeriesLabelsJson!.Contains("Evening", StringComparison.Ordinal));
        }
        finally { o.Window.Close(); }
    }

    [AvaloniaFact]
    public async Task The_last_series_cannot_be_removed()
    {
        var o = await OpenSeriesAsync();
        try
        {
            var vm = (CounterSeriesQuickAddViewModel)o.Vm.QuickAdd!;
            vm.Series.Should().HaveCount(1);
            var remove = Named<Button>(o.View, "RemoveButton");
            remove.IsEffectivelyEnabled.Should().BeFalse("con una sola serie il pulsante e' spento");
            Click(o.Window, remove);
            vm.Series.Should().HaveCount(1);

            Click(o.Window, Named<Button>(o.View, "AddSeriesButton"));
            vm.Series.Should().HaveCount(2);
            var removes = Quick(o.View).GetVisualDescendants().OfType<Button>().Where(b => b.Name == "RemoveButton").ToList();
            removes.Should().HaveCount(2).And.OnlyContain(b => b.IsEffectivelyEnabled, "con due serie si puo' togliere");
            Click(o.Window, removes[1]);
            vm.Series.Should().HaveCount(1);
            Named<Button>(o.View, "RemoveButton").IsEffectivelyEnabled.Should().BeFalse("tornato a una sola serie");
        }
        finally { o.Window.Close(); }
    }
}
