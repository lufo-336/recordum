using System.Windows;
using System.Windows.Controls;
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

    [WpfFact]
    public void Set_screen_position_opens_the_popup_once()
    {
        var card = new Border { Width = 200, Height = 40, Background = Brushes.Gray };
        card.Measure(new Size(200, 40));
        card.Arrange(new Rect(0, 0, 200, 40));
        card.UpdateLayout();

        using var ghost = new DragGhost(card);
        ghost.IsOpen.Should().BeFalse("il popup non si apre finché non arriva una posizione");

        ghost.SetScreenPosition(new Point(100, 100));

        ghost.IsOpen.Should().BeTrue();
    }

    [WpfFact]
    public void Dispose_closes_the_popup_and_is_idempotent()
    {
        var card = new Border { Width = 120, Height = 30, Background = Brushes.Gray };
        card.Measure(new Size(120, 30));
        card.Arrange(new Rect(0, 0, 120, 30));
        card.UpdateLayout();

        var ghost = new DragGhost(card);
        ghost.SetScreenPosition(new Point(50, 50));

        ghost.Dispose();
        ghost.IsOpen.Should().BeFalse();

        var act = () => ghost.Dispose();
        act.Should().NotThrow();
        ghost.SetScreenPosition(new Point(10, 10)); // no-op dopo dispose
        ghost.IsOpen.Should().BeFalse();
    }
}
