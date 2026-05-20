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
}
