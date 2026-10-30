using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Recordum.UI.Views.HabitEditor;

public partial class GroupContentView : UserControl
{
    /// <summary>Solo le figlie dirette: le righe della Panoramica hanno livello 0 per loro, di piu' per i nipoti.</summary>
    public static readonly IValueConverter IsTopLevel = new FuncValueConverter<int, bool>(level => level == 0);

    public GroupContentView() => InitializeComponent();
}
