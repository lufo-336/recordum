using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.Presentation.ViewModels.HabitScreen.Today;

namespace Recordum.UI.Views.HabitScreen.Head;

/// <summary>La testa della schermata. Ricostruisce il comando quando cambiano il caso, il quick-add o il contenuto del
/// diario; sotto i 640 px il comando va sotto il nome, sotto i 420 i numeri vanno su due colonne.</summary>
public partial class HabitHeadView : UserControl
{
    private const double StackBelow = 640;
    private const double TwoFiguresBelow = 420;
    private HabitScreenViewModel? _vm;
    private TodayStripViewModel? _strip;

    private Flyout OtherDayFlyout => (Flyout)OtherDayButton.Flyout!;

    public HabitHeadView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Rebind();
        OtherDayFlyout.Opening += (_, _) => PrepareCalendar();
        OtherDayCalendar.SelectedDatesChanged += OnOtherDayPicked;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty) ApplyWidth(Bounds.Width);
    }

    private void ApplyWidth(double width)
    {
        if (width <= 0) return;
        var stacked = width < StackBelow;
        Grid.SetRow(CommandBlock, stacked ? 1 : 0);
        Grid.SetColumn(CommandBlock, stacked ? 0 : 1);
        CommandBlock.HorizontalAlignment = stacked ? Avalonia.Layout.HorizontalAlignment.Left : Avalonia.Layout.HorizontalAlignment.Right;
        CommandHost.HorizontalAlignment = CommandBlock.HorizontalAlignment;
        CommandBlock.Margin = stacked ? new Thickness(0, 10, 0, 0) : default;
        NameBlock.Margin = stacked ? default : new Thickness(0, 0, 16, 0);
        if (Figures.ItemsPanelRoot is UniformGrid grid) grid.Columns = width < TwoFiguresBelow ? 2 : 4;
    }

    private void Rebind()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        if (_strip is not null) _strip.PropertyChanged -= OnStripPropertyChanged;
        _vm = DataContext as HabitScreenViewModel;
        _strip = _vm?.TodayStrip;
        if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
        if (_strip is not null) _strip.PropertyChanged += OnStripPropertyChanged;
        RebuildCommand();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HabitScreenViewModel.Case) or nameof(HabitScreenViewModel.QuickAdd)) RebuildCommand();
    }

    private void OnStripPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TodayStripViewModel.Content) && _vm?.Case == HabitCase.Journal) RebuildCommand();
    }

    private void RebuildCommand()
    {
        CommandHost.Child = _vm is null ? null : HeadCommandTemplate.Build(_vm);
    }

    private void PrepareCalendar()
    {
        if (_vm is null) return;
        OtherDayCalendar.DisplayDateEnd = _vm.CurrentDay.ToDateTime(TimeOnly.MinValue);
        OtherDayCalendar.DisplayDate = _vm.SelectedDay.ToDateTime(TimeOnly.MinValue);
        OtherDayCalendar.SelectedDate = null;
    }

    private async void OnOtherDayPicked(object? sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_vm is null || OtherDayCalendar.SelectedDate is not { } picked) return;
            var day = DateOnly.FromDateTime(picked);
            if (day > _vm.CurrentDay) return;
            OtherDayFlyout.Hide();
            await _vm.SelectDayAsync(day);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"OnOtherDayPicked failed: {ex}"); }
    }
}
