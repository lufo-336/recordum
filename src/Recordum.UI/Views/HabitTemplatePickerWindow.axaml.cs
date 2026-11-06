using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Recordum.Presentation.ViewModels.HabitEditor;

namespace Recordum.UI.Views;

/// <summary>Gemella di <c>HabitTemplatePicker</c> (WPF): si chiude da sola quando il ViewModel ha creato l'abitudine o chiede
/// il creatore (vuoto o da un modello); il lanciatore legge poi il risultato dal ViewModel. Le due liste di schede condividono
/// una sola scelta (<see cref="HabitTemplatePickerViewModel.Selected"/>), tenuta qui e non con un binding two-way: la
/// ricerca toglie e rimette schede, e una lista che perde la sua voce non deve azzerare la scelta.</summary>
public partial class HabitTemplatePickerWindow : RecordumWindow
{
    /// <summary>Sotto questa larghezza il dettaglio della scheda scende sotto le schede: a 720 px, con i 340 del dettaglio,
    /// due schede per riga non ci starebbero.</summary>
    public const double NarrowWidth = 760;

    private readonly HabitTemplatePickerViewModel _vm;
    private bool _narrow;

    public HabitTemplatePickerWindow(HabitTemplatePickerViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        vm.PropertyChanged += OnVmPropertyChanged;
        vm.VisibleCustom.CollectionChanged += OnCustomChanged;
        YoursSection.IsVisible = vm.VisibleCustom.Count > 0;
        OfficialList.SelectionChanged += OnListSelectionChanged;
        CustomList.SelectionChanged += OnListSelectionChanged;
        // In tunnel: la lista si terrebbe Invio e, su una griglia a due colonne, le frecce non le saprebbe muovere.
        OfficialList.AddHandler(KeyDownEvent, OnListKeyDown, RoutingStrategies.Tunnel);
        CustomList.AddHandler(KeyDownEvent, OnListKeyDown, RoutingStrategies.Tunnel);
        LayoutGrid.SizeChanged += (_, e) => Arrange(e.NewSize.Width < NarrowWidth);
        Closed += (_, _) =>
        {
            vm.PropertyChanged -= OnVmPropertyChanged;
            vm.VisibleCustom.CollectionChanged -= OnCustomChanged;
        };
    }

    private void OnCustomChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        YoursSection.IsVisible = _vm.VisibleCustom.Count > 0;

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(HabitTemplatePickerViewModel.OpenBuilderRequested) when _vm.OpenBuilderRequested:
            case nameof(HabitTemplatePickerViewModel.CreatedHabitNodeId) when _vm.CreatedHabitNodeId is not null:
            case nameof(HabitTemplatePickerViewModel.CustomizeTemplateRequested) when _vm.CustomizeTemplateRequested is not null:
                Close();
                break;
            case nameof(HabitTemplatePickerViewModel.Selected):
                ShowSelection(OfficialList, _vm.VisibleOfficial);
                ShowSelection(CustomList, _vm.VisibleCustom);
                break;
        }
    }

    private void ShowSelection(ListBox list, System.Collections.ObjectModel.ObservableCollection<PickerOption> items) =>
        list.SelectedItem = _vm.Selected is { } s && items.Contains(s) ? s : null;

    // Solo cio' che si aggiunge diventa la scelta: una lista che si svuota (la scelta e' passata all'altra, la ricerca l'ha
    // tolta) non la azzera; ci pensa il ViewModel.
    private void OnListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (((ListBox)sender!).SelectedItem is PickerOption option) _vm.Selected = option;
    }

    // Invio su una scheda = Usa questo modello; le frecce si muovono nella griglia a due colonne (destra/sinistra di una scheda,
    // su/giu' di una riga). Solo i tasti della scheda: quelli del ⋮ (Invio apre il suo menu) e della casella di rinomina
    // passano in tunnel di qui ma restano loro.
    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is not ListBoxItem) return;
        var list = (ListBox)sender!;
        if (e.Key == Key.Enter)
        {
            _vm.UseSelectedCommand.Execute(null);
            e.Handled = true;
            return;
        }
        var step = e.Key switch { Key.Left => -1, Key.Right => 1, Key.Up => -Columns, Key.Down => Columns, _ => 0 };
        if (step == 0 || list.ItemCount == 0) return;
        var next = Math.Clamp(Math.Max(list.SelectedIndex, 0) + step, 0, list.ItemCount - 1);
        list.SelectedIndex = next;
        list.ContainerFromIndex(next)?.Focus(NavigationMethod.Directional);
        e.Handled = true;
    }

    private const int Columns = 2;   // le UniformGrid delle due liste

    private void Arrange(bool narrow)
    {
        if (narrow == _narrow) return;
        _narrow = narrow;
        ((Panel)DetailHost.Parent!).Children.Remove(DetailHost);
        if (narrow)
        {
            LayoutGrid.ColumnDefinitions[1].Width = new GridLength(0);
            DetailHost.BorderThickness = new Thickness(0, 1, 0, 0);
            DetailHost.Margin = new Thickness(0, 12, 0, 0);
            MainStack.Children.Add(DetailHost);
        }
        else
        {
            LayoutGrid.ColumnDefinitions[1].Width = new GridLength(340);
            DetailHost.BorderThickness = new Thickness(1, 0, 0, 0);
            DetailHost.Margin = default;
            LayoutGrid.Children.Add(DetailHost);
        }
    }

    // ===== Menu della scheda: Rinomina in linea, Elimina =====

    private void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is PickerOption option) _vm.DeleteCustomCommand.Execute(option);
    }

    private void OnRename(object? sender, RoutedEventArgs e)
    {
        if (((Control)sender!).DataContext is not PickerOption option) return;
        if (RenameBoxOf(option) is not { } box) return;
        box.Text = option.Name;
        box.IsVisible = true;
        box.Focus();
        box.SelectAll();
    }

    private TextBox? RenameBoxOf(PickerOption option) =>
        CustomList.ContainerFromItem(option)?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(b => b.Name == "RenameBox");

    private void OnRenameKeyDown(object? sender, KeyEventArgs e)
    {
        var box = (TextBox)sender!;
        if (e.Key == Key.Enter) CommitRename(box);
        else if (e.Key == Key.Escape) box.IsVisible = false;
        else return;
        e.Handled = true;
    }

    private void OnRenameLostFocus(object? sender, RoutedEventArgs e) => CommitRename((TextBox)sender!);

    private void CommitRename(TextBox box)
    {
        if (!box.IsVisible) return;
        box.IsVisible = false;
        if (box.DataContext is PickerOption option && !string.Equals(box.Text?.Trim(), option.Name, StringComparison.Ordinal))
            _vm.RenameCustomCommand.Execute((option, box.Text ?? string.Empty));
    }
}
