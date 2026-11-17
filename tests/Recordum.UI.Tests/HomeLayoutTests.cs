using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Quick;
using Recordum.UI.Controls;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Home;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>La Home della direzione visiva (§5): "Oggi" con la data, l'intestazione dei giorni con oggi a destra e
/// allineata alle strisce, il log rapido su ogni riga, i gruppi con l'avanzamento, niente sovrapposizioni da stretta.</summary>
public class HomeLayoutTests
{
    private static readonly Guid GroupId = Guid.NewGuid();

    private static HomeTreeRow Row(string name, HabitType? type = HabitType.Action, Guid? parent = null, int days = 7, bool done = false)
    {
        var counts = Enumerable.Range(0, days).Select(i => i % 2).ToArray();
        var last7 = days >= 7 ? counts[^7..] : counts;
        return new HomeTreeRow(Guid.NewGuid(), parent, name, "#2F7FC1", type, null, 0, false, PinScope.None, 0, false,
            ImmutableArray.Create(last7), ImmutableArray<Guid>.Empty, CompletionsLast14Days: ImmutableArray.Create(counts))
        { IsCompletedToday = done };
    }

    private static (Window W, HomeView V, HomeViewModel Vm) Show(double width, params HomeTreeRow[] rows)
    {
        var query = new FakeHabitTreeQueryService();
        query.Rows.AddRange(rows);
        var vm = new HomeViewModel(query, new FakeHabitNodeService(), new FakePurgeService(),
            quickActions: new QuickActionHandlerSelector([]),
            clock: new PrivateDbHost.FakeClock { Now = new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero) });
        var view = new HomeView { DataContext = vm };
        var w = new Window { Width = width, Height = 600, Content = view };
        w.Show();
        vm.LoadCommand.Execute(null);
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        Dispatcher.UIThread.RunJobs();
        return (w, view, vm);
    }

    private static Rect BoundsIn(Visual v, Visual root) =>
        new(v.TranslatePoint(new Point(0, 0), root)!.Value, v.Bounds.Size);

    private static void Pump()
    {
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window w, Control target)
    {
        var p = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), w)!.Value;
        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Pump();
    }

    /// <summary>I pulsanti dei giorni dell'intestazione, dal più vecchio a oggi.</summary>
    private static List<Button> HeaderDays(HomeView view) =>
        view.FindControl<DayHeaderView>("DayHeader")!.GetVisualDescendants().OfType<Button>().ToList();

    private static List<TextBlock> StripCells(HabitRow row) =>
        row.GetVisualDescendants().OfType<HeatmapStrip>().Single().GetVisualDescendants().OfType<TextBlock>().ToList();

    private static List<Border> MarkedCells(HabitRow row) =>
        row.GetVisualDescendants().OfType<HeatmapStrip>().Single().GetVisualDescendants().OfType<Border>()
            .Where(b => b.Name == HeatmapStrip.SelectedCellName).ToList();

    [AvaloniaFact]
    public void Header_reads_Oggi_with_the_date()
    {
        using var culture = TestCulture.Pin();
        var (w, view, _) = Show(900, Row("Acqua"));
        view.FindControl<TextBlock>("TodayTitle")!.Text.Should().Be("Oggi");
        view.FindControl<TextBlock>("TodayDate")!.Text.Should().Be("mercoledì 28 ottobre");
        w.Close();
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_done_filter_icon_reads_as_an_active_control_when_on(string variant)
    {
        // Misurato: con l'icona attenuata su Brush.Accent il contrasto stava sotto i 3:1 di un controllo attivo.
        var (w, view, _) = Show(900, Row("Acqua"));
        w.RequestedThemeVariant = variant == "Dark" ? Avalonia.Styling.ThemeVariant.Dark : Avalonia.Styling.ThemeVariant.Light;
        var toggle = view.FindControl<Avalonia.Controls.Primitives.ToggleButton>("DoneToggle")!;
        var icon = toggle.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
        foreach (var on in new[] { false, true })
        {
            toggle.IsChecked = on;
            for (var i = 0; i < 2; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
            var fill = ((ISolidColorBrush)toggle.Background!).Color;
            var stroke = ((ISolidColorBrush)icon.Stroke!).Color;
            Recordum.UI.Theming.HabitColors.Contrast(stroke, fill).Should().BeGreaterThanOrEqualTo(3, $"{variant}, acceso={on}");
        }
        w.Close();
    }

    [AvaloniaFact]
    public void Today_column_is_aligned_across_strip_lengths()
    {
        // Review Focus 4: 7 e 14 giorni, un gruppo senza striscia, una figlia e una nipote in un sottogruppo. GIO-2: le
        // card finiscono tutte allo stesso x a ogni profondità, quindi ogni "oggi" sta sotto "oggi" entro 1 px.
        using var culture = TestCulture.Pin();
        var sub = Guid.NewGuid();
        var (w, view, _) = Show(900,
            Row("Sette"), Row("Quattordici", HabitType.Resistance, days: 14),
            Row("Gruppo", type: null) with { HabitNodeId = GroupId }, Row("Figlia", parent: GroupId),
            Row("Sottogruppo", type: null, parent: GroupId) with { HabitNodeId = sub }, Row("Nipote", parent: sub));
        var today = HeaderDays(view).Last();
        today.Content.Should().Be("oggi");
        var column = BoundsIn(today, view);

        var strips = view.GetVisualDescendants().OfType<HabitRow>()
            .Select(r => r.GetVisualDescendants().OfType<HeatmapStrip>().Single()).ToList();
        strips.Should().HaveCount(4);
        foreach (var strip in strips)
        {
            var cell = BoundsIn(strip.GetVisualDescendants().OfType<TextBlock>().Last(), view);
            cell.Center.X.Should().BeApproximately(column.Center.X, 1.0, "l'ultima cella di ogni striscia è sotto \"oggi\"");
        }
        w.Close();
    }

    [AvaloniaFact]
    public void A_done_action_has_a_tinted_quick_log_button()
    {
        // GIO-4 (variante A2 scelta dall'autore il 2026-11-17): solo un'azione fatta nel giorno scelto ha il pulsante tinto
        // col bordo pieno; una non fatta e gli altri tipi restano a contorno sul fondo del pannello.
        var (w, view, _) = Show(900,
            Row("Fatta", done: true), Row("Da fare"), Row("Senza fumo", HabitType.Resistance, done: true));
        var buttons = view.GetVisualDescendants().OfType<HabitRow>()
            .ToDictionary(r => ((HomeRowViewModel)r.DataContext!).Name, r => r.FindControl<Button>("QuickLog")!);
        var habit = Color.Parse("#2F7FC1");

        var done = buttons["Fatta"];
        ((ISolidColorBrush)done.Background!).Color.Should().Be(Color.FromArgb(48, habit.R, habit.G, habit.B));
        ((ISolidColorBrush)done.BorderBrush!).Color.Should().Be(habit);

        var pane = ((ISolidColorBrush)buttons["Da fare"].Background!).Color;
        pane.A.Should().Be(255, "a contorno sul fondo opaco del pannello");
        ((ISolidColorBrush)buttons["Senza fumo"].Background!).Color.Should().Be(pane, "la ricaduta non cambia");
        w.Close();
    }

    [AvaloniaFact]
    public void Narrow_home_has_no_overlap()
    {
        // Review Focus 3 e F2b-5: a 280 px "+ Nuova" finiva sopra "Archiviate".
        using var culture = TestCulture.Pin();
        var (w, view, _) = Show(280, Row("Un nome davvero molto lungo per una riga stretta"));
        var parts = new[] { "TodayTitle", "PrevDayButton", "NextDayButton", "SearchButton", "DoneToggle", "SortBox", "MoreButton", "NewMenuButton" }
            .Select(n => (Name: n, Rect: BoundsIn(view.FindControl<Control>(n)!, view))).ToList();
        foreach (var a in parts)
            foreach (var b in parts.Where(p => p.Name != a.Name))
                a.Rect.Intersects(b.Rect).Should().BeFalse($"{a.Name} e {b.Name} si sovrappongono");
        parts.Should().OnlyContain(p => p.Rect.Right <= 280 + 0.5, "niente esce dalla colonna");
        view.FindControl<TextBlock>("TodayDate")!.IsVisible.Should().BeFalse("sotto 320 px la data si nasconde");

        var name = view.GetVisualDescendants().OfType<HabitRow>().Single()
            .GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.StartsWith("Un nome", StringComparison.Ordinal) == true);
        name.TextTrimming.Should().Be(TextTrimming.CharacterEllipsis);
        name.Bounds.Width.Should().BeGreaterThan(90, "anche a 280 px il nome si legge");
        w.Close();
    }

    [AvaloniaFact]
    public void A_wide_home_shows_fourteen_days_a_middle_one_seven_and_a_narrow_one_four()
    {
        var (w, view, _) = Show(900, Row("Quattordici", HabitType.Resistance, days: 14));
        HeaderDays(view).Should().HaveCount(14);
        w.Width = 460;
        for (var i = 0; i < 2; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        HeaderDays(view).Should().HaveCount(7);
        view.GetVisualDescendants().OfType<HabitRow>().Single().GetVisualDescendants().OfType<HeatmapStrip>().Single()
            .GetVisualDescendants().OfType<TextBlock>().Should().HaveCount(7);

        // A 280 px (il minimo) sette colonne lasciavano al nome venti pixel: se ne vedono quattro (visto nel PNG).
        w.Width = 280;
        for (var i = 0; i < 2; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        HeaderDays(view).Should().HaveCount(4);
        w.Close();
    }

    [AvaloniaFact]
    public void Every_row_has_its_quick_log_with_the_icon_of_its_type()
    {
        var (w, view, _) = Show(900, Row("Corsa"), Row("Acqua", HabitType.Counter), Row("Senza fumo", HabitType.Resistance, days: 14));
        var buttons = view.GetVisualDescendants().OfType<HabitRow>()
            .Select(r => r.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("quicklog"))).ToList();
        buttons.Should().HaveCount(3).And.OnlyContain(b => b.IsVisible);
        var icons = buttons.Select(b => b.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single().Data).ToList();
        icons.Should().OnlyHaveUniqueItems("azione, contatore e astinenza hanno icone diverse");
        w.Close();
    }

    [AvaloniaFact]
    public void Group_row_shows_progress()
    {
        using var culture = TestCulture.Pin();
        var (w, view, _) = Show(900,
            Row("Mattina", type: null) with { HabitNodeId = GroupId },
            Row("Acqua", parent: GroupId, done: true), Row("Corsa", parent: GroupId));
        view.GetVisualDescendants().OfType<GroupRow>().Single()
            .GetVisualDescendants().OfType<TextBlock>().Should().Contain(t => t.Text == "1 di 2");
        w.Close();
    }

    [AvaloniaFact]
    public void Row_menu_items_have_icons()
    {
        var (w, view, _) = Show(900, Row("Corsa"));
        var row = view.GetVisualDescendants().OfType<HabitRow>().Single();
        var menu = (ContextMenu)row.Resources["RowMenu"]!;
        menu.Items.OfType<MenuItem>().Should().OnlyContain(m => m.Icon != null, "ogni voce ha la sua icona Lucide");
        menu.Items.OfType<MenuItem>().Where(m => m.Classes.Contains("danger")).Should().HaveCount(2, "cestino ed elimina definitivamente");
        w.Close();
    }

    [AvaloniaFact]
    public void Group_row_menu_items_have_icons()
    {
        // Review M-1: il menu della riga di gruppo era rimasto senza icone e senza il rosso sulle voci che cancellano.
        var (w, view, _) = Show(900, Row("Mattina", type: null) with { HabitNodeId = GroupId }, Row("Acqua", parent: GroupId));
        var row = view.GetVisualDescendants().OfType<GroupRow>().Single();
        var menu = (ContextMenu)row.Resources["RowMenu"]!;
        menu.Items.OfType<MenuItem>().Should().OnlyContain(m => m.Icon != null);
        menu.Items.OfType<MenuItem>().Where(m => m.Classes.Contains("danger")).Should().HaveCount(2);
        w.Close();
    }

    // ── Onda "Giornata", Task 7: la testa su una riga, i giorni che si cliccano, la fascia ──

    [AvaloniaFact]
    public void Head_is_one_row_at_900()
    {
        using var culture = TestCulture.Pin();
        var (w, view, _) = Show(900, Row("Acqua", done: true), Row("Corsa"));
        var title = BoundsIn(view.FindControl<TextBlock>("TodayTitle")!, view);
        var add = BoundsIn(view.FindControl<Button>("NewMenuButton")!, view);
        add.Top.Should().BeLessThan(title.Bottom, "titolo e \"+ Nuova\" stanno sulla stessa riga");
        add.Bottom.Should().BeGreaterThan(title.Top);
        add.Right.Should().BeApproximately(900 - 12, 1, "gli strumenti stanno a destra");
        view.FindControl<Control>("HomeTabs").Should().BeNull("i segmenti Attive/Archiviate/Cestino non ci sono più");
        view.FindControl<TextBlock>("DayCount")!.Text.Should().Contain("1 di 2");
        view.FindControl<TextBox>("SearchBox")!.IsVisible.Should().BeFalse("la ricerca sta dietro la lente");
        w.Close();
    }

    [AvaloniaFact]
    public void Head_wraps_under_520()
    {
        using var culture = TestCulture.Pin();
        var (w, view, _) = Show(500, Row("Acqua"));
        var title = BoundsIn(view.FindControl<TextBlock>("TodayTitle")!, view);
        var prev = BoundsIn(view.FindControl<Button>("PrevDayButton")!, view);
        var add = BoundsIn(view.FindControl<Button>("NewMenuButton")!, view);
        var lens = BoundsIn(view.FindControl<Button>("SearchButton")!, view);
        prev.Top.Should().BeLessThan(title.Bottom, "‹ › restano accanto al titolo");
        add.Top.Should().BeGreaterThanOrEqualTo(title.Bottom - 2, "gli strumenti vanno sulla riga sotto");
        lens.Top.Should().BeGreaterThanOrEqualTo(title.Bottom - 2);
        add.Right.Should().BeApproximately(500 - 12, 1, "a destra");
        w.Close();
    }

    [AvaloniaFact]
    public void Clicking_a_header_day_selects_it()
    {
        using var culture = TestCulture.Pin();
        var (w, view, vm) = Show(900, Row("Quattordici", HabitType.Resistance, days: 14));
        var days = HeaderDays(view);
        days.Should().HaveCount(14);
        Avalonia.Automation.AutomationProperties.GetName(days[^3]).Should().Be("lunedì 26 ottobre");

        Click(w, days[^3]);

        vm.DaysBack.Should().Be(2);
        view.FindControl<TextBlock>("TodayTitle")!.Text.Should().Be("Lunedì 26 ottobre");
        var after = HeaderDays(view);
        after.Where(b => b.Classes.Contains("selected")).Should().ContainSingle().Which.Should().BeSameAs(after[^3]);
        w.Close();
    }

    [AvaloniaTheory]
    [InlineData(900, 14)]
    [InlineData(460, 7)]
    [InlineData(300, 4)]
    public void Header_days_sit_over_their_columns(int width, int columns)
    {
        var (w, view, _) = Show(width, Row("Quattordici", HabitType.Resistance, days: 14));
        var days = HeaderDays(view);
        var cells = StripCells(view.GetVisualDescendants().OfType<HabitRow>().Single());
        days.Should().HaveCount(columns);
        cells.Should().HaveCount(columns);
        for (var i = 0; i < columns; i++)
            BoundsIn(days[i], view).Center.X.Should().BeApproximately(BoundsIn(cells[i], view).Center.X, 1.0, $"colonna {i}");
        w.Close();
    }

    [AvaloniaFact]
    public void Selected_day_is_marked_on_every_row()
    {
        var (w, view, vm) = Show(900, Row("Sette"), Row("Quattordici", HabitType.Resistance, days: 14), Row("Acqua", HabitType.Counter));
        foreach (var back in new[] { 0, 2 })
        {
            vm.SelectDayCommand.Execute(back);
            Pump();
            var column = BoundsIn(HeaderDays(view).Single(b => b.Classes.Contains("selected")), view);
            var rows = view.GetVisualDescendants().OfType<HabitRow>().ToList();
            rows.Should().HaveCount(3);
            foreach (var row in rows)
            {
                var marked = MarkedCells(row);
                marked.Should().ContainSingle($"una sola cella marcata per riga ({back} giorni fa)");
                marked[0].Background.Should().NotBeNull();
                BoundsIn(marked[0], view).Center.X.Should().BeApproximately(column.Center.X, 1.0, "sotto il giorno scelto");
            }
        }
        w.Close();
    }

    [AvaloniaFact]
    public void Banner_shows_only_on_a_past_day_and_goes_back()
    {
        using var culture = TestCulture.Pin();
        var (w, view, vm) = Show(900, Row("Quattordici", HabitType.Resistance, days: 14));
        var band = view.FindControl<Border>("PastDayBand")!;
        band.IsVisible.Should().BeFalse("su oggi niente fascia");

        Click(w, view.FindControl<Button>("PrevDayButton")!);

        vm.DaysBack.Should().Be(1);
        band.IsVisible.Should().BeTrue();
        band.Bounds.Height.Should().BeGreaterThan(0);
        band.GetVisualDescendants().OfType<TextBlock>().Should().Contain(t => t.Text != null && t.Text.Contains("martedì 27 ottobre"));
        view.FindControl<TextBlock>("TodayDate")!.Text.Should().Be("ieri");

        Click(w, view.FindControl<Button>("BackToTodayButton")!);

        vm.DaysBack.Should().Be(0);
        band.IsVisible.Should().BeFalse();
        w.Close();
    }

    [AvaloniaFact]
    public void Every_row_has_a_cell_under_every_header_day()
    {
        // GIO-3: a 900 px l'intestazione ha 14 giorni, e prima le righe che non sono astinenze ne riempivano 7: un giorno
        // scelto 10 giorni fa non aveva la sua cella. Ora quanti giorni si vedono lo decide la Home, uguale per tutte.
        using var culture = TestCulture.Pin();
        var (w, view, vm) = Show(900, Row("Azione", days: 14), Row("Contatore", HabitType.Counter, days: 14),
            Row("Astinenza", HabitType.Resistance, days: 14), Row("Diario", HabitType.Journal, days: 14));
        var days = HeaderDays(view);
        days.Should().HaveCount(14);
        var rows = view.GetVisualDescendants().OfType<HabitRow>().ToList();
        rows.Should().HaveCount(4);
        foreach (var row in rows)
            StripCells(row).Should().HaveCount(14, "ogni riga ha le colonne dell'intestazione");

        vm.SelectDayCommand.Execute(10);
        Pump();
        var column = BoundsIn(HeaderDays(view).Single(b => b.Classes.Contains("selected")), view);
        foreach (var row in view.GetVisualDescendants().OfType<HabitRow>())
        {
            var marked = MarkedCells(row);
            marked.Should().ContainSingle("il giorno scelto ha la sua cella su ogni riga");
            BoundsIn(marked[0], view).Center.X.Should().BeApproximately(column.Center.X, 1.0);
        }
        w.Close();
    }

    [AvaloniaFact]
    public void Narrowing_the_view_clamps_the_day()
    {
        // Review Focus 4: il giorno scelto esce dalle colonne visibili → diventa il più vecchio visibile.
        using var culture = TestCulture.Pin();
        var (w, view, vm) = Show(900, Row("Quattordici", HabitType.Resistance, days: 14));
        vm.MaxDaysBack.Should().Be(13, "la vista dice al ViewModel quante colonne si vedono");
        vm.SelectDayCommand.Execute(6);
        Pump();
        vm.DaysBack.Should().Be(6);

        w.Width = 360;
        Pump();

        // A 360 px si vedono cinque giorni (da 340; sotto, quattro): tabella in HomeRowsMetrics.
        vm.MaxDaysBack.Should().Be(4);
        vm.DaysBack.Should().Be(4);
        var days = HeaderDays(view);
        days.Should().HaveCount(5);
        days[0].Classes.Contains("selected").Should().BeTrue("il giorno scelto è il più vecchio che si vede");
        view.FindControl<TextBlock>("TodayTitle")!.Text.Should().Be("Sabato 24 ottobre");
        view.FindControl<Border>("PastDayBand")!.GetVisualDescendants().OfType<TextBlock>()
            .Should().Contain(t => t.Text != null && t.Text.Contains("sabato 24 ottobre"));
        MarkedCells(view.GetVisualDescendants().OfType<HabitRow>().Single()).Should().ContainSingle();
        w.Close();
    }

    [AvaloniaFact]
    public void A_search_with_no_match_does_not_move_the_day()
    {
        // Senza righe non c'è una striscia da misurare: il giorno scelto resta dov'è.
        var (w, view, vm) = Show(900, Row("Quattordici", HabitType.Resistance, days: 14));
        vm.SelectDayCommand.Execute(5);
        Pump();
        vm.SearchText = "zzzz";
        Pump();
        vm.Rows.Should().BeEmpty("il test non prova niente se la ricerca trova qualcosa");
        vm.DaysBack.Should().Be(5);
        w.Close();
    }

    [AvaloniaFact]
    public void More_menu_switches_tabs_and_archive_has_no_day_controls()
    {
        using var culture = TestCulture.Pin();
        var (w, view, vm) = Show(900, Row("Quattordici", HabitType.Resistance, days: 14));
        var more = view.FindControl<Button>("MoreButton")!;
        var flyout = (MenuFlyout)more.Flyout!;
        flyout.ShowAt(more);
        Pump();
        // "Tag" (GIO-22) resta nascosta: nessuna riga di questo test ha tag. HomeTagFilterViewTests la guarda accesa.
        var items = flyout.Items.OfType<MenuItem>().Where(i => i.IsVisible).ToList();
        items.Select(i => i.Header).Should().Equal("Attive", "Archiviate", "Cestino");
        items.Select(i => i.IsChecked).Should().Equal(new[] { true, false, false }, "la spunta è sulla scheda attiva");
        var back = view.FindControl<Button>("BackToActiveButton")!;
        back.IsVisible.Should().BeFalse();

        items[1].Command!.Execute(items[1].CommandParameter);
        flyout.Hide();
        Pump();

        vm.ActiveTab.Should().Be(HomeTab.Archived);
        view.FindControl<TextBlock>("TodayTitle")!.Text.Should().Be("Archiviate");
        view.FindControl<Button>("PrevDayButton")!.IsEffectivelyVisible.Should().BeFalse("niente ‹ › in Archivio");
        view.FindControl<Button>("NextDayButton")!.IsEffectivelyVisible.Should().BeFalse();
        view.FindControl<TextBlock>("DayCount")!.IsVisible.Should().BeFalse("niente conto");
        view.FindControl<TextBlock>("TodayDate")!.IsVisible.Should().BeFalse();
        view.FindControl<DayHeaderView>("DayHeader")!.IsVisible.Should().BeFalse("niente intestazione dei giorni");
        back.IsVisible.Should().BeTrue();
        back.Content.Should().Be("Torna alle abitudini");
        flyout.ShowAt(more);
        Pump();
        flyout.Items.OfType<MenuItem>().Where(i => i.IsVisible).Select(i => i.IsChecked).Should().Equal(false, true, false);
        flyout.Hide();

        Click(w, back);

        vm.ActiveTab.Should().Be(HomeTab.Active);
        back.IsVisible.Should().BeFalse();
        view.FindControl<Button>("PrevDayButton")!.IsEffectivelyVisible.Should().BeTrue();
        w.Close();
    }

    [AvaloniaFact]
    public void Search_opens_from_the_lens_and_from_ctrl_f()
    {
        using var culture = TestCulture.Pin();
        var (w, view, vm) = Show(900, Row("Acqua"), Row("Corsa"));
        var box = view.FindControl<TextBox>("SearchBox")!;
        var date = view.FindControl<TextBlock>("TodayDate")!;
        var count = view.FindControl<TextBlock>("DayCount")!;
        box.IsVisible.Should().BeFalse();
        date.IsVisible.Should().BeTrue();

        Click(w, view.FindControl<Button>("SearchButton")!);

        box.IsVisible.Should().BeTrue("la lente apre la casella");
        box.IsFocused.Should().BeTrue();
        date.IsVisible.Should().BeFalse("la casella prende il posto di data e conto");
        count.IsVisible.Should().BeFalse();

        box.Text = "acq";
        Pump();
        vm.Rows.Should().ContainSingle();
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
        Pump();
        box.IsVisible.Should().BeFalse("Esc la richiude");
        vm.SearchText.Should().BeNullOrEmpty("e toglie il filtro: una ricerca che non si vede non deve filtrare");
        vm.Rows.Should().HaveCount(2);
        date.IsVisible.Should().BeTrue();

        w.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
        Pump();
        box.IsVisible.Should().BeTrue("Ctrl+F la apre");
        box.IsFocused.Should().BeTrue();

        // Vuota e senza fuoco si richiude; con del testo resta.
        view.FindControl<Button>("NewMenuButton")!.Focus();
        Pump();
        box.IsVisible.Should().BeFalse();

        w.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
        box.Text = "cor";
        view.FindControl<Button>("NewMenuButton")!.Focus();
        Pump();
        box.IsVisible.Should().BeTrue("una ricerca in corso resta a vista");
        w.Close();
    }
}
