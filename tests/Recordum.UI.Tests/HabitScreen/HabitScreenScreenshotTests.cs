using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Recordum.Core;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.History;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.HabitScreen;
using Recordum.UI.Views.HabitScreen.Head;
using Recordum.UI.Views.HabitScreen.Notes;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>Onda 2a, Task 10: la schermata dell'abitudine per ciascuno degli otto casi con la Cronologia aperta, e le sue
/// viste (periodi, giorno passato, colonna Note, visore, tutte le colonne, 480 px), in chiaro e in scuro. I PNG finiscono
/// negli artefatti della CI e si guardano; ognuno ha la sua asserzione strutturale (mai il PNG da solo).</summary>
public class HabitScreenScreenshotTests(ITestOutputHelper output)
{
    private static readonly PrivateDbHost Host = new("recordum-ui-shots",
        s => s.AddSingleton<Recordum.Presentation.Services.IConfirmationDialog>(new Yes()));

    private sealed class Yes : Recordum.Presentation.Services.IConfirmationDialog
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
    }

    private static readonly Guid Mood5 = Guid.Parse("00000002-0000-0000-0000-000000000007");
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);
    private static readonly int[] Days = [0, 1, 2, 4, 5, 8, 9, 10, 15, 21, 22];

    private sealed record Opened(IServiceScope Scope, HabitScreenViewModel Vm, HabitScreenView View, Window Window, Guid Id)
        : IDisposable
    {
        public void Dispose() { Window.Close(); Scope.Dispose(); }
    }

    private static HabitType TypeOf(HabitCase c) => c switch
    {
        HabitCase.Action => HabitType.Action,
        HabitCase.State => HabitType.State,
        HabitCase.Abstinence => HabitType.Resistance,
        HabitCase.Journal => HabitType.Journal,
        _ => HabitType.Counter,
    };

    /// <summary>Un'abitudine del caso con tre settimane di registrazioni (l'astinenza: due periodi e una ricaduta).</summary>
    private static async Task<Guid> SeedAsync(IServiceProvider sp, HabitCase c)
    {
        Host.Clock.Now = Now.AddDays(-40);
        var id = await sp.GetRequiredService<IHabitNodeService>().CreateAsync(new HabitNode
        {
            NodeType = NodeType.Habit, HabitType = TypeOf(c), Name = $"Esempio {c}", Color = "#E11D48",
        });
        var metrics = sp.GetRequiredService<IMetricDefinitionService>();
        var configs = sp.GetRequiredService<IHabitConfigService>();
        var defs = new List<MetricDefinition>();
        async Task Metric(string name, MetricType type, int order, Guid? scale = null)
        {
            var m = new MetricDefinition { HabitNodeId = id, Name = name, Type = type, SortOrder = order, ScaleTemplateId = scale };
            await metrics.CreateAsync(m);
            defs.Add(m);
        }
        switch (c)
        {
            case HabitCase.Measured:
                await Metric("pagine", MetricType.Integer, 0);
                await configs.UpsertAsync(id, x => x.Repeatable = false);
                break;
            case HabitCase.Series:
                await Metric("ripetizioni", MetricType.Integer, 0);
                await Metric("kg", MetricType.Decimal, 1);
                break;
            case HabitCase.Session:
                await Metric("tempo", MetricType.Seconds, 0);
                await configs.UpsertAsync(id, x => { x.HasIntegratedTimer = true; x.TimerMode = TimerMode.Session; x.SessionBaseTimer = true; });
                break;
            case HabitCase.State:
                await Metric("umore", MetricType.Integer, 0, Mood5);
                break;
        }

        if (c == HabitCase.Abstinence)
        {
            var abstinence = sp.GetRequiredService<IAbstinenceService>();
            await abstinence.StartAsync(id);
            Host.Clock.Now = Now.AddDays(-12);
            await abstinence.LapseAsync(id);
            Host.Clock.Now = Now.AddDays(-11);
            await abstinence.StartAsync(id);
        }
        else if (c == HabitCase.Journal)
        {
            Host.Clock.Now = Now;
            foreach (var d in Days.Take(5))
                await sp.GetRequiredService<IDiaryService>().SaveEntryAsync(id, DateOnly.FromDateTime(Now.AddDays(-d).Date),
                    $"Giornata {d}: **lenta**, poi una passeggiata.");
        }
        else
        {
            Host.Clock.Now = Now;
            foreach (var d in Days)
            {
                var at = Now.AddDays(-d).AddHours(-1);
                var values = defs.Select(m => m.Type switch
                {
                    MetricType.Seconds => new CompletionValue { MetricDefinitionId = m.Id, ValueSeconds = 1200 + 60 * d },
                    MetricType.Decimal => new CompletionValue { MetricDefinitionId = m.Id, ValueDecimal = 40 + d % 3 * 2.5m },
                    _ when m.ScaleTemplateId is not null => new CompletionValue { MetricDefinitionId = m.Id, ValueInteger = 1 + d % 5 },
                    _ => new CompletionValue { MetricDefinitionId = m.Id, ValueInteger = 10 + d },
                }).ToList();
                await sp.GetRequiredService<ICompletionService>().RecordAsync(new Completion
                {
                    HabitNodeId = id, CompletedAt = at.UtcDateTime, CompletedAtMs = at.ToUnixTimeMilliseconds(),
                }, values.Count > 0 ? values : null);
            }
        }
        Host.Clock.Now = Now;
        return id;
    }

    private static async Task<Opened> OpenAsync(HabitCase c, string variant, double width = 1000,
        Action<HabitScreenViewModel>? columns = null, Func<IServiceProvider, Guid, Task>? extra = null)
    {
        var scope = Host.Provider.CreateScope();
        var sp = scope.ServiceProvider;
        await HabitScreenColumnsTests.ResetColumnStateAsync(scope);
        await sp.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", "00:00");
        var id = await SeedAsync(sp, c);
        if (extra is not null) await extra(sp, id);
        var vm = sp.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(id);
        vm.IsCronologiaOpen = true;
        columns?.Invoke(vm);
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window
        {
            Width = width, Height = 1000, Content = view,
            RequestedThemeVariant = variant == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        w.Show();
        await UntilAsync(() => vm.Map.Cells.Count > 0 || vm.Map.Weeks.Count > 0 || vm.Map.Years.Count > 0);
        await Task.Delay(200);   // i marcatori arrivano dopo le celle
        PrivateDbHost.Pump();
        return new Opened(scope, vm, view, w, id);
    }

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 150 && !done(); i++) { await Task.Delay(20); PrivateDbHost.Pump(); }
        done().Should().BeTrue("la condizione doveva avverarsi entro il tempo");
    }

    private static T Named<T>(Avalonia.Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(x => x.Name == name);

    // Lo ScrollViewer della heatmap era nascosto: il suo contenuto entra nell'albero visuale al primo layout dopo.
    private static bool Visible(Opened o, string name) =>
        o.View.GetVisualDescendants().OfType<Control>().Any(x => x.Name == name && x.IsEffectivelyVisible && x.Bounds.Width > 0);

    private void Shoot(Opened o, string name) => output.WriteLine(ScreenshotProbeTests.Capture(o.Window, name));

    [AvaloniaTheory]
    [InlineData(HabitCase.Action, "Light")]
    [InlineData(HabitCase.Action, "Dark")]
    [InlineData(HabitCase.CounterTap, "Light")]
    [InlineData(HabitCase.CounterTap, "Dark")]
    [InlineData(HabitCase.Measured, "Light")]
    [InlineData(HabitCase.Measured, "Dark")]
    [InlineData(HabitCase.Series, "Light")]
    [InlineData(HabitCase.Series, "Dark")]
    [InlineData(HabitCase.Session, "Light")]
    [InlineData(HabitCase.Session, "Dark")]
    [InlineData(HabitCase.State, "Light")]
    [InlineData(HabitCase.State, "Dark")]
    [InlineData(HabitCase.Abstinence, "Light")]
    [InlineData(HabitCase.Abstinence, "Dark")]
    [InlineData(HabitCase.Journal, "Light")]
    [InlineData(HabitCase.Journal, "Dark")]
    public async Task Each_case_with_the_log_open(HabitCase c, string variant)
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync(c, variant);
        Shoot(o, $"habit-{c.ToString().ToLowerInvariant()}-{variant.ToLowerInvariant()}");

        o.Vm.Case.Should().Be(c);
        Named<TextBlock>(o.View, "HabitName").Text.Should().Be($"Esempio {c}");
        Named<Border>(o.View, "CommandHost").Child.Should().NotBeNull("il comando del caso");
        o.View.GetVisualDescendants().OfType<CronologiaColumnView>().Single().IsEffectivelyVisible.Should().BeTrue();
        o.Vm.Period.Period.Should().Be(HistoryPeriod.Month);
        o.Vm.Map.Cells.Should().HaveCount(42, "il mese è una griglia 7×6");
        if (c is HabitCase.State)
            o.View.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "StepLabel")
                .Should().OnlyContain(t => !t.TextLayout.TextLines.Any(l => l.HasCollapsed), "le etichette dei gradini non si troncano");
        if (c is HabitCase.Abstinence)
        {
            // GIO-9: la ricaduta è una cella vuota col contorno e una × nel colore dell'abitudine, il numero in angolo.
            o.Vm.Map.Cells.Should().Contain(x => x.IsLapse, "la ricaduta è segnata");
            o.Vm.Map.Cells.Where(x => x.IsLapse).Should().OnlyContain(x => x.Step == 0, "il giorno di una ricaduta non è pulito");
            var marks = o.View.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>()
                .Where(p => p.Name == "LapseMark" && p.IsEffectivelyVisible).ToList();
            marks.Should().HaveCount(o.Vm.Map.Cells.Count(x => x.IsLapse));
            var mark = marks[0];
            var cell = mark.FindAncestorOfType<Button>()!;
            var outline = (Border)mark.Parent!;
            outline.BorderThickness.Should().Be(new Thickness(1.5));
            var expected = ((ISolidColorBrush)Recordum.UI.Converters.HabitTextBrushConverter.BrushFor(o.Vm.Node!.Color, o.View.ActualThemeVariant)).Color;
            ((ISolidColorBrush)outline.BorderBrush!).Color.Should().Be(expected, "il contorno è nel colore dell'abitudine");
            ((ISolidColorBrush)mark.Stroke!).Color.Should().Be(expected, "la × è nel colore dell'abitudine");
            mark.Bounds.Width.Should().BeGreaterThan(8, "la × è grande, al centro");
            var number = cell.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DayNumber");
            var at = number.TranslatePoint(new Point(0, 0), cell)!.Value;
            at.X.Should().BeLessThan(10, "il numero sta in alto a sinistra");
            at.Y.Should().BeLessThan(8);
            (at.X + number.Bounds.Width).Should().BeLessThan(mark.TranslatePoint(new Point(0, 0), cell)!.Value.X + 1, "il numero non copre la ×");
            o.View.GetVisualDescendants().OfType<Border>().Should().NotContain(b => b.Name == "LapseNotch", "la tacca non c'è più");
        }
        else o.Vm.Map.Cells.Count(x => x.Step > 0).Should().BeGreaterThan(3, "la mappa mostra i giorni registrati");
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task Periods_of_an_action(string variant)
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync(HabitCase.Action, variant);

        o.Vm.Period.Period = HistoryPeriod.Week;
        await UntilAsync(() => o.Vm.Map.Cells.Count == 7);
        Shoot(o, $"habit-period-week-{variant.ToLowerInvariant()}");

        o.Vm.Period.Period = HistoryPeriod.Year;
        await UntilAsync(() => o.Vm.Map.Weeks.Count >= 52 && Visible(o, "Weeks"));
        PrivateDbHost.Pump();
        Shoot(o, $"habit-period-year-{variant.ToLowerInvariant()}");

        o.Vm.Period.Period = HistoryPeriod.All;
        await UntilAsync(() => o.Vm.Map.Years.Count == 1 && Visible(o, "Years"));
        PrivateDbHost.Pump();
        Shoot(o, $"habit-period-all-{variant.ToLowerInvariant()}");
        o.Vm.Map.Years[0].Year.Should().Be(2026);
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task A_past_day_chosen(string variant)
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync(HabitCase.Measured, variant);
        var day = new DateOnly(2026, 10, 23);
        await o.Vm.SelectDayAsync(day);
        await UntilAsync(() => o.Vm.Map.Cells.Single(x => x.Date == day).IsSelected
                               && Named<Border>(o.View, "RecordsHost").Child is not null);
        PrivateDbHost.Pump();
        Shoot(o, $"habit-past-day-{variant.ToLowerInvariant()}");

        Named<TextBlock>(o.View, "DayHeading").IsEffectivelyVisible.Should().BeTrue("non è oggi");
        Named<TextBlock>(o.View, "DayHeadingToday").IsEffectivelyVisible.Should().BeFalse();
    }

    private static async Task NoteWithPhoto(IServiceProvider sp, Guid id)
    {
        var note = new Note
        {
            HabitNodeId = id, TargetDate = new DateOnly(2026, 10, 28), UpdatedAtMs = Host.Clock.UtcNowMs,
            BodyMd = "## Lungo il fiume\nGambe **pesanti**, testa libera.\n\n- 14 km\n- ritmo 5:40",
        };
        await sp.GetRequiredService<INoteService>().CreateAsync(note);
        var png = JournalFixtures.WritePng(160, 120);
        try
        {
            var photo = await sp.GetRequiredService<IPhotoImporter>().ImportAsync(png, CancellationToken.None);
            await sp.GetRequiredService<IAttachmentService>().AddAsync(note.Id, photo, CancellationToken.None);
        }
        finally { File.Delete(png); }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task The_notes_column_and_the_viewer(string variant)
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync(HabitCase.Action, variant, 1100, vm => vm.IsNoteOpen = true, NoteWithPhoto);
        var notes = o.Vm.Notes;
        await UntilAsync(() => notes.Notes.Count == 1);
        notes.SelectedNote = notes.Notes[0];
        var column = o.View.GetVisualDescendants().OfType<NotesColumnView>().Single();
        await UntilAsync(() => Named<ItemsControl>(column, "Attachments").GetVisualDescendants().OfType<Image>().Any(i => i.Source is not null));
        PrivateDbHost.Pump();
        Shoot(o, $"habit-notes-{variant.ToLowerInvariant()}");
        Named<Recordum.UI.Controls.DiaryEditor>(column, "NoteEditor").Markdown.Should().StartWith("## Lungo il fiume");

        notes.OpenLightboxCommand.Execute(notes.Attachments[0]);
        await UntilAsync(() => Named<Image>(column, "LightboxImage").Source is not null);
        PrivateDbHost.Pump();
        Shoot(o, $"habit-notes-viewer-{variant.ToLowerInvariant()}");
        Named<Control>(column, "Lightbox").IsEffectivelyVisible.Should().BeTrue();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task All_columns_open(string variant)
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync(HabitCase.Journal, variant, 1400, vm =>
        {
            vm.IsDiarioOpen = true;
            vm.IsNoteOpen = true;
            vm.IsConfiguraOpen = true;
        });
        // Un diario non ha la colonna Note (Task 13): le sue colonne sono tre, anche con Note nell'insieme aperto.
        await UntilAsync(() => Named<Border>(o.View, "ConfiguraColumn").Bounds.Width >= 320);
        PrivateDbHost.Pump();
        Shoot(o, $"habit-all-columns-{variant.ToLowerInvariant()}");
        foreach (var name in new[] { "DiarioColumn", "CronologiaColumn", "ConfiguraColumn" })
            Named<Border>(o.View, name).Bounds.Width.Should().BeGreaterThanOrEqualTo(320, name);
        Named<Border>(o.View, "NoteColumn").IsVisible.Should().BeFalse();
        o.View.GetVisualDescendants().OfType<NotesColumnView>().Should().BeEmpty();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public async Task At_480_px(string variant)
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync(HabitCase.Series, variant, 480);
        Shoot(o, $"habit-480-{variant.ToLowerInvariant()}");
        var scroll = Named<ScrollViewer>(o.View, "ColumnsScroll");
        Named<Border>(o.View, "CronologiaColumn").Bounds.Width.Should().BeGreaterThanOrEqualTo(320);
        Named<HabitHeadView>(o.View, "Head").Bounds.Width.Should().BeLessThanOrEqualTo(480, "la testa non esce dalla finestra");
        scroll.Viewport.Width.Should().BeLessThanOrEqualTo(480);
        o.View.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "FigureCaption")
            .Should().OnlyContain(t => !t.TextLayout.TextLines.Any(l => l.HasCollapsed), "le didascalie dei numeri non si troncano");
    }
}
