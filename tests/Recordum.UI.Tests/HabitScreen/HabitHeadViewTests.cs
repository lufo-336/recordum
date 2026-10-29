using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.UI.Converters;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.Head;
using Recordum.UI.Views.HabitScreen.QuickAdd;
using Recordum.UI.Views.HabitScreen.Today;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>La testa della schermata dell'abitudine (spec direzione visiva §6.1): nome, un comando, i numeri, i pulsanti
/// delle colonne. DB privato, orologio al 28/10/2026 alle 10:00.</summary>
public class HabitHeadViewTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-head");
    private const string Color = "#2F8FB0";

    private sealed record Opened(IServiceScope Scope, HabitScreenViewModel Vm, HabitScreenView View, Window Window, Guid Id);

    private static async Task<Guid> CreateAsync(IServiceScope scope, HabitCase @case)
    {
        var sp = scope.ServiceProvider;
        var type = @case switch
        {
            HabitCase.Action => HabitType.Action,
            HabitCase.State => HabitType.State,
            HabitCase.Abstinence => HabitType.Resistance,
            HabitCase.Journal => HabitType.Journal,
            _ => HabitType.Counter,
        };
        var id = await sp.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = type, Name = $"Testa {@case}", Color = Color,
        });
        var metrics = sp.GetRequiredService<IMetricDefinitionService>();
        async Task Metric(string name, MetricType t, int sort, Guid? scale = null) => await metrics.CreateAsync(new MetricDefinition
        {
            HabitNodeId = id, Name = name, Type = t, SortOrder = sort, ScaleTemplateId = scale,
        });
        switch (@case)
        {
            case HabitCase.Measured: await Metric("pagine", MetricType.Integer, 0); break;
            case HabitCase.Series: await Metric("ripetizioni", MetricType.Integer, 0); await Metric("kg", MetricType.Decimal, 1); break;
            case HabitCase.Session:
                await Metric("tempo", MetricType.Seconds, 0);
                await sp.GetRequiredService<IHabitConfigService>().UpsertAsync(id, c => { c.HasIntegratedTimer = true; c.TimerMode = TimerMode.Stopwatch; });
                break;
            case HabitCase.State:
                var scale = (await sp.GetRequiredService<IReferenceDataQueryService>().ListScalesAsync()).First();
                await Metric("umore", MetricType.Integer, 0, scale.Id);
                break;
        }
        return id;
    }

    private static async Task<Opened> OpenAsync(HabitCase @case, double width = 900)
    {
        Host.Clock.Now = new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);
        var scope = Host.Provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", "00:00");
        var id = await CreateAsync(scope, @case);
        var vm = scope.ServiceProvider.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = width, Height = 800, Content = view };
        w.Show();
        PrivateDbHost.Pump();
        return new Opened(scope, vm, view, w, id);
    }

    private static T Named<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static HabitHeadView Head(Opened o) => o.View.GetVisualDescendants().OfType<HabitHeadView>().Single();

    [AvaloniaFact]
    public async Task Name_is_20_semibold_in_the_text_variant()
    {
        var o = await OpenAsync(HabitCase.Action);
        var name = Named<TextBlock>(Head(o), "HabitName");
        name.Text.Should().Be("Testa Action");
        name.FontSize.Should().Be(20);
        name.FontWeight.Should().Be(FontWeight.SemiBold);
        ((ISolidColorBrush)name.Foreground!).Color.Should().Be(
            ((ISolidColorBrush)HabitTextBrushConverter.BrushFor(Color, o.Window.ActualThemeVariant)).Color);
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Schedule_and_usual_hour_under_the_name()
    {
        using var _ = TestCulture.Pin();
        var o = await OpenAsync(HabitCase.Action);
        Named<TextBlock>(Head(o), "ScheduleText").Text.Should().Be("Ogni giorno");
        Named<TextBlock>(Head(o), "UsualHour").IsVisible.Should().BeFalse("senza dati non c'è un'ora abituale");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Head_shows_four_figures()
    {
        var o = await OpenAsync(HabitCase.Action);
        Named<ItemsControl>(Head(o), "Figures").ItemCount.Should().Be(4);
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Journal_head_shows_three()
    {
        var o = await OpenAsync(HabitCase.Journal);
        Named<ItemsControl>(Head(o), "Figures").ItemCount.Should().Be(3);
        o.Window.Close();
    }

    [AvaloniaTheory]
    [InlineData(HabitCase.Action, typeof(ActionCommandView))]
    [InlineData(HabitCase.CounterTap, typeof(CounterTapCommandView))]
    [InlineData(HabitCase.Measured, typeof(SeriesQuickAddView))]
    [InlineData(HabitCase.Series, typeof(SeriesQuickAddView))]
    [InlineData(HabitCase.Session, typeof(SessionCommandView))]
    [InlineData(HabitCase.State, typeof(StateCommandView))]
    [InlineData(HabitCase.Abstinence, typeof(AbstinenceCommandView))]
    [InlineData(HabitCase.Journal, typeof(JournalCommandView))]
    public async Task One_command_per_case(HabitCase @case, Type expected)
    {
        var o = await OpenAsync(@case);
        o.Vm.Case.Should().Be(@case);
        var host = Named<Border>(Head(o), "CommandHost");
        host.Child.Should().NotBeNull().And.BeOfType(expected);
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Other_day_shows_the_day_and_back_to_today()
    {
        using var _ = TestCulture.Pin();
        var o = await OpenAsync(HabitCase.Action);
        var head = Head(o);
        Named<StackPanel>(head, "OtherDayLine").IsVisible.Should().BeFalse();

        await o.Vm.SelectDayAsync(new DateOnly(2026, 10, 27));
        PrivateDbHost.Pump();

        Named<StackPanel>(head, "OtherDayLine").IsVisible.Should().BeTrue();
        Named<TextBlock>(head, "SelectedDayText").Text.Should().Contain("27 ottobre");
        Named<Button>(head, "BackToTodayButton").Command!.Execute(null);
        PrivateDbHost.Pump();
        Named<StackPanel>(head, "OtherDayLine").IsVisible.Should().BeFalse();
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Undo_is_hidden_for_abstinence()
    {
        var o = await OpenAsync(HabitCase.Abstinence);
        Named<Button>(Head(o), "UndoButton").IsVisible.Should().BeFalse();
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task There_is_no_today_strip_nor_chip_bar()
    {
        var o = await OpenAsync(HabitCase.Action);
        o.View.GetVisualDescendants().OfType<TodayStripView>().Should().BeEmpty();
        o.View.GetVisualDescendants().OfType<CheckBox>().Where(c => c.Classes.Contains("segment")).Should().BeEmpty();
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Cronologia_has_no_quick_add()
    {
        var o = await OpenAsync(HabitCase.Action);
        o.Vm.IsCronologiaOpen = true;
        PrivateDbHost.Pump();
        var column = Named<Border>(o.View, "CronologiaColumn");
        column.GetVisualDescendants().OfType<ActionCommandView>().Should().BeEmpty();
        column.GetVisualDescendants().OfType<TimerControlView>().Should().BeEmpty();
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Narrow_head_has_no_overlap()
    {
        var o = await OpenAsync(HabitCase.Series, width: 480);
        var head = Head(o);
        Rect R(Control c) => new(c.TranslatePoint(new Point(0, 0), o.Window)!.Value, c.Bounds.Size);
        var parts = new Control[] { Named<TextBlock>(head, "HabitName"), Named<Border>(head, "CommandHost"),
            Named<ItemsControl>(head, "Figures"), Named<Panel>(head, "ColumnButtons") };
        for (var i = 0; i < parts.Length; i++)
            for (var j = i + 1; j < parts.Length; j++)
                R(parts[i]).Intersects(R(parts[j])).Should().BeFalse($"{parts[i].Name} e {parts[j].Name} non si sovrappongono");
        R(Named<Border>(head, "CommandHost")).Right.Should().BeLessThanOrEqualTo(480 + 0.5);
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Column_buttons_use_lucide_icons()
    {
        var o = await OpenAsync(HabitCase.Journal);
        var buttons = Named<Panel>(Head(o), "ColumnButtons").GetVisualDescendants().OfType<ToggleButton>().Where(b => b.IsVisible).ToList();
        buttons.Select(b => b.Name).Should().Equal("ChipDiario", "ChipCronologia", "ChipNote", "ChipConfigura");
        buttons.Should().OnlyContain(b => b.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Any());
        o.View.GetVisualDescendants().OfType<TextBlock>().Should().NotContain(t => t.Text != null && t.Text.Contains('✎'));
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Column_buttons_toggle_their_columns()
    {
        var o = await OpenAsync(HabitCase.Action);
        var note = Named<ToggleButton>(Head(o), "ChipNote");
        note.IsChecked = true;
        PrivateDbHost.Pump();
        o.Vm.IsNoteOpen.Should().BeTrue();
        o.Window.Close();
    }
}
