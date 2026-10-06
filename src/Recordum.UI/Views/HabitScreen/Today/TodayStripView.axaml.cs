using System.ComponentModel;
using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitScreen.Today;

namespace Recordum.UI.Views.HabitScreen.Today;

/// <summary>La fascia Oggi: costruisce la view del tipo tramite <see cref="TodayContentTemplate"/> e si
/// collassa (bordo compreso) quando il contenuto e' null o il template non ha una view per esso.
/// Niente ContentControl: con un ContentTemplate statico il template si materializza anche a Content null.</summary>
public partial class TodayStripView : UserControl
{
    private TodayStripViewModel? _vm;

    public TodayStripView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Rebind();
    }

    private void Rebind()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as TodayStripViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
        Rebuild();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TodayStripViewModel.Content)) Rebuild();
    }

    private void Rebuild()
    {
        var content = _vm?.Content;
        var view = TodayContentTemplate.Instance.Build(content);
        if (view is not null) view.DataContext = content;
        Frame.Child = view;
        IsVisible = view is not null;
    }
}
