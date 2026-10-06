using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Recordum.UI.Views.HabitScreen.QuickAdd;

public partial class SeriesQuickAddView : UserControl
{
    /// <summary>Serie valida: marcatore pieno; vuota: attenuato.</summary>
    public static readonly IValueConverter ValidityOpacity =
        new FuncValueConverter<bool, double>(valid => valid ? 1d : 0.35);

    public SeriesQuickAddView() => InitializeComponent();
}
