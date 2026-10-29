using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests;

public class HomeViewTests
{
    internal static (Window W, HomeView V, HomeViewModel Vm, FakeHabitNodeService Nodes) Show(params Guid[] ids)
    {
        // Nomi deterministici (r0, r1, …): l'ordine di Roots dipende dall'ordinamento del ViewModel,
        // non dall'ordine in cui la fixture li elenca — con un nome derivato dal Guid i test
        // diventavano dipendenti dal caso.
        var rows = ids.Select((i, n) => RowFixture.Row(i, $"r{n}", "#E11D48")).ToArray();
        var (vm, _, nodes) = HomeFixture.Build(rows);
        var view = new HomeView { DataContext = vm };
        var w = new Window { Width = 900, Height = 600, Content = view };
        w.Show();
        vm.LoadCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return (w, view, vm, nodes);
    }

    [AvaloniaFact]
    public void Only_the_branch_of_the_current_state_is_materialised()
    {
        var (w, view, vm, _) = Show(Guid.NewGuid(), Guid.NewGuid());
        vm.State.Should().Be(HomeState.Loaded);
        view.GetVisualDescendants().OfType<HabitRow>().Should().HaveCount(2);
        view.GetVisualDescendants().OfType<EmptyState>().Should().BeEmpty();

        vm.State = HomeState.Empty;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        view.GetVisualDescendants().OfType<EmptyState>().Should().ContainSingle();
        view.GetVisualDescendants().OfType<HabitRow>().Should().BeEmpty();
        w.Close();
    }

    [AvaloniaFact]
    public void The_sort_box_shows_translated_labels_not_enum_names()
    {
        using var culture = Fixtures.TestCulture.Pin();
        var (w, view, _, _) = Show(Guid.NewGuid());
        var box = view.GetVisualDescendants().OfType<ComboBox>().Single(b => b.Name == "SortBox");
        box.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible)
            .Should().Contain(t => t.Text == "Manuale").And.NotContain(t => t.Text == "Manual");
        w.Close();
    }

    [AvaloniaFact]
    public void The_new_menu_shows_both_the_habit_item_and_the_group_item()
    {
        // F1 review I4 lo spense (il selettore non c'era); F2b lo ha riacceso con il selettore vero.
        var (w, view, _, _) = Show(Guid.NewGuid());
        var button = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "NewMenuButton");
        var items = ((MenuFlyout)button.Flyout!).Items.OfType<MenuItem>().ToList();

        items.Single(i => (string?)i.Header == Strings.Home_NewMenu_Habit).IsVisible.Should().BeTrue();
        items.Single(i => (string?)i.Header == Strings.Home_NewMenu_Group).IsVisible.Should().BeTrue();
        button.IsVisible.Should().BeTrue("il pulsante del menu resta");
        w.Close();
    }

    [AvaloniaFact]
    public void A_click_on_a_row_selects_it_and_a_press_drag_does_not()
    {
        var (w, view, vm, _) = Show(Guid.NewGuid(), Guid.NewGuid());
        var card = view.GetVisualDescendants().OfType<HabitRow>().First();
        var centre = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), w)!.Value;

        w.MouseDown(centre, MouseButton.Left);
        w.MouseUp(centre, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        vm.SelectedRow.Should().BeSameAs(card.DataContext);

        vm.ClearSelection();
        w.MouseDown(centre, MouseButton.Left);
        w.MouseUp(centre + new Vector(0, 40), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        vm.SelectedRow.Should().BeNull("un press-drag non è un clic e non deve selezionare");
        w.Close();
    }

    [AvaloniaFact]
    public void A_click_on_a_control_inside_the_row_does_not_select_the_row()
    {
        // La casella di rinomina è dentro la riga: un clic lì posiziona il cursore, non seleziona.
        var (w, view, vm, _) = Show(Guid.NewGuid(), Guid.NewGuid());
        var row = view.GetVisualDescendants().OfType<HabitRow>().ElementAt(1);
        var target = (HomeRowViewModel)row.DataContext!;
        target.BeginRenameCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var box = row.GetVisualDescendants().OfType<TextBox>().Single();
        box.IsVisible.Should().BeTrue("il test non prova niente se la casella non è a schermo");
        var p = box.TranslatePoint(new Point(box.Bounds.Width / 2, box.Bounds.Height / 2), w)!.Value;

        w.MouseDown(p, MouseButton.Left);
        w.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        vm.SelectedRow.Should().BeNull("un clic nella casella di rinomina non è un clic sulla riga");
        w.Close();
    }

    [AvaloniaFact]
    public void A_right_click_on_an_unselected_row_selects_it_first()
    {
        var (w, view, vm, _) = Show(Guid.NewGuid(), Guid.NewGuid());
        var second = view.GetVisualDescendants().OfType<HabitRow>().ElementAt(1);
        var p = second.TranslatePoint(new Point(second.Bounds.Width / 2, second.Bounds.Height / 2), w)!.Value;

        w.MouseDown(p, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();

        vm.SelectedRow.Should().BeSameAs(second.DataContext);
        w.Close();
    }

    [AvaloniaFact]
    public void A_click_on_the_empty_tree_background_raises_the_background_click()
    {
        var (w, view, vm, _) = Show(Guid.NewGuid());
        var raised = 0;
        vm.BackgroundClicked += (_, _) => raised++;
        var region = view.GetVisualDescendants().OfType<Border>().First(b => b.Name == "TreeRegion");
        var low = region.TranslatePoint(new Point(20, region.Bounds.Height - 10), w)!.Value;

        w.MouseDown(low, MouseButton.Left);
        w.MouseUp(low, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        raised.Should().Be(1);
        w.Close();
    }

    [AvaloniaFact]
    public void Ctrl_F_moves_the_focus_to_the_search_box_even_with_nothing_focused()
    {
        var (w, view, _, _) = Show(Guid.NewGuid());
        w.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
        Dispatcher.UIThread.RunJobs();
        var box = view.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "SearchBox");
        box.IsFocused.Should().BeTrue();
        w.Close();
    }

    [AvaloniaFact]
    public void Delete_acts_on_the_selected_row_even_without_focus_inside_it()
    {
        var id = Guid.NewGuid();
        var (w, _, vm, nodes) = Show(id);
        vm.SelectRowCommand.Execute(vm.Roots[0]);
        Dispatcher.UIThread.RunJobs();
        nodes.SoftDeleted.Should().BeEmpty("il test non prova niente se prima del tasto è già cancellato");

        w.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, "");
        Dispatcher.UIThread.RunJobs();

        nodes.SoftDeleted.Should().Equal(new[] { id },
            "il tasto raggiunge la riga selezionata senza che il fuoco sia dentro di essa");
        w.Close();
    }

    [AvaloniaFact]
    public void A_detached_view_no_longer_listens_for_shortcuts_on_the_window()
    {
        // Allo stacco GetTopLevel(this) è già null: senza un riferimento tenuto da parte il gestore
        // sul TopLevel restava appeso e Ctrl+F veniva "gestito" da una vista ormai fuori dall'albero.
        var (w, view, _, _) = Show(Guid.NewGuid());
        var handled = false;
        w.AddHandler(InputElement.KeyDownEvent, (_, e) => handled = e.Handled, RoutingStrategies.Bubble, handledEventsToo: true);
        w.Content = new Border();
        Dispatcher.UIThread.RunJobs();
        view.GetVisualParent().Should().BeNull("il test non prova niente se la vista è ancora agganciata");

        w.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
        Dispatcher.UIThread.RunJobs();

        handled.Should().BeFalse();
        w.Close();
    }

    [AvaloniaFact]
    public void The_error_branch_is_built_and_its_retry_button_reloads()
    {
        // L'Error usa un binding a riflessione ($parent[HomeView].DataContext) che il compilatore non controlla.
        var (w, view, vm, _) = Show(Guid.NewGuid());
        vm.State = HomeState.Error;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        view.GetVisualDescendants().OfType<HabitRow>().Should().BeEmpty();
        var retry = view.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, Strings.Home_Error_Retry));
        retry.Command.Should().BeSameAs(vm.RetryCommand);

        retry.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();
        vm.State.Should().Be(HomeState.Loaded);
        w.Close();
    }
}
