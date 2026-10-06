using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Recordum.UI.Views.HabitScreen.Calendar;

public partial class CalendarNavigatorView : UserControl
{
    // Rampa del passo di intensita' 0..4 (stessa del gemello WPF: 0x00, 0x26, 0x4a, 0x78, 0xae su 255).
    private static readonly double[] Ramp = [0, 0x26 / 255.0, 0x4a / 255.0, 0x78 / 255.0, 0xae / 255.0];

    public static readonly IValueConverter StepOpacity =
        new FuncValueConverter<int, double>(step => Ramp[Math.Clamp(step, 0, 4)]);

    public CalendarNavigatorView() => InitializeComponent();
}
