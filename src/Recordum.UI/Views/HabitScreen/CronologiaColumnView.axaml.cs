using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.UI.Views.HabitScreen.QuickAdd;
using Recordum.UI.Views.HabitScreen.Records;

namespace Recordum.UI.Views.HabitScreen;

/// <summary>Il corpo della colonna Cronologia. Esiste solo a colonna aperta (HabitScreenView lo costruisce e lo
/// butta): le sottoscrizioni al ViewModel, che vive piu' a lungo, si prendono in ingresso nell'albero visuale e si
/// rilasciano in uscita.</summary>
public partial class CronologiaColumnView : UserControl
{
    private HabitScreenViewModel? _vm;
    private HabitRecordsViewModel? _records;

    public CronologiaColumnView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => { if (VisualRoot is not null) Subscribe(); };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Subscribe();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        if (_records is not null) _records.PropertyChanged -= OnRecordsPropertyChanged;
        _vm = null; _records = null;
    }

    private void Subscribe()
    {
        Unsubscribe();
        _vm = DataContext as HabitScreenViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
        _records = _vm?.Records;
        if (_records is not null) _records.PropertyChanged += OnRecordsPropertyChanged;
        RebuildQuickAdd();
        RebuildRecords();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HabitScreenViewModel.QuickAddForCronologia)) RebuildQuickAdd();
    }

    private void OnRecordsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HabitRecordsViewModel.CurrentList)) RebuildRecords();
    }

    // Come QuickAddHost: la lista cambia tipo con l'abitudine (registrazioni / periodi), quindi si ricostruisce
    // dal tipo; ricostruire a ogni SetContext anche a tipo invariato e' innocuo (la lista e' la stessa istanza).
    private void RebuildRecords()
    {
        var content = _records?.CurrentList;
        if (RecordsHost.Child?.DataContext == content && content is not null) return;
        var view = RecordsTemplate.Instance.Build(content);
        if (view is not null) view.DataContext = content;
        RecordsHost.Child = view;
    }

    // Niente ContentControl: con un ContentTemplate statico il template si materializza anche a Content null.
    private void RebuildQuickAdd()
    {
        var content = _vm?.QuickAddForCronologia;
        var view = QuickAddTemplate.Instance.Build(content);
        if (view is not null) view.DataContext = content;
        QuickAddHost.Child = view;
    }

}
