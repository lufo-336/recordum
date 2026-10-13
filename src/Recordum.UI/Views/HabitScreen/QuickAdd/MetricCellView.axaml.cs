using Avalonia;
using Avalonia.Controls;
using Recordum.UI.Behaviors;
using Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

namespace Recordum.UI.Views.HabitScreen.QuickAdd;

public partial class MetricCellView : UserControl
{
    /// <summary>Il quick-add che ospita la cella: porta il cronometro condiviso (comando e orologio).</summary>
    public static readonly StyledProperty<SeriesQuickAddViewModelBase?> HostProperty =
        AvaloniaProperty.Register<MetricCellView, SeriesQuickAddViewModelBase?>(nameof(Host));

    public SeriesQuickAddViewModelBase? Host
    {
        get => GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    public MetricCellView()
    {
        InitializeComponent();
        RejectedTextGuard.Watch<SeriesCellViewModel>(IntegerBox, this, vm => vm.IntegerValue = null);
        RejectedTextGuard.Watch<SeriesCellViewModel>(DecimalBox, this, vm => vm.DecimalValue = null);
        RejectedTextGuard.Watch<SeriesCellViewModel>(DurationBox, this, vm => vm.DurationValue = null);
    }
}
