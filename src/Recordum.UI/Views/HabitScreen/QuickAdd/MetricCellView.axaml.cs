using Avalonia;
using Avalonia.Controls;
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
        Watch(IntegerBox, vm => vm.IntegerValue = null);
        Watch(DecimalBox, vm => vm.DecimalValue = null);
        Watch(DurationBox, vm => vm.DurationValue = null);
    }

    private bool _restoring;

    /// <summary>Ruling R10: un testo rifiutato dal binding svuota la cella, cosi' Salva non registra il valore
    /// vecchio mentre la casella mostra l'errore. Il testo digitato resta nella casella: svuotare il ViewModel lo
    /// riscriverebbe, quindi lo si rimette (il nuovo rifiuto non cambia piu' niente).</summary>
    private void Watch(TextBox box, Action<SeriesCellViewModel> clear) =>
        box.GetObservable(DataValidationErrors.HasErrorsProperty).Subscribe(new Observer(hasErrors =>
        {
            if (!hasErrors || _restoring || DataContext is not SeriesCellViewModel vm) return;
            var rejected = box.Text;
            _restoring = true;
            try
            {
                clear(vm);
                if (box.Text != rejected) box.Text = rejected;
            }
            finally { _restoring = false; }
        }));

    private sealed class Observer(Action<bool> onNext) : IObserver<bool>
    {
        public void OnNext(bool value) => onNext(value);
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
}
