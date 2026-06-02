using System.ComponentModel;
using System.Windows;
using Recordum.WPF.ViewModels.HabitEditor;

namespace Recordum.WPF.Views;

public partial class HabitTemplatePicker : Window
{
    public Guid? CreatedHabitNodeId { get; private set; }

    public HabitTemplatePicker(HabitTemplatePickerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not HabitTemplatePickerViewModel vm) return;
        if (e.PropertyName == nameof(HabitTemplatePickerViewModel.OpenBuilderRequested) && vm.OpenBuilderRequested)
        {
            DialogResult = true;
            Close();
            return;
        }
        if (e.PropertyName != nameof(HabitTemplatePickerViewModel.CreatedHabitNodeId)) return;
        if (vm.CreatedHabitNodeId is null) return;
        CreatedHabitNodeId = vm.CreatedHabitNodeId;
        DialogResult = true;
        Close();
    }
}
