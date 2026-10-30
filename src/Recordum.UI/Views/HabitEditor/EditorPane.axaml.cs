using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Recordum.Core.Enums;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitEditor;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.UI.Converters;

namespace Recordum.UI.Views.HabitEditor;

/// <summary>Il corpo della colonna Configura: i blocchi di un'abitudine o di un gruppo e lo stato di salvataggio.</summary>
public partial class EditorPane : UserControl
{
    /// <summary>La Panoramica della schermata: il blocco "Contenuto" di un gruppo ne mostra le righe. La imposta
    /// <c>HabitScreenView</c> quando costruisce la colonna (il DataContext del pane e' l'editor).</summary>
    public static readonly StyledProperty<PanoramicaViewModel?> GroupProperty =
        AvaloniaProperty.Register<EditorPane, PanoramicaViewModel?>(nameof(Group));

    public PanoramicaViewModel? Group { get => GetValue(GroupProperty); set => SetValue(GroupProperty, value); }

    private HabitEditorViewModel? _vm;
    private readonly List<ConfigRow> _rows;

    public EditorPane()
    {
        InitializeComponent();
        GroupContent.DataContext = null;
        _rows = SectionsStack.GetLogicalDescendants().OfType<ConfigRow>().ToList();
        foreach (var row in _rows)
            row.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) UpdateBlocks(); };
        AdvancedToggle.IsCheckedChanged += (_, _) => ApplyAdvanced();
        DataContextChanged += (_, _) => Rebind();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GroupProperty) GroupContent.DataContext = Group;
    }

    private void ApplyAdvanced()
    {
        AdvancedRows.IsVisible = AdvancedToggle.IsChecked == true;
        AdvancedChevron.RenderTransform = AdvancedRows.IsVisible ? new RotateTransform(90) : null;
    }

    // L'editor vive piu' della colonna: il pane lo ascolta solo finche' e' nell'albero, o ogni apertura di Configura
    // lascerebbe un pane intero appeso al ViewModel (come ConfigRow).
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebind();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
    }

    private void Rebind()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
        _vm = DataContext as HabitEditorViewModel;
        if (_vm is not null && VisualRoot is not null) _vm.PropertyChanged += OnVmChanged;
        UpdateType();
    }

    private void OnVmChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(HabitEditorViewModel.SelectedHabitType) or nameof(HabitEditorViewModel.SelectedNodeType)) UpdateType();
    }

    private bool IsGroup => _vm?.SelectedNodeType == NodeType.Group;

    private void UpdateType()
    {
        TypeLabel.Text = _vm is null ? string.Empty
            : IsGroup ? Strings.Editor_NodeType_Group
            : (string)HabitTypeLabelConverter.Instance.Convert(_vm.SelectedHabitType, typeof(string), null, System.Globalization.CultureInfo.CurrentCulture);

        // Un gruppo vede Tag in Avanzate (il suo Chi e' e' nome, descrizione, colore, icona) e Obiettivo e Inizio della
        // giornata in "Per le figlie"; un'abitudine li vede in Chi e' e Quando conta. Una sezione, una riga sola.
        var group = IsGroup;
        IdentityTagsRow.Section = group ? null : _vm?.Tags;
        AdvancedTagsRow.Section = group ? _vm?.Tags : null;
        WhenGoalRow.Section = group ? null : _vm?.Goal;
        ChildrenGoalRow.Section = group ? _vm?.Goal : null;
        WhenResetRow.Section = group ? null : _vm?.ResetTime;
        ChildrenResetRow.Section = group ? _vm?.ResetTime : null;
        UpdateBlocks();
    }

    // Un blocco senza voci visibili non si mostra (un'azione non ha niente in "Cosa registri"); quelli dell'altro tipo di nodo mai.
    private void UpdateBlocks()
    {
        var group = IsGroup;
        static bool AnyRow(Panel block) => block.Children.OfType<ConfigRow>().Any(r => r.IsVisible);
        RecordBlock.IsVisible = !group && AnyRow(RecordBlock);
        WhenBlock.IsVisible = !group && AnyRow(WhenBlock);
        HomeBlock.IsVisible = !group && AnyRow(HomeBlock);
        ForChildrenBlock.IsVisible = group;
        ContentBlock.IsVisible = group;
        SaveTemplateRow.IsVisible = group;
        AdvancedBlock.IsVisible = AnyRow(AdvancedRows);
    }
}
