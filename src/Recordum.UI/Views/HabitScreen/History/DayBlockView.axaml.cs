using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Recordum.Presentation.ViewModels.HabitScreen;
using Recordum.UI.Controls;
using Recordum.UI.Views.HabitScreen.Records;

namespace Recordum.UI.Views.HabitScreen.History;

public partial class DayBlockView : UserControl
{
    private HabitRecordsViewModel? _records;
    private bool _attached;
    private bool _editingDayNote;

    public DayBlockView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Bind(_attached);
        DayNoteBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) UpdateDayNoteMode(); };
        DayNoteBox.GotFocus += (_, _) => { _editingDayNote = true; UpdateDayNoteMode(); };
        DayNoteBox.LostFocus += (_, _) =>
        {
            _editingDayNote = false;
            UpdateDayNoteMode();
            // Lasciata la casella la nota torna resa, cioè sembra salvata: lo è davvero (mai perso, come nella colonna Note).
            if (DataContext is HabitScreenViewModel { DayNote: { CanSave: true } note }) note.SaveCommand.Execute(null);
        };
        DayNoteFrame.PointerPressed += (_, _) => EditDayNote();
        // Con la casella nascosta il Tab si ferma sulla cornice, e da lì si scrive: la nota resa resta raggiungibile da tastiera.
        DayNoteFrame.GotFocus += (_, e) => { if (ReferenceEquals(e.Source, DayNoteFrame)) EditDayNote(); };
        DayNoteBlock.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) UpdateDayNoteMode(); };
        UpdateDayNoteMode();
    }

    private void EditDayNote()
    {
        if (DayNoteBox.IsVisible) return;
        _editingDayNote = true;
        UpdateDayNoteMode();
        DayNoteBox.Focus();
        DayNoteBox.CaretIndex = DayNoteBox.Text?.Length ?? 0;
    }

    private DiaryEditor? _dayNoteRendered;

    // La nota del giorno si legge resa; il Markdown grezzo compare solo quando la casella è vuota o ha il fuoco. L'editor
    // che rende nasce alla prima nota da leggere: su un diario (blocco nascosto) e su un giorno senza nota non si costruisce.
    private void UpdateDayNoteMode()
    {
        var raw = _editingDayNote || string.IsNullOrWhiteSpace(DayNoteBox.Text) || !DayNoteBlock.IsVisible;
        DayNoteBox.IsVisible = raw;
        DayNoteRenderedHost.IsVisible = !raw;
        DayNoteFrame.Focusable = !raw;
        if (raw) return;
        _dayNoteRendered ??= new DiaryEditor { Name = "DayNoteRendered", IsReadOnly = true, MinHeight = 64 };
        _dayNoteRendered.Markdown = DayNoteBox.Text ?? string.Empty;
        DayNoteRenderedHost.Child = _dayNoteRendered;
    }

    // Iscritta solo a schermo: i Records vivono quanto la schermata e terrebbero in vita la colonna chiusa.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Bind(_attached = true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Bind(_attached = false);
    }

    private void Bind(bool attached)
    {
        if (_records is not null) _records.PropertyChanged -= OnRecordsPropertyChanged;
        _records = attached ? (DataContext as HabitScreenViewModel)?.Records : null;
        if (_records is not null) _records.PropertyChanged += OnRecordsPropertyChanged;
        if (attached) Rebuild();
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
