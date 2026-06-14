using FluentAssertions;
using Recordum.WPF.Behaviors;
using Xunit;

namespace Recordum.WPF.Tests.Behaviors;

public class TreeDragDropBehaviorTests
{
    [Theory]
    [InlineData(0.0, TreeDragDropBehavior.DropZone.Before)]
    [InlineData(0.3, TreeDragDropBehavior.DropZone.Before)]
    [InlineData(0.5, TreeDragDropBehavior.DropZone.Onto)]
    [InlineData(0.85, TreeDragDropBehavior.DropZone.After)]
    [InlineData(1.0, TreeDragDropBehavior.DropZone.After)]
    public void Resolve_zone_uses_three_band_geometry(double relativeY, TreeDragDropBehavior.DropZone expected)
    {
        TreeDragDropBehavior.ResolveZone(relativeY).Should().Be(expected);
    }

    [Fact]
    public void Onto_group_plans_append_at_end()
    {
        var target = Guid.NewGuid();
        var plan = TreeDragDropBehavior.ResolveDrop(
            TreeDragDropBehavior.DropZone.Onto, targetIsGroup: true, targetId: target, targetParentId: null);
        plan.Should().Be(new TreeDragDropBehavior.DropPlan(target, null, false));
    }

    [Fact]
    public void Before_plans_insert_before_target_among_its_siblings()
    {
        var parent = Guid.NewGuid();
        var target = Guid.NewGuid();
        var plan = TreeDragDropBehavior.ResolveDrop(
            TreeDragDropBehavior.DropZone.Before, targetIsGroup: false, targetId: target, targetParentId: parent);
        plan.Should().Be(new TreeDragDropBehavior.DropPlan(parent, target, false));
    }

    [Fact]
    public void After_plans_insert_after_target_among_its_siblings()
    {
        var parent = Guid.NewGuid();
        var target = Guid.NewGuid();
        var plan = TreeDragDropBehavior.ResolveDrop(
            TreeDragDropBehavior.DropZone.After, targetIsGroup: false, targetId: target, targetParentId: parent);
        plan.Should().Be(new TreeDragDropBehavior.DropPlan(parent, target, true));
    }

    [Fact]
    public void Onto_non_group_falls_back_to_sibling_after_target()
    {
        var parent = Guid.NewGuid();
        var target = Guid.NewGuid();
        var plan = TreeDragDropBehavior.ResolveDrop(
            TreeDragDropBehavior.DropZone.Onto, targetIsGroup: false, targetId: target, targetParentId: parent);
        plan.Should().Be(new TreeDragDropBehavior.DropPlan(parent, target, true));
    }
}
