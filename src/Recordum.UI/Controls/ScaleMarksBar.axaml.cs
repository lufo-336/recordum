using Avalonia;
using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.UI.Controls;

public partial class ScaleMarksBar : UserControl
{
    public static readonly StyledProperty<IReadOnlyList<ScalePoint>?> PointsProperty =
        AvaloniaProperty.Register<ScaleMarksBar, IReadOnlyList<ScalePoint>?>(nameof(Points));

    public static readonly StyledProperty<bool> ShowNamesProperty =
        AvaloniaProperty.Register<ScaleMarksBar, bool>(nameof(ShowNames), true);

    public IReadOnlyList<ScalePoint>? Points { get => GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public bool ShowNames { get => GetValue(ShowNamesProperty); set => SetValue(ShowNamesProperty, value); }

    public ScaleMarksBar() => InitializeComponent();
}
