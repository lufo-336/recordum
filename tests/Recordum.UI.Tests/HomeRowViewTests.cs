using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAssertions;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests;

public class HomeRowViewTests
{
    private static Control Show(Control view)
    {
        var w = new Window { Width = 600, Height = 200, Content = view };
        w.Show();
        Dispatcher.UIThread.RunJobs();
        return view;
    }

    [AvaloniaFact]
    public void The_template_builds_a_group_row_for_a_group_and_a_habit_row_otherwise()
    {
        var group = RowFixture.Vm(RowFixture.Row(Guid.NewGuid(), type: null));
        var habit = RowFixture.Vm(RowFixture.Row(Guid.NewGuid()));
        HomeRowTemplate.Instance.Match(group).Should().BeTrue();
        HomeRowTemplate.Instance.Match("x").Should().BeFalse();
        HomeRowTemplate.Instance.Build(group).Should().BeOfType<GroupRow>();
        HomeRowTemplate.Instance.Build(habit).Should().BeOfType<HabitRow>();
    }

    [AvaloniaFact]
    public void The_rename_editor_replaces_the_label_only_while_renaming()
    {
        var vm = RowFixture.Vm(RowFixture.Row(Guid.NewGuid(), "Corsa"));
        var view = Show(new HabitRow { DataContext = vm });
        var label = view.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Text == "Corsa");
        var box = view.GetLogicalDescendants().OfType<TextBox>().Single();
        label.IsVisible.Should().BeTrue();
        box.IsVisible.Should().BeFalse();

        vm.BeginRenameCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        label.IsVisible.Should().BeFalse();
        box.IsVisible.Should().BeTrue();
        box.Text.Should().Be("Corsa");
    }

    [AvaloniaFact]
    public void Enter_in_the_rename_editor_commits()
    {
        var vm = RowFixture.Vm(RowFixture.Row(Guid.NewGuid(), "Corsa"));
        var view = Show(new HabitRow { DataContext = vm });
        vm.BeginRenameCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        var box = view.GetLogicalDescendants().OfType<TextBox>().Single();
        box.Text = "Nuoto";
        Dispatcher.UIThread.RunJobs();

        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = box });
        Dispatcher.UIThread.RunJobs();

        vm.IsRenaming.Should().BeFalse();
        vm.Name.Should().Be("Nuoto");
    }

    [AvaloniaFact]
    public void The_context_menu_of_a_habit_row_offers_the_pin_submenu()
    {
        var vm = RowFixture.Vm(RowFixture.Row(Guid.NewGuid()));
        var view = Show(new HabitRow { DataContext = vm });
        var grid = view.GetLogicalDescendants().OfType<Grid>().Single(g => g.Name == "MenuHost");
        grid.ContextMenu.Should().BeNull("il menu nasce alla prima richiesta, non con la riga (F1-PERF1)");
        // La richiesta vera (tasto destro, Shift+F10) lo costruisce e lo apre. Aperto: un ContextMenu
        // chiuso non eredita il DataContext e ogni voce resta al default IsVisible=true, quindi
        // "contiene Pin" passerebbe anche con i binding rotti. Si asserisce pure ciò che deve mancare
        // (Ripristina/Riattiva) e si prova un tab dove Pin sparisce.
        grid.RaiseEvent(new ContextRequestedEventArgs());
        Dispatcher.UIThread.RunJobs();
        var menu = grid.ContextMenu!;
        menu.IsOpen.Should().BeTrue();

        IEnumerable<object?> Visible() => menu.Items.OfType<MenuItem>().Where(i => i.IsVisible).Select(i => i.Header);
        var pin = Recordum.Presentation.Resources.Strings.Home_ContextMenu_Pin;
        Visible().Should().Contain(pin)
            .And.NotContain(Recordum.Presentation.Resources.Strings.Home_ContextMenu_Restore)
            .And.NotContain(Recordum.Presentation.Resources.Strings.Home_ContextMenu_Unarchive);

        vm.CurrentTab = HomeTab.Trash;
        Dispatcher.UIThread.RunJobs();
        Visible().Should().NotContain(pin)
            .And.Contain(Recordum.Presentation.Resources.Strings.Home_ContextMenu_Restore);
        menu.Close();
    }

    [AvaloniaFact]
    public void The_group_frame_is_tinted_only_when_expanded_with_children()
    {
        var id = Guid.NewGuid();
        var vm = RowFixture.Vm(RowFixture.Row(id, "Gruppo", "#E11D48", type: null, childCount: 1));
        vm.Children.Add(RowFixture.Vm(RowFixture.Row(Guid.NewGuid(), "Figlia", parent: id)));
        var view = Show(new GroupRow { DataContext = vm });
        var frame = view.GetLogicalDescendants().OfType<Border>().First(b => b.Classes.Contains("groupframe"));
        var childList = view.GetLogicalDescendants().OfType<ItemsControl>().First(c => c.Classes.Contains("childlist"));

        childList.IsVisible.Should().BeTrue();
        frame.Background.Should().BeAssignableTo<ISolidColorBrush>()
            .Which.Color.Should().Be(Color.FromArgb(10, 0xE1, 0x1D, 0x48));

        vm.IsExpanded = false;
        Dispatcher.UIThread.RunJobs();

        childList.IsVisible.Should().BeFalse();
        frame.Background.Should().Be(Brushes.Transparent);
    }

    [AvaloniaFact]
    public void A_nested_group_recurses_through_the_same_template()
    {
        var rootId = Guid.NewGuid();
        var innerId = Guid.NewGuid();
        var root = RowFixture.Vm(RowFixture.Row(rootId, "Radice", type: null, childCount: 1));
        var inner = RowFixture.Vm(RowFixture.Row(innerId, "Interno", type: null, parent: rootId, childCount: 1));
        inner.Children.Add(RowFixture.Vm(RowFixture.Row(Guid.NewGuid(), "Foglia", parent: innerId)));
        root.Children.Add(inner);

        var view = Show(new GroupRow { DataContext = root });
        Dispatcher.UIThread.RunJobs();

        // La radice è `view`, non un discendente: si contano il gruppo interno e la foglia.
        view.GetLogicalDescendants().OfType<GroupRow>().Should().HaveCount(1);
        view.GetLogicalDescendants().OfType<HabitRow>().Should().HaveCount(1);
    }
}
