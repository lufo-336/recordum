using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Recordum.Presentation.ViewModels.HabitEditor;

namespace Recordum.UI.Views;

/// <summary>Gemella di <c>HabitTemplatePicker</c> (WPF): si chiude da sola quando il ViewModel ha creato
/// l'abitudine o chiede il builder; il lanciatore legge poi il risultato dal ViewModel.</summary>
public partial class HabitTemplatePickerWindow : RecordumWindow
{
    private readonly HabitTemplatePickerViewModel _vm;

    public HabitTemplatePickerWindow(HabitTemplatePickerViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        vm.PropertyChanged += OnVmPropertyChanged;
        vm.CustomOptions.CollectionChanged += (_, _) => YoursSection.IsVisible = vm.CustomOptions.Count > 0;
        YoursSection.IsVisible = vm.CustomOptions.Count > 0;
        Closed += (_, _) => vm.PropertyChanged -= OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if ((e.PropertyName == nameof(HabitTemplatePickerViewModel.OpenBuilderRequested) && _vm.OpenBuilderRequested)
            || (e.PropertyName == nameof(HabitTemplatePickerViewModel.CreatedHabitNodeId) && _vm.CreatedHabitNodeId is not null))
            Close();
    }

    private void OnPick(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is PickerOption option) _vm.PickCommand.Execute(option);
    }

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is PickerOption option) _vm.DeleteCustomCommand.Execute(option);
    }

    private void OnRename(object? sender, RoutedEventArgs e)
    {
        var button = (Control)sender!;
        if (button.DataContext is not PickerOption option) return;
        var box = button.GetVisualAncestors().OfType<Panel>().First().Children.OfType<TextBox>().First();
        _vm.RenameCustomCommand.Execute((option, box.Text ?? string.Empty));
    }
}
