using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.UI.Views.HabitScreen.Records;

namespace Recordum.UI.Views.HabitScreen.History;

public partial class DayBlockView : UserControl
{
    private HabitRecordsViewModel? _records;
    private bool _attached;

    public DayBlockView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(_attached);
    }

    // Iscritta solo a schermo: i Records vivono quanto la schermata e terrebbero in vita la colonna chiusa.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Bind(_attached = true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Bind(_attached = false);
    }

    private void Bind(bool attached)
    {
        if (_records is not null) _records.PropertyChanged -= OnRecordsPropertyChanged;
        _records = attached ? (DataContext as HabitScreenViewModel)?.Records : null;
        if (_records is not null) _records.PropertyChanged += OnRecordsPropertyChanged;
        if (attached) Rebuild();
    }

    private void OnRecordsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HabitRecordsViewModel.CurrentList)) Rebuild();
    }

    // La lista cambia tipo con l'abitudine (registrazioni / periodi): si costruisce dal tipo (niente ContentControl).
    private void Rebuild()
    {
        var content = _records?.CurrentList;
        var view = RecordsTemplate.Instance.Build(content);
        if (view is not null) view.DataContext = content;
        RecordsHost.Child = view;
    }
}
