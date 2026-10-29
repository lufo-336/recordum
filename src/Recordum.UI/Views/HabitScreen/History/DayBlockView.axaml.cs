using System.ComponentModel;
using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.UI.Views.HabitScreen.Records;

namespace Recordum.UI.Views.HabitScreen.History;

public partial class DayBlockView : UserControl
{
    private HabitRecordsViewModel? _records;

    public DayBlockView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_records is not null) _records.PropertyChanged -= OnRecordsPropertyChanged;
            _records = (DataContext as HabitScreenViewModel)?.Records;
            if (_records is not null) _records.PropertyChanged += OnRecordsPropertyChanged;
            Rebuild();
        };
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
