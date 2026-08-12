using System.Windows.Controls;

namespace Recordum.WPF.Views.HabitBuilder;

/// <summary>The builder's three-tier live preview (E7): the row as it will look in Home, the "Oggi"
/// strip content, and the recording module. Bound to a
/// <see cref="ViewModels.HabitBuilder.Preview.BuilderPreviewModel"/> as DataContext.</summary>
public partial class BuilderPreviewPane : UserControl
{
    public BuilderPreviewPane() => InitializeComponent();
}
