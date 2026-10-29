using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.History;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.History;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>La colonna Cronologia (direzione visiva §6.1): periodo, mappa che cambia forma, il giorno con la nota, l'elenco
/// del periodo. DB privato, oggi mercoledì 28/10/2026 alle 10:00, reset a mezzanotte.</summary>
public class CronologiaColumnTests
{
    private static readonly PrivateDbHost Host = new("recordum-ui-cronologia",
        s => s.AddSingleton<Recordum.Presentation.Services.IConfirmationDialog>(new Yes()));

    private sealed class Yes : Recordum.Presentation.Services.IConfirmationDialog
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
    }
    private const string Color = "#2F8FB0";
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);

    private sealed record Opened(IServiceScope Scope, HabitScreenViewModel Vm, HabitScreenView View, Window Window, Guid Id)
    {
        public CronologiaColumnView Column => View.GetVisualDescendants().OfType<CronologiaColumnView>().Single();
        public ICompletionService Completions => Scope.ServiceProvider.GetRequiredService<ICompletionService>();
    }

    private static async Task<Opened> OpenAsync(HabitType type = HabitType.Action, Func<IServiceProvider, Guid, Task>? seed = null,
        DateTimeOffset? createdAt = null)
    {
        Host.Clock.Now = createdAt ?? Now;
        var scope = Host.Provider.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", "00:00");
        var id = await sp.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = type, Name = $"Cronologia {Guid.NewGuid():N}", Color = Color,
        });
        Host.Clock.Now = Now;
        if (seed is not null) await seed(sp, id);
        var vm = sp.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        vm.IsCronologiaOpen = true;
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = 1000, Height = 1400, Content = view };
        w.Show();
        Pump();
        var o = new Opened(scope, vm, view, w, id);
        await UntilAsync(() => Days(o).Count > 0);
        return o;
    }

    private static Task Record(IServiceProvider sp, Guid id, DateTimeOffset at, string? notes = null) =>
        sp.GetRequiredService<ICompletionService>().RecordAsync(new Completion
        {
            HabitNodeId = id, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(), Notes = notes,
        }).AsTask();

    private static void Pump() => PrivateDbHost.Pump();

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 150 && !done(); i++) { await Task.Delay(20); Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static T Named<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static List<Button> Days(Opened o) =>
        [.. o.Column.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("day") && b.IsEffectivelyVisible)];

    private static Button Day(Opened o, DateOnly d) => Days(o).Single(b => ((HistoryDayCell)b.DataContext!).Date == d);

    private static void Click(Window w, Control c)
    {
        var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();
    }

    private static Color Resource(Opened o, string key) =>
        ((ISolidColorBrush)Application.Current!.FindResource(o.Window.ActualThemeVariant, key)!).Color;

    [AvaloniaFact]
    public async Task Period_bar_switches_the_map_shape()
    {
        var o = await OpenAsync(seed: (sp, id) => Record(sp, id, new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero)),
            createdAt: new DateTimeOffset(2025, 5, 1, 12, 0, 0, TimeSpan.Zero));
        Days(o).Should().HaveCount(42, "il mese è una griglia 7×6");

        Named<RadioButton>(o.Column, "WeekPeriod").IsChecked = true;
        await UntilAsync(() => Days(o).Count == 7);

        Named<RadioButton>(o.Column, "YearPeriod").IsChecked = true;
        // Lo ScrollViewer era nascosto: il suo contenuto entra nell'albero visuale al primo layout dopo.
        await UntilAsync(() => o.Column.GetVisualDescendants().OfType<ItemsControl>().Any(c => c.Name == "Weeks" && c.ItemCount > 0));
        Days(o).Should().BeEmpty();
        Named<ItemsControl>(o.Column, "Weeks").ItemCount.Should().BeGreaterThanOrEqualTo(52, "l'anno è a colonne di settimana");

        Named<RadioButton>(o.Column, "AllPeriod").IsChecked = true;
        await UntilAsync(() => Named<ItemsControl>(o.Column, "Years").IsVisible);
        var years = Named<ItemsControl>(o.Column, "Years");
        years.ItemCount.Should().Be(2, "dati dal 2025 al 2026: un blocco per anno");
        ((HistoryYear)years.Items[0]!).Year.Should().Be(2026, "dal più recente");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Month_day_numbers_are_12px_and_centered()
    {
        var o = await OpenAsync();
        var number = Day(o, new DateOnly(2026, 10, 15)).GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DayNumber");
        number.Text.Should().Be("15");
        number.FontSize.Should().Be(12);
        number.HorizontalAlignment.Should().Be(HorizontalAlignment.Center);
        number.VerticalAlignment.Should().Be(VerticalAlignment.Center);
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Out_of_month_differs_from_empty()
    {
        var o = await OpenAsync();
        Border Background(DateOnly d) => Day(o, d).GetVisualDescendants().OfType<Border>().Single(b => b.Name == "CellBackground");

        // Ottobre 2026 comincia di giovedì: lunedì 28 settembre è fuori dal mese.
        Background(new DateOnly(2026, 9, 28)).IsVisible.Should().BeFalse("fuori dal mese c'è solo il numero, senza cella");
        Day(o, new DateOnly(2026, 9, 28)).GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DayNumber")
            .Foreground.Should().BeAssignableTo<ISolidColorBrush>().Which.Color.Should().Be(Resource(o, "Brush.TextFaint"));

        var empty = Background(new DateOnly(2026, 10, 5));
        empty.IsVisible.Should().BeTrue();
        ((ISolidColorBrush)empty.Background!).Color.Should().Be(Resource(o, "Brush.Cell"), "un giorno vuoto del mese ha la sua cella");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Selected_is_not_completed()
    {
        var o = await OpenAsync();
        var day = new DateOnly(2026, 10, 20);
        var before = ((ISolidColorBrush)Day(o, day).GetVisualDescendants().OfType<Border>().Single(b => b.Name == "CellBackground").Background!).Color;

        Click(o.Window, Day(o, day));
        await UntilAsync(() => ((HistoryDayCell)Day(o, day).DataContext!).IsSelected);

        var button = Day(o, day);
        ((ISolidColorBrush)button.BorderBrush!).Color.Should().Be(Resource(o, "Brush.Ink"), "la selezione è un bordo d'inchiostro");
        button.BorderThickness.Should().Be(new Thickness(2));
        ((ISolidColorBrush)button.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "CellBackground").Background!).Color
            .Should().Be(before, "la selezione non riempie la cella: non sembra un completamento");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Clicking_a_day_selects_it_for_recording()
    {
        using var _ = TestCulture.Pin();
        var o = await OpenAsync();
        Click(o.Window, Day(o, new DateOnly(2026, 10, 27)));
        await UntilAsync(() => o.Vm.SelectedDay == new DateOnly(2026, 10, 27));
        Named<TextBlock>(o.View, "SelectedDayText").Text.Should().Be("per martedì 27 ottobre");
        Named<TextBlock>(o.Column, "DayHeading").Text.Should().Be("martedì 27 ottobre");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Day_note_is_serif_with_a_dashed_frame()
    {
        var o = await OpenAsync();
        var box = Named<TextBox>(o.Column, "DayNoteBox");
        box.FontFamily.Should().Be(Application.Current!.FindResource("Font.Serif"));
        box.FontSize.Should().Be(14);
        ((Visual)box.Parent!).GetVisualChildren().OfType<Rectangle>().Single().StrokeDashArray.Should().NotBeNullOrEmpty();
        Named<Button>(o.Column, "DayNoteSaveButton").IsVisible.Should().BeFalse("senza modifiche non c'è niente da salvare");

        box.Text = "una nota";
        Pump();
        Named<Button>(o.Column, "DayNoteSaveButton").IsVisible.Should().BeTrue();
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Delete_a_record_from_the_day()
    {
        var o = await OpenAsync(seed: (sp, id) => Record(sp, id, Now.AddHours(-1)));
        var host = Named<Border>(o.Column, "RecordsHost");
        await UntilAsync(() => host.GetVisualDescendants().OfType<Button>().Any(b => b.Name == "DeleteButton"));
        Click(o.Window, host.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "DeleteButton"));
        await UntilAsync(() => o.Completions.ListByHabitAsync(o.Id).AsTask().Result.Count == 0);
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Journal_history_is_rendered()
    {
        var o = await OpenAsync(HabitType.Journal, (sp, id) => sp.GetRequiredService<IDiaryService>()
            .SaveEntryAsync(id, new DateOnly(2026, 10, 27), new Recordum.Core.Models.DiaryEntryDraft("## Al lago\n**fredda**", [], [])).AsTask());
        var list = Named<Border>(o.Column, "ListHost");
        await UntilAsync(() => list.GetVisualDescendants().OfType<Recordum.UI.Controls.DiaryEditor>().Any());
        list.GetVisualDescendants().OfType<Recordum.UI.Controls.DiaryEditor>().Should().OnlyContain(e => e.IsReadOnly);
        o.Column.GetVisualDescendants().OfType<TextBlock>()
            .Should().NotContain(t => t.Text != null && (t.Text.Contains("##") || t.Text.Contains("**")), "la voce è resa, non il Markdown grezzo");
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task With_notes_filter()
    {
        var o = await OpenAsync(seed: async (sp, id) =>
        {
            await Record(sp, id, Now.AddDays(-2), "corsa al parco");
            await Record(sp, id, Now.AddDays(-3));
        });
        var rows = o.Vm.PeriodRecords.Records.Rows;
        await UntilAsync(() => rows.Count == 2);
        Named<RadioButton>(o.Column, "FilterWithNotes").IsChecked = true;
        Pump();
        rows.Should().ContainSingle().Which.Preview.Should().Be("corsa al parco");
        Named<RadioButton>(o.Column, "FilterAll").IsChecked = true;
        Pump();
        rows.Should().HaveCount(2);
        o.Window.Close();
    }

    [AvaloniaFact]
    public async Task Next_is_disabled_on_the_current_period()
    {
        var o = await OpenAsync();
        Named<Button>(o.Column, "NextButton").IsEffectivelyEnabled.Should().BeFalse("il periodo che contiene oggi è l'ultimo");
        Click(o.Window, Named<Button>(o.Column, "PreviousButton"));
        await UntilAsync(() => Named<Button>(o.Column, "NextButton").IsEffectivelyEnabled);
        o.Window.Close();
    }
}
