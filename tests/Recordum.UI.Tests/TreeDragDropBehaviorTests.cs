using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using Recordum.Presentation.ViewModels;
using Recordum.UI.Behaviors;
using Recordum.UI.Tests.Fixtures;
using Recordum.UI.Views;
using Recordum.UI.Views.Rows;
using Xunit;

namespace Recordum.UI.Tests;

public class TreeDragDropBehaviorTests
{
    [AvaloniaTheory]
    [InlineData(0.0, DropZone.Before)]
    [InlineData(0.39, DropZone.Before)]
    [InlineData(0.4, DropZone.Onto)]
    [InlineData(0.69, DropZone.Onto)]
    [InlineData(0.7, DropZone.After)]
    [InlineData(1.0, DropZone.After)]
    public void ResolveZone_splits_the_row_in_three_bands(double fraction, DropZone expected)
        => TreeDragDropBehavior.ResolveZone(fraction).Should().Be(expected);

    [AvaloniaFact]
    public void Enabling_the_behaviour_allows_dropping_and_disabling_it_takes_it_back()
    {
        var ic = new ItemsControl();
        TreeDragDropBehavior.SetEnabled(ic, true);
        DragDrop.GetAllowDrop(ic).Should().BeTrue();
        TreeDragDropBehavior.SetEnabled(ic, false);
        DragDrop.GetAllowDrop(ic).Should().BeFalse();
    }

    [AvaloniaFact]
    public void FindRowCard_climbs_to_the_card_and_gives_up_outside_a_row()
    {
        var (w, view, _, _) = HomeViewTests.Show(Guid.NewGuid());
        var row = view.GetVisualDescendants().OfType<HabitRow>().Single();
        var inner = row.GetVisualDescendants().OfType<TextBlock>().First();

        var card = TreeDragDropBehavior.FindRowCard(inner);
        card.Should().NotBeNull();
        card!.Classes.Should().Contain("habitblock");
        card.DataContext.Should().BeSameAs(row.DataContext);

        TreeDragDropBehavior.FindRowCard(view).Should().BeNull();
        w.Close();
    }

    [AvaloniaFact]
    public void A_pointer_drag_past_the_threshold_fades_the_source_row_and_clears_it_at_the_end()
    {
        var (w, view, vm, _) = HomeViewTests.Show(Guid.NewGuid(), Guid.NewGuid());
        var row = view.GetVisualDescendants().OfType<HabitRow>().First();
        var src = (HomeRowViewModel)row.DataContext!;
        var p0 = row.TranslatePoint(new Point(40, row.Bounds.Height / 2), w)!.Value;

        w.MouseDown(p0, MouseButton.Left);
        // RawInputModifiers.LeftMouseButton è obbligatorio: senza, il codice legge il pulsante come
        // rilasciato e disarma — il trascinamento non parte e il test proverebbe il contrario.
        w.MouseMove(p0 + new Vector(0, 2), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        src.IsBeingDragged.Should().BeFalse("due pixel non sono un trascinamento");

        w.MouseMove(p0 + new Vector(0, 20), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        src.IsBeingDragged.Should().BeTrue();

        w.MouseUp(p0 + new Vector(0, 20), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        src.IsBeingDragged.Should().BeFalse();
        vm.SelectedRow.Should().BeNull("il rilascio che chiude un trascinamento non è un clic");
        w.Close();
    }

    [AvaloniaFact]
    public void Hovering_the_middle_of_a_group_lights_it_up_as_a_container()
    {
        var groupId = Guid.NewGuid();
        var (vm, _, _) = HomeFixture.Build(
            RowFixture.Row(Guid.NewGuid(), "a0", "#E11D48"),
            RowFixture.Row(groupId, "g1", "#E11D48", type: null, childCount: 1),
            RowFixture.Row(Guid.NewGuid(), "z0", "#E11D48", parent: groupId));
        var view = new HomeView { DataContext = vm };
        var w = new Window { Width = 900, Height = 600, Content = view };
        w.Show();
        vm.LoadCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var groupRow = view.GetVisualDescendants().OfType<GroupRow>().Single();
        var group = (HomeRowViewModel)groupRow.DataContext!;
        var src = view.GetVisualDescendants().OfType<HabitRow>().First(r => !ReferenceEquals(r.DataContext, group));
        var card = TreeDragDropBehavior.FindRowCard(groupRow.GetVisualDescendants().OfType<TextBlock>().First())!;

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(TreeDragDropBehavior.RowFormat, (HomeRowViewModel)src.DataContext!));

        // Metà esatta della card: zona Onto.
        var p = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height * 0.5), w)!.Value;
        w.DragDrop(p, RawDragEventType.DragEnter, data, DragDropEffects.Move);
        w.DragDrop(p, RawDragEventType.DragOver, data, DragDropEffects.Move);
        Dispatcher.UIThread.RunJobs();

        group.IsDropTarget.Should().BeTrue();
        group.DropEdge.Should().Be(RowDropEdge.None);
        w.Close();
    }

    [AvaloniaFact]
    public void Drag_over_a_row_paints_the_insertion_edge_and_the_drop_reparents()
    {
        var (w, view, _, nodes) = HomeViewTests.Show(Guid.NewGuid(), Guid.NewGuid());
        var srcRow = view.GetVisualDescendants().OfType<HabitRow>().ElementAt(0);
        var src = (HomeRowViewModel)srcRow.DataContext!;
        var targetRow = view.GetVisualDescendants().OfType<HabitRow>().ElementAt(1);
        var target = (HomeRowViewModel)targetRow.DataContext!;
        // Come in un trascinamento vero si parte da un discendente e si RISALE alla card.
        var card = TreeDragDropBehavior.FindRowCard(targetRow.GetVisualDescendants().OfType<TextBlock>().First())!;

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(TreeDragDropBehavior.RowFormat, src));

        // Terzo inferiore della card: zona After.
        var p = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height * 0.85), w)!.Value;
        // Il DragEnter è obbligatorio: senza, il DragOver non raggiunge nessun bersaglio (misurato).
        w.DragDrop(p, RawDragEventType.DragEnter, data, DragDropEffects.Move);
        w.DragDrop(p, RawDragEventType.DragOver, data, DragDropEffects.Move);
        Dispatcher.UIThread.RunJobs();
        target.DropEdge.Should().Be(RowDropEdge.After);

        w.DragDrop(p, RawDragEventType.Drop, data, DragDropEffects.Move);
        Dispatcher.UIThread.RunJobs();

        target.DropEdge.Should().Be(RowDropEdge.None);
        nodes.ReorderCalls.Should().ContainSingle()
            .Which.Should().Be((src.HabitNodeId, target.ParentId, target.HabitNodeId, true));
        w.Close();
    }

    [AvaloniaFact]
    public void Dropping_a_row_on_itself_does_nothing()
    {
        var (w, view, _, nodes) = HomeViewTests.Show(Guid.NewGuid(), Guid.NewGuid());
        var row = view.GetVisualDescendants().OfType<HabitRow>().First();
        var src = (HomeRowViewModel)row.DataContext!;
        var card = TreeDragDropBehavior.FindRowCard(row.GetVisualDescendants().OfType<TextBlock>().First())!;

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(TreeDragDropBehavior.RowFormat, src));

        var p = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height * 0.85), w)!.Value;
        w.DragDrop(p, RawDragEventType.DragEnter, data, DragDropEffects.Move);
        w.DragDrop(p, RawDragEventType.DragOver, data, DragDropEffects.Move);
        w.DragDrop(p, RawDragEventType.Drop, data, DragDropEffects.Move);
        Dispatcher.UIThread.RunJobs();

        src.DropEdge.Should().Be(RowDropEdge.None);
        nodes.ReorderCalls.Should().BeEmpty();
        w.Close();
    }
}
