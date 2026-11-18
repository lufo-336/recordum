using System.ComponentModel;
using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitEditor.Sections;

namespace Recordum.UI.Views.HabitEditor.Sections;

public partial class ScalesSectionView : UserControl
{
    private ScalesSectionViewModel? _vm;
    private Guid? _shownId;
    private string? _shownName;

    public ScalesSectionView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
            _vm = DataContext as ScalesSectionViewModel;
            if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
            (_shownId, _shownName) = (_vm?.SelectedScaleTemplateId, _vm?.Summary);
        };
    }

    /// <summary>Rinominare una scala cambia <c>Name</c> sul posto (il ComboBox non si svuota: CLAUDE.md 8), e un oggetto che
    /// non notifica non riscrive il testo del picker: si rifa' il template, che ridisegna la casella e l'elenco.</summary>
    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ScalesSectionViewModel.Summary) || _vm is null) return;
        var (id, name) = (_vm.SelectedScaleTemplateId, _vm.Summary);
        var renamed = id == _shownId && name != _shownName;
        (_shownId, _shownName) = (id, name);
        if (!renamed) return;
        var t = ScaleBox.ItemTemplate;
        ScaleBox.ItemTemplate = null;
        ScaleBox.ItemTemplate = t;
    }
}
