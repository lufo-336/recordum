using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
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
using Recordum.UI.Views.HabitScreen.Group;
using Recordum.UI.Views.HabitScreen.History;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests.HabitScreen;

/// <summary>La schermata del gruppo (onda 2b): la testa con l'avanzamento, la colonna Panoramica (Oggi, Insieme, la mappa).
/// DB privato, oggi mercoledì 28/10/2026 alle 10:00, reset a mezzanotte.</summary>
public class GroupScreenTests(ITestOutputHelper output)
{
    private static readonly PrivateDbHost Host = new("recordum-ui-group");
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);
    private const string Color = "#2F8FB0";

    private sealed record Opened(IServiceScope Scope, HabitScreenViewModel Vm, HabitScreenView View, Window Window, Guid GroupId,
        Guid A, Guid B, Guid? C) : IDisposable
    {
        public GroupHeadView Head => View.GetVisualDescendants().OfType<GroupHeadView>().Single();
        public PanoramicaColumnView Column => View.GetVisualDescendants().OfType<PanoramicaColumnView>().Single();
        public void Dispose() => Window.Close();
    }

    /// <summary>Gruppo con due azioni A e B (A fatta oggi se <paramref name="aDone"/>); con <paramref name="rich"/> anche un
    /// sottogruppo S con uno stato C dentro.</summary>
    private static async Task<Opened> OpenAsync(bool aDone = true, bool rich = false, bool empty = false, double width = 1000)
    {
        Host.Clock.Now = Now;
        var scope = Host.Provider.CreateScope();
        var sp = scope.ServiceProvider;
        await sp.GetRequiredService<IAppSettingService>().SetAsync("daily_reset_time", "00:00");
        var nodes = sp.GetRequiredService<IHabitNodeService>();
        async Task<Guid> Make(string name, NodeType nt, HabitType? ht, Guid? parent) => await nodes.CreateAsync(new HabitNode
        {
            NodeType = nt, HabitType = ht, Name = $"{name} {Guid.NewGuid():N}"[..14], Color = Color, ParentId = parent,
        });
        var group = await Make("Gruppo", NodeType.Group, null, null);
        Guid a = default, b = default; Guid? c = null;
        if (!empty)
        {
            a = await Make("Acqua", NodeType.Habit, HabitType.Action, group);
            b = await Make("Stretching", NodeType.Habit, HabitType.Action, group);
            if (rich)
            {
                var s = await Make("Sera", NodeType.Group, null, group);
                c = await Make("Umore", NodeType.Habit, HabitType.State, s);
            }
            if (aDone)
                await sp.GetRequiredService<ICompletionService>().RecordAsync(new Completion
                { HabitNodeId = a, CompletedAt = Now.UtcDateTime, CompletedAtMs = Now.ToUnixTimeMilliseconds() });
        }
        var vm = sp.GetRequiredService<HabitScreenViewModel>();
        await vm.LoadAsync(group);
        vm.IsPanoramicaOpen = true;
        var view = new HabitScreenView { DataContext = vm };
        var w = new Window { Width = width, Height = 1500, Content = view, RequestedThemeVariant = ThemeVariant.Light };
        w.Show();
        Pump();
        var o = new Opened(scope, vm, view, w, group, a, b, c);
        if (!empty) await UntilAsync(() => o.Column.GetVisualDescendants().OfType<HabitRow>().Any() && o.Vm.GroupFigures!.Items.Length > 0);
        Pump();
        return o;
    }

    private static void Pump() => PrivateDbHost.Pump();

    private static async Task UntilAsync(Func<bool> done)
    {
        for (var i = 0; i < 150 && !done(); i++) { await Task.Delay(20); Pump(); }
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

    private void Shoot(Opened o, string name)
    {
        Pump();
        output.WriteLine(ScreenshotProbeTests.Capture(o.Window, name));
    }

    private static Button QuickLog(Opened o, Guid id) => o.Column.GetVisualDescendants().OfType<HabitRow>()
        .Single(r => ((Recordum.Presentation.ViewModels.HomeRowViewModel)r.DataContext!).HabitNodeId == id)
        .GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("quicklog"));

    [AvaloniaFact]
    public async Task Group_head_shows_progress_and_figures()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync();
        Named<TextBlock>(o.Head, "ProgressCount").Text.Should().Be("1 di 2");
        Named<TextBlock>(o.Head, "ProgressCaption").Text.Should().Be("fatte oggi");
        var bar = Named<ProgressBar>(o.Head, "ProgressBar");
        var fill = bar.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Indicator");
        bar.Bounds.Width.Should().BeInRange(50, 150.5, "la barra sta nel blocco di 150 px, non oltre il margine");
        fill.Bounds.Width.Should().BeApproximately(bar.Bounds.Width / 2, 1.5, "1 di 2: la barra è a metà");
        Named<TextBlock>(o.Head, "Subtitle").Text.Should().Be("2 abitudini · la giornata comincia alle 00:00");
        o.Head.GetVisualDescendants().OfType<ItemsControl>().Single(i => i.Name == "Figures").ItemCount.Should().BeGreaterThan(0);
        Shoot(o, "group-head-light");
    }

    [AvaloniaFact]
    public async Task Group_head_has_no_record_command()
    {
        using var o = await OpenAsync();
        o.Head.GetVisualDescendants().OfType<Button>().Should().NotContain(b => b.Classes.Contains("record"));
        o.Head.GetVisualDescendants().OfType<ToggleButton>().Where(t => t.IsVisible).Select(t => t.Name)
            .Should().Equal("ChipPanoramica", "ChipNote", "ChipConfigura");
    }

    [AvaloniaFact]
    public async Task Overview_rows_are_home_rows()
    {
        using var o = await OpenAsync(rich: true);
        o.Column.GetVisualDescendants().OfType<HabitRow>().Count().Should().Be(3);
        o.Column.GetVisualDescendants().OfType<GroupRow>().Count().Should().Be(1);
        Shoot(o, "group-overview-light");
    }

    [AvaloniaFact]
    public async Task Quick_log_from_the_group_marks_the_child()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync(aDone: true);
        Click(o.Window, QuickLog(o, o.B));
        await UntilAsync(() => Named<TextBlock>(o.Head, "ProgressCount").Text == "2 di 2");
        var fill = Named<ProgressBar>(o.Head, "ProgressBar").GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Indicator");
        fill.Bounds.Width.Should().BeApproximately(Named<ProgressBar>(o.Head, "ProgressBar").Bounds.Width, 1.5);
    }

    [AvaloniaFact]
    public async Task Clicking_a_form_child_opens_it()
    {
        using var o = await OpenAsync(rich: true);
        Guid? opened = null;
        o.Vm.Panoramica.ChildSelected += (_, id) => opened = id;
        Click(o.Window, QuickLog(o, o.C!.Value));
        await UntilAsync(() => opened is not null);
        opened.Should().Be(o.C);
    }

    [AvaloniaFact]
    public async Task Clicking_a_row_name_opens_the_child()
    {
        using var o = await OpenAsync();
        Guid? opened = null;
        o.Vm.Panoramica.ChildSelected += (_, id) => opened = id;
        var name = o.Column.GetVisualDescendants().OfType<HabitRow>().First(r => ((Recordum.Presentation.ViewModels.HomeRowViewModel)r.DataContext!).HabitNodeId == o.B)
            .GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.StartsWith("Stretching", StringComparison.Ordinal) == true);
        Click(o.Window, name);
        await UntilAsync(() => opened is not null);
        opened.Should().Be(o.B);
    }

    [AvaloniaFact]
    public async Task A_row_in_the_group_column_has_no_menu()
    {
        using var o = await OpenAsync();
        var row = o.Column.GetVisualDescendants().OfType<HabitRow>().First();
        var host = Named<Control>(row, "MenuHost");
        host.RaiseEvent(new ContextRequestedEventArgs());
        Pump();
        host.ContextMenu.Should().BeNull("il menu della riga lo dà la Home");
    }

    [AvaloniaFact]
    public async Task Together_has_a_row_per_child()
    {
        using var o = await OpenAsync(rich: true);
        await UntilAsync(() => Named<TogetherGrid>(o.Column, "Together").RowCount == 3);
        var grid = Named<TogetherGrid>(o.Column, "Together");
        grid.Bounds.Height.Should().BeGreaterThan(3 * TogetherGrid.RowMin - 1);
        Shoot(o, "group-together-year-light");
    }

    [AvaloniaFact]
    public async Task Together_follows_the_period()
    {
        using var o = await OpenAsync(rich: true);
        var grid = Named<TogetherGrid>(o.Column, "Together");
        await UntilAsync(() => grid.RowCount == 3);
        Named<RadioButton>(o.Column, "WeekPeriod").IsChecked = true;
        await UntilAsync(() => grid.Rows![0].Cells.Count == 7);
        grid.Rows!.Should().OnlyContain(r => r.Cells.Count == 7);
        grid.ShowHeader.Should().BeTrue();
        grid.CellSize.Should().Be(TogetherGrid.MaxCell);
        Shoot(o, "group-together-week-light");
        Named<RadioButton>(o.Column, "MonthPeriod").IsChecked = true;
        await UntilAsync(() => grid.Rows![0].Cells.Count == 31);
        grid.ShowHeader.Should().BeTrue();
        Shoot(o, "group-together-month-light");
        Named<RadioButton>(o.Column, "YearPeriod").IsChecked = true;
        await UntilAsync(() => grid.Rows![0].Cells.Count == 365);
        grid.ShowHeader.Should().BeFalse();
        grid.CellSize.Should().BeLessThan(TogetherGrid.MaxCell);
    }

    [AvaloniaFact]
    public async Task Together_cells_never_shrink_below_4px()
    {
        using var o = await OpenAsync(rich: true, width: 460);
        var grid = Named<TogetherGrid>(o.Column, "Together");
        await UntilAsync(() => grid.Days == 365);
        grid.CellSize.Should().BeGreaterThanOrEqualTo(4, "(460-140)/365 sarebbe 0,9 px: si scorre, non si rimpicciolisce");
        var cells = grid.Scroll.Content as Control;
        cells!.Bounds.Width.Should().BeGreaterThanOrEqualTo(365 * 4);
        Shoot(o, "group-together-year-narrow-light");
        o.Window.RequestedThemeVariant = ThemeVariant.Dark;
        Pump();
        Shoot(o, "group-together-year-narrow-dark");
    }

    [AvaloniaFact]
    public async Task Together_long_periods_scroll_to_today()
    {
        using var o = await OpenAsync(rich: true);
        var grid = Named<TogetherGrid>(o.Column, "Together");
        await UntilAsync(() => grid.Days == 365);
        await UntilAsync(() => grid.Scroll.Extent.Width > grid.Scroll.Viewport.Width);
        await UntilAsync(() => grid.Scroll.Offset.X > 0);
        var pitch = grid.CellSize + grid.CellGap;
        var today = grid.Rows![0].Cells.ToList().FindIndex(c => c.IsToday);
        var x = today * pitch;
        x.Should().BeInRange(grid.Scroll.Offset.X, grid.Scroll.Offset.X + grid.Scroll.Viewport.Width - grid.CellSize, "oggi sta nella finestra");
        ((today + 1) * pitch).Should().BeApproximately(grid.Scroll.Offset.X + grid.Scroll.Viewport.Width, pitch + 1, "oggi è all'estremo destro");
    }

    [AvaloniaFact]
    public async Task Group_map_is_the_year_by_default()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync();
        o.Vm.Period.Period.Should().Be(HistoryPeriod.Year);
        Named<TextBlock>(o.Column, "MapTitle").Text.Should().Be("2026");
        var map = Named<HistoryMapView>(o.Column, "Map");
        await UntilAsync(() => o.Vm.Panoramica.Map.Weeks.Count >= 52);
        map.Selectable.Should().BeFalse("scegliere un giorno non fa niente per un gruppo");
        var grid = map.GetVisualDescendants().OfType<HeatGrid>().Single(g => g.Name == "Weeks");
        grid.Selectable.Should().BeFalse();
        grid.Cursor.Should().BeNull();
        grid.Focusable.Should().BeFalse();
        Named<TextBlock>(o.Column, "MapLegend").Text.Should().Be("intensità = quante figlie quel giorno; pieno = tutte");
        Shoot(o, "group-map-year-light");
    }

    [AvaloniaFact]
    public async Task Empty_group_says_so()
    {
        using var culture = TestCulture.Pin();
        using var o = await OpenAsync(empty: true);
        Named<TextBlock>(o.Column, "EmptyText").IsEffectivelyVisible.Should().BeTrue();
        Named<ItemsControl>(o.Column, "TodayRows").IsVisible.Should().BeFalse();
        o.Column.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Classes.Contains("primary")).Should().BeEmpty();
        Named<TextBlock>(o.Head, "ProgressCount").Text.Should().Be("0 di 0");
        Shoot(o, "group-empty-light");
    }

    [AvaloniaFact]
    public async Task Narrow_group_head_has_no_overlap()
    {
        using var o = await OpenAsync(width: 480);
        Rect R(Control c) => new(c.TranslatePoint(new Point(0, 0), o.Window)!.Value, c.Bounds.Size);
        var parts = new Control[] { Named<TextBlock>(o.Head, "HabitName"), Named<TextBlock>(o.Head, "Subtitle"), Named<StackPanel>(o.Head, "Progress"),
            Named<ItemsControl>(o.Head, "Figures"), Named<Panel>(o.Head, "ColumnButtons") };
        for (var i = 0; i < parts.Length; i++)
            for (var j = i + 1; j < parts.Length; j++)
                R(parts[i]).Intersects(R(parts[j])).Should().BeFalse($"{parts[i].Name} e {parts[j].Name} non si sovrappongono");
        R(parts[2]).Right.Should().BeLessThanOrEqualTo(480 + 0.5);
        Shoot(o, "group-head-narrow-light");
    }
}
