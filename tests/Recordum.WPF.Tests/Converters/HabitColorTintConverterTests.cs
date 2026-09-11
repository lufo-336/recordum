using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Recordum.Core.Abstractions;
using Recordum.WPF.Converters;
using Recordum.WPF.Tests.Fixtures;

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

    [WpfFact]
    public void Danger_tint_overrides_the_habit_color()
    {
        // Terzo valore MarkerTint.Danger: il colore restituito viene da Brush.Danger nel tema
        // CORRENTE del processo di test (Light di default, App.xaml), non dall'esadecimale
        // dell'abitudine passato in values[0] — qualunque sia il tema attivo, il suo canale R
        // (0xDC chiaro / 0xF8 scuro) non puo' mai coincidere con 0x5f dell'abitudine.
        TestApp.EnsureLoaded();
        var c = new HabitColorTintConverter();
        var b = (SolidColorBrush)c.Convert(
            new object[] { "#5fb37a", 4, MarkerTint.Danger }, typeof(Brush), null!, CultureInfo.InvariantCulture);
        Assert.NotEqual(0x5f, b.Color.R);
        Assert.Equal(((SolidColorBrush)Application.Current.TryFindResource("Brush.Danger")).Color.R, b.Color.R);
    }
}
