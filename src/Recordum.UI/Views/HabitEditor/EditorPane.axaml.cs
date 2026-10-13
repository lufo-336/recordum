using Avalonia.Controls;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.UI.Converters;

namespace Recordum.UI.Views.HabitEditor;

/// <summary>Il corpo della colonna Configura: testata, sezioni e stato di salvataggio.</summary>
public partial class EditorPane : UserControl
{
    private HabitEditorViewModel? _vm;

    public EditorPane()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Rebind();
    }

    private void Rebind()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = DataContext as HabitEditorViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
        UpdateType();
    }

    private void OnVmChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HabitEditorViewModel.SelectedHabitType) or nameof(HabitEditorViewModel.SelectedNodeType)) UpdateType();
    }

    private void UpdateType() => TypeLabel.Text = _vm is null ? string.Empty
        : _vm.SelectedNodeType == NodeType.Group ? Strings.Editor_NodeType_Group
        : (string)HabitTypeLabelConverter.Instance.Convert(_vm.SelectedHabitType, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture);
}
