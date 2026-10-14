using Avalonia.Controls;
using Avalonia.Data.Converters;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;

namespace Recordum.UI.Views.HabitEditor.Sections;

public partial class MetricsSectionView : UserControl
{
    public static readonly MetricType[] Types = Enum.GetValues<MetricType>();

    public static readonly IValueConverter TypeLabel = new FuncValueConverter<MetricType, string>(v => v switch
    {
        MetricType.Integer => Strings.Editor_MetricType_Integer,
        MetricType.Decimal => Strings.Editor_MetricType_Decimal,
        MetricType.Seconds => Strings.Editor_MetricType_Seconds,
        MetricType.Text => Strings.Editor_MetricType_Text,
        _ => string.Empty,
    });

    public MetricsSectionView() => InitializeComponent();
}
