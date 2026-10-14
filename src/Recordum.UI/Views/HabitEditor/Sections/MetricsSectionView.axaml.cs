using Avalonia.Controls;
using Recordum.Core.Enums;

namespace Recordum.UI.Views.HabitEditor.Sections;

public partial class MetricsSectionView : UserControl
{
    public static readonly MetricType[] Types = Enum.GetValues<MetricType>();

    public MetricsSectionView() => InitializeComponent();
}
