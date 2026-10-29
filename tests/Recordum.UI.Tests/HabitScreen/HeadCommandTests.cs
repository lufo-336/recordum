using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;
using Recordum.UI.Converters;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.Head;
using Recordum.UI.Views.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>Gli otto comandi in testa (direzione visiva §6.2): ognuno registra sul giorno scelto, nel colore
/// dell'abitudine. DB privato, servizi veri, orologio al 28/10/2026 alle 10:00.</summary>
public class HeadCommandTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-head-command");
    private const string Color = "#2F8FB0";

    private sealed record Opened(IServiceScope Scope, HabitScreenViewModel Vm, HabitScreenView View, Window Window, Guid Id)
    {
        public ICompletionService Completions => Scope.ServiceProvider.GetRequiredService<ICompletionService>();
        public Border Command => Named<Border>(View, "CommandHost");
    }

    private static async Task<Opened> OpenAsync(HabitType type, Func<IServiceProvider, Guid, Task>? setup = null)
    {
        Host.Clock.Now = new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);
        var scope = Host.Provider.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", "00:00");
        var id = await sp.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = type, Name = $"Comando {type}", Color = Color,
        });
        if (setup is not null) await setup(sp, id);
        var vm = sp.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = 900, Height = 800, Content = view };
        w.Show();
        Pump();
        return new Opened(scope, vm, view, w, id);
    }

    private static void Pump() => PrivateDbHost.Pump();

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 100 && !done(); i++) { await Task.Delay(20); Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static T Named<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Click(Window w, Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();
    }

    private static Task<IReadOnlyList<Completion>> ListAsync(Opened o) => o.Completions.ListByHabitAsync(o.Id).AsTask();

    [AvaloniaFact]
    public async Task Action_click_records_and_click_again_removes()
    {
        var o = await OpenAsync(HabitType.Action);
        var record = Named<ToggleButton>(o.Command, "RecordButton");
        record.IsChecked.Should().BeFalse();

        Click(o.Window, record);
        await UntilAsync(() => record.IsChecked == true);
        (await ListAsync(o)).Should().ContainSingle();

        Click(o.Window, record);
        await UntilAsync(() => record.IsChecked == false);
        (await ListAsync(o)).Should().BeEmpty();
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Action_on_another_day_records_there()
    {
        using var _ = TestCulture.Pin();
        var o = await OpenAsync(HabitType.Action);
        await o.Vm.SelectDayAsync(new DateOnly(2026, 10, 26));
        Pump();
        var record = Named<ToggleButton>(o.Command, "RecordButton");
        record.GetVisualDescendants().OfType<TextBlock>().Single(t => t.IsEffectivelyVisible).Text.Should().Be("Fatto");

        Click(o.Window, record);
        await UntilAsync(() => record.IsChecked == true);
        var saved = (await ListAsync(o)).Should().ContainSingle().Subject;
        saved.IsRetroactive.Should().BeTrue();
        DateOnly.FromDateTime(saved.CompletedAt).Should().Be(new DateOnly(2026, 10, 26));

        await o.Vm.SelectDayAsync(new DateOnly(2026, 10, 28));
        Pump();
        record.IsChecked.Should().BeFalse("oggi non ha registrazioni");
        record.GetVisualDescendants().OfType<TextBlock>().Single(t => t.IsEffectivelyVisible).Text.Should().Be("Fatto oggi");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task CounterTap_plus_and_minus()
    {
        var o = await OpenAsync(HabitType.Counter, async (sp, id) =>
            await sp.GetRequiredService<IGoalService>().CreateAsync(new Goal
            {
                HabitNodeId = id, Name = "otto", TargetValue = 8, PeriodType = GoalPeriodType.Rolling, RollingDays = 1,
            }));
        var tap = (CounterTapQuickAddViewModel)o.Vm.QuickAdd!;
        var plus = Named<Button>(o.Command, "RecordButton");
        Click(o.Window, plus);
        await UntilAsync(() => tap.DayCount == 1 && !tap.IsSaving);
        Click(o.Window, plus);
        await UntilAsync(() => tap.DayCount == 2 && !tap.IsSaving);
        Click(o.Window, Named<Button>(o.Command, "MinusButton"));
        await UntilAsync(() => tap.DayCount == 1);
        for (var i = 0; i < 10; i++) { await Task.Delay(20); Pump(); }   // lascia scorrere gli eventi: il conto non deriva

        tap.DayCount.Should().Be(1);
        (await ListAsync(o)).Should().ContainSingle();
        var progress = Named<TextBlock>(o.Command, "Progress");
        progress.IsVisible.Should().BeTrue();
        progress.Text.Should().Be("1/8");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task CounterTap_without_a_goal_has_no_progress()
    {
        var o = await OpenAsync(HabitType.Counter);
        Named<TextBlock>(o.Command, "Progress").IsVisible.Should().BeFalse();
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task State_step_click_records_that_value()
    {
        var o = await OpenAsync(HabitType.State, async (sp, id) =>
        {
            var scale = (await sp.GetRequiredService<IReferenceDataQueryService>().ListScalesAsync())
                .First(s => s.MaxValue - s.MinValue + 1 is >= 3 and <= 10);
            await sp.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
            {
                HabitNodeId = id, Name = "umore", Type = MetricType.Integer, SortOrder = 0, ScaleTemplateId = scale.Id,
            });
        });
        var steps = Named<ItemsControl>(o.Command, "Steps");
        steps.IsVisible.Should().BeTrue();
        Named<MeasuredQuickAddView>(o.Command, "Cells").IsVisible.Should().BeFalse();

        var third = steps.GetVisualDescendants().OfType<Button>().ElementAt(2);
        Click(o.Window, third);
        await UntilAsync(() => ListAsync(o).Result.Count == 1);
        (await ListAsync(o)).Single().Values.Should().ContainSingle().Which.ValueInteger.Should().Be(3);
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Abstinence_lapse_asks_first()
    {
        var o = await OpenAsync(HabitType.Resistance);
        var ab = (AbstinenceQuickAddViewModel)o.Vm.QuickAdd!;
        Click(o.Window, Named<Button>(o.Command, "StartButton"));
        await UntilAsync(() => ab.CanLapse);

        var lapse = Named<Button>(o.Command, "LapseButton");
        lapse.Classes.Should().Contain("danger");
        Click(o.Window, lapse);
        var flyout = (Flyout)lapse.Flyout!;
        flyout.IsOpen.Should().BeTrue("la ricaduta si conferma");
        ab.CanLapse.Should().BeTrue("il clic da solo non chiude il periodo");

        var confirm = ((Control)flyout.Content!).GetLogicalDescendants().OfType<Button>().Single(b => b.Name == "ConfirmLapseButton");
        confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await UntilAsync(() => !ab.CanLapse);
        flyout.IsOpen.Should().BeFalse();
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Session_start_shows_the_running_controls()
    {
        var o = await OpenAsync(HabitType.Counter, async (sp, id) =>
        {
            await sp.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
            {
                HabitNodeId = id, Name = "tempo", Type = MetricType.Seconds, SortOrder = 0,
            });
            await sp.GetRequiredService<IHabitConfigService>().UpsertAsync(id, c => { c.HasIntegratedTimer = true; c.TimerMode = TimerMode.Stopwatch; });
        });
        o.Vm.Case.Should().Be(HabitCase.Session);
        var start = Named<Button>(o.Command, "StartButton");
        start.Classes.Should().Contain("record");
        Named<ToggleButton>(o.Command, "ManualButton").IsEffectivelyVisible.Should().BeTrue();
        Named<TextBlock>(o.Command, "Elapsed").IsVisible.Should().BeFalse("da fermo non c'è un tempo");

        Named<ToggleButton>(o.Command, "ManualButton").IsChecked = true;
        Pump();
        Named<Border>(o.Command, "ManualHost").Child.Should().BeOfType<SeriesQuickAddView>().Which.IsEffectivelyVisible.Should().BeTrue();

        Click(o.Window, start);
        await UntilAsync(() => o.Vm.TimerControl.IsActive);
        start.IsVisible.Should().BeFalse();
        Named<Button>(o.Command, "PauseButton").IsEffectivelyVisible.Should().BeTrue();
        Named<Button>(o.Command, "StopButton").IsEffectivelyVisible.Should().BeTrue();
        var elapsed = Named<TextBlock>(o.Command, "Elapsed");
        elapsed.IsVisible.Should().BeTrue();
        elapsed.FontSize.Should().Be(20);
        Named<Button>(o.Command, "NextSegmentButton").IsVisible.Should().BeFalse("un cronometro semplice non ha segmenti");
        Named<StackPanel>(o.Command, "ManualRow").IsVisible.Should().BeFalse("mentre corre non si registra a mano");

        await o.Vm.TimerControl.StopCommand.ExecuteAsync(null);
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Journal_command_opens_the_diary_column()
    {
        var o = await OpenAsync(HabitType.Journal);
        o.Vm.IsDiarioOpen = false;
        Pump();
        Click(o.Window, Named<Button>(o.Command, "PrimaryButton"));
        o.Vm.IsDiarioOpen.Should().BeTrue();
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Record_button_uses_the_habit_text_variant()
    {
        var o = await OpenAsync(HabitType.Action);
        var record = Named<ToggleButton>(o.Command, "RecordButton");
        var expected = ((ISolidColorBrush)HabitTextBrushConverter.BrushFor(Color, o.Window.ActualThemeVariant)).Color;
        ((ISolidColorBrush)record.BorderBrush!).Color.Should().Be(expected);
        ((ISolidColorBrush)record.Foreground!).Color.Should().Be(expected);

        Click(o.Window, record);
        await UntilAsync(() => record.IsChecked == true);
        ((ISolidColorBrush)record.Background!).Color.Should().Be(Avalonia.Media.Color.Parse(Color), "fatto = pieno nel colore");

        o.Window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        Click(o.Window, record);
        await UntilAsync(() => record.IsChecked == false);
        ((ISolidColorBrush)record.BorderBrush!).Color.Should().Be(
            ((ISolidColorBrush)HabitTextBrushConverter.BrushFor(Color, Avalonia.Styling.ThemeVariant.Dark)).Color,
            "la variante \"testo\" segue il tema");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task A_case_without_its_quick_add_builds_no_command()
    {
        // Review dell'onda 2a: LoadAsync assegna il caso prima di ricostruire il quick-add; per un attimo la testa
        // costruiva il comando del caso nuovo sopra il quick-add dell'abitudine di prima (binding compilati in errore).
        var o = await OpenAsync(HabitType.Action);
        o.Vm.QuickAdd.Should().BeOfType<ActionQuickAddViewModel>();
        foreach (var other in new[] { HabitCase.CounterTap, HabitCase.State, HabitCase.Abstinence, HabitCase.Measured, HabitCase.Series })
        {
            o.Vm.Case = other;
            HeadCommandTemplate.Build(o.Vm).Should().BeNull($"{other} sopra un quick-add d'azione");
        }
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Session_without_fields_keeps_the_manual_tap()
    {
        // Review dell'onda 2a: un contatore col timer e senza campi ha il quick-add a tocco, che QuickAddTemplate non
        // costruisce: "+ a mano" spariva e si poteva registrare solo col cronometro (su main c'erano +1 e annulla).
        var o = await OpenAsync(HabitType.Counter, (sp, id) =>
            sp.GetRequiredService<IHabitConfigService>().UpsertAsync(id, c => { c.HasIntegratedTimer = true; c.TimerMode = TimerMode.Stopwatch; }).AsTask());
        o.Vm.Case.Should().Be(HabitCase.Session);
        Named<ToggleButton>(o.Command, "ManualButton").IsVisible.Should().BeTrue();
        Named<Border>(o.Command, "ManualHost").Child.Should().BeOfType<CounterTapCommandView>();
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task There_is_no_recording_for_row()
    {
        foreach (var setup in new Func<IServiceProvider, Guid, Task>[]
        {
            (sp, id) => sp.GetRequiredService<IMetricDefinitionService>().CreateAsync(new MetricDefinition
                { HabitNodeId = id, Name = "pagine", Type = MetricType.Integer, SortOrder = 0 }).AsTask(),
            async (sp, id) =>
            {
                var ms = sp.GetRequiredService<IMetricDefinitionService>();
                await ms.CreateAsync(new MetricDefinition { HabitNodeId = id, Name = "rip", Type = MetricType.Integer, SortOrder = 0 });
                await ms.CreateAsync(new MetricDefinition { HabitNodeId = id, Name = "kg", Type = MetricType.Decimal, SortOrder = 1 });
            },
        })
        {
            var o = await OpenAsync(HabitType.Counter, setup);
            o.Command.Child.Should().BeOfType<SeriesQuickAddView>();
            Named<Button>(o.Command, "SaveButton").Classes.Should().Contain("record");
            o.View.GetVisualDescendants().OfType<TextBlock>()
                .Should().NotContain(t => t.Text == Strings.Abitudine_QuickAdd_RecordingFor);
            o.Window.Close();
        }
    }
}
