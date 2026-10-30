using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Recordum.Presentation.ViewModels.HabitEditor;

namespace Recordum.UI.Views.HabitEditor;

/// <summary>Una voce di Configura: la riga col riassunto che apre sul posto la parte <see cref="Part"/> della sezione. Aperta =
/// <see cref="SectionViewModelBase.IsExpanded"/> per la parte principale, uno stato della riga per la parte avanzata. Visibile
/// se la sezione si applica (<see cref="SectionViewModelBase.IsVisible"/>) e ha un contenuto per quella parte.</summary>
public partial class ConfigRow : UserControl
{
    public static readonly StyledProperty<SectionViewModelBase?> SectionProperty =
        AvaloniaProperty.Register<ConfigRow, SectionViewModelBase?>(nameof(Section));

    public static readonly StyledProperty<SectionPart> PartProperty =
        AvaloniaProperty.Register<ConfigRow, SectionPart>(nameof(Part));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ConfigRow, bool>(nameof(IsOpen));

    /// <summary>Un avviso che si vede anche a riga chiusa (l'icona col tooltip): l'inizio della giornata rilegge lo storico.</summary>
    public static readonly StyledProperty<string?> WarningProperty =
        AvaloniaProperty.Register<ConfigRow, string?>(nameof(Warning));

    public SectionViewModelBase? Section { get => GetValue(SectionProperty); set => SetValue(SectionProperty, value); }
    public SectionPart Part { get => GetValue(PartProperty); set => SetValue(PartProperty, value); }
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public string? Warning { get => GetValue(WarningProperty); set => SetValue(WarningProperty, value); }

    private SectionViewModelBase? _hooked;

    public ConfigRow()
    {
        InitializeComponent();
        Root.DataContext = null;
        IsVisible = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SectionProperty || change.Property == PartProperty) Rehook();
        else if (change.Property == IsOpenProperty) ApplyOpen();
        else if (change.Property == WarningProperty)
        {
            WarningIcon.IsVisible = !string.IsNullOrEmpty(Warning);
            ToolTip.SetTip(WarningIcon, Warning);
        }
    }

    // L'editor vive piu' della colonna: la riga si iscrive alla sezione solo finche' e' nell'albero, o ogni apertura di
    // Configura lascerebbe righe e view appese al ViewModel.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Hook();
        UpdateVisibility();
        if (Part == SectionPart.Main && Section is not null) IsOpen = Section.IsExpanded;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unhook();
    }

    private void Hook()
    {
        Unhook();
        if (VisualRoot is null || Section is null) return;
        _hooked = Section;
        _hooked.PropertyChanged += OnSectionChanged;
    }

    private void Unhook()
    {
        if (_hooked is not null) _hooked.PropertyChanged -= OnSectionChanged;
        _hooked = null;
    }

    private void Rehook()
    {
        Hook();
        Root.DataContext = Section;
        ContentHost.Child = null;   // un'altra sezione: il contenuto si ricostruisce alla prossima apertura
        SummaryText.IsVisible = Part == SectionPart.Main;
        UpdateVisibility();
        IsOpen = Part == SectionPart.Main && Section?.IsExpanded == true;
        ApplyOpen();
    }

    private void OnSectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SectionViewModelBase.IsVisible)) UpdateVisibility();
        else if (e.PropertyName == nameof(SectionViewModelBase.IsExpanded) && Part == SectionPart.Main) IsOpen = Section!.IsExpanded;
    }

    private void UpdateVisibility() => IsVisible = Section is { IsVisible: true } s
        && (Part == SectionPart.Main || s.HasAdvanced) && SectionContent.Has(s, Part);

    private void ApplyOpen()
    {
        if (Part == SectionPart.Main && Section is not null) Section.IsExpanded = IsOpen;
        if (IsOpen && ContentHost.Child is null && Section is not null) ContentHost.Child = SectionContent.Build(Section, Part);
        ContentHost.IsVisible = IsOpen && ContentHost.Child is not null;
        Chevron.RenderTransform = IsOpen ? new RotateTransform(90) : null;   // la freccia a destra diventa in giu'
    }
}
