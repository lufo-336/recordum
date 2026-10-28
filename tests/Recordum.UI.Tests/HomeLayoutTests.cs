using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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

    [AvaloniaFact]
    public void Header_reads_Oggi_with_the_date()
    {
        using var culture = TestCulture.Pin();
        var (w, view, _) = Show(900, Row("Acqua"));
        view.FindControl<TextBlock>("TodayTitle")!.Text.Should().Be("Oggi");
        view.FindControl<TextBlock>("TodayDate")!.Text.Should().Be("mercoledì 28 ottobre");
        w.Close();
    }

    [AvaloniaFact]
    public void Today_column_is_aligned_across_strip_lengths()
    {
        // Review Focus 4: 7 e 14 giorni, un gruppo senza striscia e una figlia rientrata. La figlia è rientrata anche a
        // destra dalla cornice del gruppo (6 px): conta che il suo "oggi" stia sotto "oggi", dentro la colonna.
        using var culture = TestCulture.Pin();
        var (w, view, _) = Show(900,
            Row("Sette"), Row("Quattordici", HabitType.Resistance, days: 14),
            Row("Gruppo", type: null) with { HabitNodeId = GroupId }, Row("Figlia", parent: GroupId));
        var header = view.FindControl<HeatmapStrip>("StripHeader")!;
        var today = header.GetVisualDescendants().OfType<TextBlock>().Last();
        today.Text.Should().Be("oggi");
        var column = BoundsIn(today, view);

        var strips = view.GetVisualDescendants().OfType<HabitRow>()
            .Select(r => r.GetVisualDescendants().OfType<HeatmapStrip>().Single()).ToList();
        strips.Should().HaveCount(3);
        foreach (var strip in strips)
        {
            var cell = BoundsIn(strip.GetVisualDescendants().OfType<TextBlock>().Last(), view);
            cell.Center.X.Should().BeInRange(column.Left, column.Right, "l'ultima cella di ogni striscia è sotto \"oggi\"");
        }
        w.Close();
    }

    [AvaloniaFact]
    public void Narrow_home_has_no_overlap()
    {
        // Review Focus 3 e F2b-5: a 280 px "+ Nuova" finiva sopra "Archiviate".
        using var culture = TestCulture.Pin();
        var (w, view, _) = Show(280, Row("Un nome davvero molto lungo per una riga stretta"));
        var parts = new[] { "NewMenuButton", "HomeTabs", "SearchBox", "DoneToggle", "SortBox" }
            .Select(n => (Name: n, Rect: BoundsIn(view.FindControl<Control>(n)!, view))).ToList();
        foreach (var a in parts)
            foreach (var b in parts.Where(p => p.Name != a.Name))
                a.Rect.Intersects(b.Rect).Should().BeFalse($"{a.Name} e {b.Name} si sovrappongono");
        parts.Should().OnlyContain(p => p.Rect.Right <= 280 + 0.5, "niente esce dalla colonna");
        view.FindControl<TextBlock>("TodayDate")!.IsVisible.Should().BeFalse("sotto 320 px la data si nasconde");

        var name = view.GetVisualDescendants().OfType<HabitRow>().Single()
            .GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.StartsWith("Un nome", StringComparison.Ordinal) == true);
        name.TextTrimming.Should().Be(TextTrimming.CharacterEllipsis);
        w.Close();
    }

    [AvaloniaFact]
    public void A_wide_home_shows_fourteen_days_and_a_narrow_one_seven()
    {
        var (w, view, _) = Show(900, Row("Quattordici", HabitType.Resistance, days: 14));
        view.FindControl<HeatmapStrip>("StripHeader")!.GetVisualDescendants().OfType<TextBlock>().Should().HaveCount(14);
        w.Width = 360;
        for (var i = 0; i < 2; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        view.FindControl<HeatmapStrip>("StripHeader")!.GetVisualDescendants().OfType<TextBlock>().Should().HaveCount(7);
        view.GetVisualDescendants().OfType<HabitRow>().Single().GetVisualDescendants().OfType<HeatmapStrip>().Single()
            .GetVisualDescendants().OfType<TextBlock>().Should().HaveCount(7);
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
}
