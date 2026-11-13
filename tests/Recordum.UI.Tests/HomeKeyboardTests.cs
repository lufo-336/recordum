using System.Collections.Immutable;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Core.Abstractions.Services.Dtos;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels;
using Recordum.Presentation.ViewModels.Quick;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests;

/// <summary>La tastiera della Home (onda "Giornata", A2): ↑ ↓ scelgono la riga, Spazio registra sul giorno della Home,
/// Invio apre e fissa, ← → cambiano giorno. Con il fuoco nella Home o da nessuna parte, mai rubando un tasto a chi lo usa.</summary>
public class HomeKeyboardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);

    private sealed class SpyHandler : IQuickActionHandler
    {
        public List<(Guid Id, DateOnly? Day)> Calls { get; } = new();
        public HabitType HabitType => HabitType.Action;
        public ValueTask<bool> ExecuteAsync(Guid habitNodeId, DateOnly? day, CancellationToken ct = default)
        {
            Calls.Add((habitNodeId, day));
            return ValueTask.FromResult(true);
        }
    }

    private static HomeTreeRow Row(string name, HabitType? type = HabitType.Action, Guid? parent = null, int children = 0)
    {
        var counts = ImmutableArray.Create(Enumerable.Repeat(0, 14).ToArray());
        return new HomeTreeRow(Guid.NewGuid(), parent, name, "#2F7FC1", type, null, 0, false, PinScope.None, children, false,
            ImmutableArray.Create(0, 0, 0, 0, 0, 0, 0), ImmutableArray<Guid>.Empty, CompletionsLast14Days: counts);
    }

    private sealed record Home(Window W, HomeView View, HomeViewModel Vm, SpyHandler Spy);

    private static Home Show(HomeTreeRow[] rows, Control? sibling = null, double height = 600)
    {
        var query = new FakeHabitTreeQueryService();
        query.Rows.AddRange(rows);
        var spy = new SpyHandler();
        var vm = new HomeViewModel(query, new FakeHabitNodeService(), new FakePurgeService(),
            quickActions: new QuickActionHandlerSelector([spy]),
            clock: new PrivateDbHost.FakeClock { Now = Now });
        var view = new HomeView { DataContext = vm };
        Control content = view;
        if (sibling is not null)
        {
            var panel = new DockPanel();
            DockPanel.SetDock(sibling, Dock.Bottom);
            panel.Children.Add(sibling);
            panel.Children.Add(view);
            content = panel;
        }
        var w = new Window { Width = 900, Height = height, Content = content };
        w.Show();
        vm.LoadCommand.Execute(null);
        Pump();
        return new Home(w, view, vm, spy);
    }

    private static Home Show(params HomeTreeRow[] rows) => Show(rows, null);

    private static void Pump()
    {
        for (var i = 0; i < 3; i++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window w, Key key, PhysicalKey physical, string symbol = "",
        RawInputModifiers modifiers = RawInputModifiers.None)
    {
        w.KeyPress(key, modifiers, physical, symbol);
        Pump();
    }

    private static void Down(Window w) => Press(w, Key.Down, PhysicalKey.ArrowDown);
    private static void Up(Window w) => Press(w, Key.Up, PhysicalKey.ArrowUp);
    private static void Left(Window w) => Press(w, Key.Left, PhysicalKey.ArrowLeft);
    private static void Right(Window w) => Press(w, Key.Right, PhysicalKey.ArrowRight);
    private static void Space(Window w) => Press(w, Key.Space, PhysicalKey.Space, " ");
    private static void Enter(Window w) => Press(w, Key.Enter, PhysicalKey.Enter, "\r");

    [AvaloniaFact]
    public void Down_without_a_selection_picks_the_first_row_and_up_the_last()
    {
        var h = Show(Row("a"), Row("b"), Row("c"));
        Down(h.W);
        h.Vm.SelectedRow.Should().BeSameAs(h.Vm.Rows[0]);

        h.Vm.SelectedRow = null;
        Up(h.W);
        h.Vm.SelectedRow.Should().BeSameAs(h.Vm.Rows[^1]);
        h.W.Close();
    }

    [AvaloniaFact]
    public void With_several_rows_chosen_the_arrow_starts_from_the_anchor()
    {
        var h = Show(Row("a"), Row("b"), Row("c"));
        h.Vm.SelectInteractive(h.Vm.Rows[1], ctrl: false, shift: false);
        h.Vm.SelectInteractive(h.Vm.Rows[2], ctrl: false, shift: true);
        h.Vm.HasMultiSelection.Should().BeTrue();
        Down(h.W);
        h.Vm.SelectedRow.Should().BeSameAs(h.Vm.Rows[2]);
        h.Vm.HasMultiSelection.Should().BeFalse();
        h.W.Close();
    }

    [AvaloniaFact]
    public void Arrows_walk_the_rows_and_stop_at_the_ends()
    {
        var h = Show(Row("a"), Row("b"), Row("c"));
        Down(h.W);
        Down(h.W);
        h.Vm.SelectedRow.Should().BeSameAs(h.Vm.Rows[1]);
        Down(h.W);
        Down(h.W);
        h.Vm.SelectedRow.Should().BeSameAs(h.Vm.Rows[2], "in fondo si resta in fondo, senza ricominciare");
        Up(h.W);
        h.Vm.SelectedRow.Should().BeSameAs(h.Vm.Rows[1]);
        h.W.Close();
    }

    [AvaloniaFact]
    public void Arrows_go_through_a_group_and_its_children()
    {
        var group = Row("g", type: null, children: 1);
        var child = Row("c", parent: group.HabitNodeId);
        var h = Show(group, child, Row("z"));
        h.Vm.Rows.Should().HaveCount(3, "il test non prova niente se il figlio non è fra le righe");
        Down(h.W);
        Down(h.W);
        h.Vm.SelectedRow!.HabitNodeId.Should().Be(child.HabitNodeId);
        h.W.Close();
    }

    [AvaloniaFact]
    public void The_chosen_row_comes_into_view()
    {
        var rows = Enumerable.Range(0, 40).Select(i => Row($"r{i:00}")).ToArray();
        var h = Show(rows, height: 400);
        var scroller = h.View.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroller.Offset.Y.Should().Be(0, "il test non prova niente se la lista parte già in fondo");

        Up(h.W);

        var last = h.Vm.Rows[^1];
        h.Vm.SelectedRow.Should().BeSameAs(last);
        var card = h.View.GetVisualDescendants().OfType<HabitRow>().SingleOrDefault(r => ReferenceEquals(r.DataContext, last));
        card.Should().NotBeNull("la riga scelta ha una vista: è entrata nella parte che si vede");
        var top = card!.TranslatePoint(new Point(0, 0), scroller)!.Value.Y;
        top.Should().BeGreaterThanOrEqualTo(-1);
        (top + card.Bounds.Height).Should().BeLessThanOrEqualTo(scroller.Viewport.Height + 1);
        h.W.Close();
    }

    [AvaloniaFact]
    public void Space_logs_the_selected_row_today_with_no_day()
    {
        var a = Row("a");
        var h = Show(a, Row("b"));
        Down(h.W);
        Space(h.W);
        h.Spy.Calls.Should().Equal((a.HabitNodeId, (DateOnly?)null));
        h.W.Close();
    }

    [AvaloniaFact]
    public void Space_on_a_past_day_logs_on_that_day()
    {
        var a = Row("a");
        var h = Show(a);
        Down(h.W);
        Left(h.W);
        h.Vm.DaysBack.Should().Be(1);
        Space(h.W);
        h.Spy.Calls.Should().Equal((a.HabitNodeId, (DateOnly?)h.Vm.SelectedDay));
        h.W.Close();
    }

    [AvaloniaFact]
    public void Space_with_no_selection_does_nothing()
    {
        var h = Show(Row("a"));
        Space(h.W);
        h.Spy.Calls.Should().BeEmpty();
        h.W.Close();
    }

    [AvaloniaFact]
    public void Space_on_a_group_does_nothing()
    {
        var group = Row("g", type: null);
        var h = Show(group, Row("a"));
        h.Vm.SelectedRow = h.Vm.Rows.Single(r => r.IsGroup);
        Space(h.W);
        h.Spy.Calls.Should().BeEmpty();
        h.W.Close();
    }

    [AvaloniaFact]
    public void Space_in_the_search_box_types_a_space()
    {
        var h = Show(Row("a"), Row("b"));
        Down(h.W);
        Press(h.W, Key.F, PhysicalKey.F, "f", RawInputModifiers.Control);
        var box = h.View.FindControl<TextBox>("SearchBox")!;
        box.IsFocused.Should().BeTrue("il test non prova niente se il fuoco non è nella casella");

        h.W.KeyTextInput("a");
        Space(h.W);
        h.W.KeyTextInput(" ");
        Pump();

        h.Spy.Calls.Should().BeEmpty("Spazio nella casella è uno spazio, non un log rapido");
        box.Text.Should().Be("a ");
        h.W.Close();
    }

    [AvaloniaFact]
    public void Arrows_do_nothing_while_renaming()
    {
        var h = Show(Row("a"), Row("b"));
        Down(h.W);
        var first = h.Vm.SelectedRow!;
        first.BeginRenameCommand.Execute(null);
        Pump();
        var box = h.View.GetVisualDescendants().OfType<HabitRow>().First().GetVisualDescendants().OfType<TextBox>().Single();
        box.Focus();
        Pump();
        box.IsFocused.Should().BeTrue("il test non prova niente se il fuoco non è nella casella di rinomina");

        Down(h.W);
        Left(h.W);
        Space(h.W);

        h.Vm.SelectedRow.Should().BeSameAs(first);
        h.Vm.DaysBack.Should().Be(0);
        h.Spy.Calls.Should().BeEmpty();
        h.W.Close();
    }

    [AvaloniaFact]
    public void Keys_are_not_taken_from_a_focused_button_or_combo()
    {
        var a = Row("a");
        var h = Show(a, Row("b"));
        Down(h.W);
        var sort = h.View.FindControl<ComboBox>("SortBox")!;
        sort.Focus();
        Pump();
        sort.IsFocused.Should().BeTrue();

        Down(h.W);
        Left(h.W);

        // ↓ sulla tendina è della tendina (cambia l'ordinamento, che rifà le righe: si confronta l'id).
        h.Vm.SelectedRow!.HabitNodeId.Should().Be(a.HabitNodeId, "↓ sulla tendina è della tendina");
        h.Vm.DaysBack.Should().Be(0);

        var search = h.View.FindControl<Button>("SearchButton")!;
        search.Focus();
        Pump();
        Space(h.W);
        h.Spy.Calls.Should().BeEmpty("Spazio su un pulsante preme il pulsante, non registra la riga");
        h.W.Close();
    }

    [AvaloniaFact]
    public void Left_and_right_change_the_day_and_right_stops_at_today()
    {
        var h = Show(Row("a"));
        Right(h.W);
        h.Vm.DaysBack.Should().Be(0, "dopo oggi non c'è niente");
        Left(h.W);
        Left(h.W);
        h.Vm.DaysBack.Should().Be(2);
        Right(h.W);
        h.Vm.DaysBack.Should().Be(1);
        h.W.Close();
    }

    [AvaloniaFact]
    public void Left_arrow_in_archive_does_nothing()
    {
        var h = Show(Row("a"));
        h.Vm.SwitchTabCommand.Execute(HomeTab.Archived);
        Pump();
        Left(h.W);
        h.Vm.DaysBack.Should().Be(0);
        h.W.Close();
    }

    [AvaloniaFact]
    public void Enter_opens_and_pins_the_selected_row()
    {
        var a = Row("a");
        var h = Show(a, Row("b"));
        var asked = new List<Guid>();
        h.Vm.HabitActivationRequested += (_, id) => asked.Add(id);
        Enter(h.W);
        asked.Should().BeEmpty("senza una riga scelta Invio non apre niente");

        Down(h.W);
        Enter(h.W);
        asked.Should().Equal(a.HabitNodeId);
        h.Vm.RecordRequestedFor.Should().BeNull("aprire non è chiedere di registrare");
        h.W.Close();
    }

    [AvaloniaFact]
    public void Keys_are_ignored_when_focus_is_in_a_workspace_tab()
    {
        var outside = new Border { Focusable = true, Height = 40, Background = Avalonia.Media.Brushes.Transparent };
        var h = Show([Row("a"), Row("b")], outside);
        outside.Focus();
        Pump();
        outside.IsFocused.Should().BeTrue("il test non prova niente se il fuoco non è fuori dalla Home");

        Down(h.W);
        Left(h.W);

        h.Vm.SelectedRow.Should().BeNull();
        h.Vm.DaysBack.Should().Be(0);
        h.W.Close();
    }

    [AvaloniaFact]
    public void The_tooltips_say_the_shortcuts()
    {
        var h = Show(Row("a"));
        ToolTip.GetTip(h.View.FindControl<Button>("PrevDayButton")!).Should().Be(Strings.Home_Day_Previous_Tip);
        ToolTip.GetTip(h.View.FindControl<Button>("NextDayButton")!).Should().Be(Strings.Home_Day_Next_Tip);
        ToolTip.GetTip(h.View.FindControl<Button>("NewMenuButton")!).Should().Be(Strings.Home_NewMenu_Tip);
        var quickLog = h.View.GetVisualDescendants().OfType<Button>().First(b => b.Name == "QuickLog");
        ToolTip.GetTip(quickLog).Should().Be(Strings.Home_QuickAction_Tip);
        h.W.Close();
    }
}
