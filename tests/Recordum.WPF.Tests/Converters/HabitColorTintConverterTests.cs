using System.Globalization;
using System.Windows.Media;
using Recordum.WPF.Converters;

namespace Recordum.WPF.Tests.Converters;

public class HabitColorTintConverterTests
{
    [WpfFact]
    public void Step0_is_transparent()
    {
        var c = new HabitColorTintConverter();
        var b = (SolidColorBrush)c.Convert(new object[] { "#5fb37a", 0 }, typeof(Brush), null!, CultureInfo.InvariantCulture);
        Assert.Equal(0, b.Color.A);
    }

    [WpfFact]
    public void Higher_step_has_higher_alpha()
    {
        var c = new HabitColorTintConverter();
        var b1 = (SolidColorBrush)c.Convert(new object[] { "#5fb37a", 1 }, typeof(Brush), null!, CultureInfo.InvariantCulture);
        var b4 = (SolidColorBrush)c.Convert(new object[] { "#5fb37a", 4 }, typeof(Brush), null!, CultureInfo.InvariantCulture);
        Assert.True(b4.Color.A > b1.Color.A);
        Assert.Equal(0x5f, b4.Color.R); // colore abitudine preservato
    }
}
