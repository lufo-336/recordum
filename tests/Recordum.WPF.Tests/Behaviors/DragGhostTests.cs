using System.Windows;
using System.Windows.Media;
using FluentAssertions;
using Recordum.WPF.Behaviors;
using Xunit;

namespace Recordum.WPF.Tests.Behaviors;

public class DragGhostTests
{
    [Fact]
    public void Physical_to_dip_applies_inverse_dpi_scale()
    {
        // 150% DPI: la trasformazione device->dip scala per 1/1.5.
        var deviceToDip = new Matrix(1.0 / 1.5, 0, 0, 1.0 / 1.5, 0, 0);

        var dip = DragGhost.PhysicalToDip(new Point(150, 300), deviceToDip);

        dip.X.Should().BeApproximately(100, 0.0001);
        dip.Y.Should().BeApproximately(200, 0.0001);
    }

    [Fact]
    public void Physical_to_dip_is_identity_at_100_percent()
    {
        var dip = DragGhost.PhysicalToDip(new Point(42, 7), Matrix.Identity);

        dip.Should().Be(new Point(42, 7));
    }
}
