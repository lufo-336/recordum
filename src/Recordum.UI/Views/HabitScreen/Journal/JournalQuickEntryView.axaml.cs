using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;

namespace Recordum.UI.Views.HabitScreen.Journal;

/// <summary>Il composer della nota veloce. Il menu <c>#</c> e' un Popup sotto il cursore: il testo e il cursore vanno al
/// ViewModel (<c>UpdateInlineQuery</c>), che dice quali suggerimenti mostrare; le frecce, Invio, Tab ed Esc si
/// intercettano in tunnel sulla TextBox, cosi' Invio con il menu aperto accetta e non va a capo.</summary>
public partial class JournalQuickEntryView : UserControl
{
    private JournalQuickEntryViewModel? _vm;
    // Esc chiude il menu fino alla prossima battuta (il ViewModel continua a offrire i suggerimenti).
    private bool _dismissed;

    public JournalQuickEntryView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Rebind();
        DraftBox.AddHandler(KeyDownEvent, OnDraftKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        DraftBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) OnTextOrCaretChanged(typing: true);
            else if (e.Property == TextBox.CaretIndexProperty) OnTextOrCaretChanged(typing: false);
        };
        // Fuori fuoco il menu non deve coprire la riga Tag; se il puntatore e' sul menu e' una scelta col mouse: resta.
        DraftBox.LostFocus += (_, _) => { if (!TagMenu.Child!.IsPointerOver) TagMenu.IsOpen = false; };
        InlineList.Tapped += (_, _) => { if (InlineList.SelectedItem is string name) Accept(name); };
    }

    private void Rebind()
    {
        if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = DataContext as JournalQuickEntryViewModel;
        if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
        UpdateMenu();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JournalQuickEntryViewModel.InlineSuggestions)) UpdateMenu();
    }

    private void OnTextOrCaretChanged(bool typing)
    {
        if (_vm is null) return;
        if (typing) _dismissed = false;
        _vm.UpdateInlineQuery(DraftBox.Text ?? string.Empty, DraftBox.CaretIndex);
        UpdateMenu();
    }

    private void UpdateMenu()
    {
        var any = _vm is { InlineSuggestions.Count: > 0 };
        if (any && InlineList.SelectedIndex < 0) InlineList.SelectedIndex = 0;
        if (any) PlaceUnderCaret();
        TagMenu.IsOpen = any && !_dismissed;
    }

    // Il menu si ancora alla riga del cursore, non al fondo dell'intera TextBox.
    private void PlaceUnderCaret()
    {
        var presenter = DraftBox.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        if (presenter?.TextLayout is not { } layout) { TagMenu.PlacementRect = null; return; }
        var caret = layout.HitTestTextPosition(DraftBox.CaretIndex);
        var top = presenter.TranslatePoint(caret.TopLeft, DraftBox);
        TagMenu.PlacementRect = top is { } p ? new Rect(p.X, p.Y, 1, caret.Height) : null;
    }

    private void Accept(string name)
    {
        if (_vm is null) return;
        var (text, caret) = _vm.AcceptInlineSuggestion(name);
        DraftBox.Text = text;
        DraftBox.CaretIndex = caret;
        DraftBox.Focus();
        UpdateMenu();
    }

    private string? First() => _vm is { InlineSuggestions.Count: > 0 } ? _vm.InlineSuggestions[0] : null;

    private void OnDraftKeyDown(object? sender, KeyEventArgs e)
    {
        if (!TagMenu.IsOpen) return;
        var count = _vm?.InlineSuggestions.Count ?? 0;
        switch (e.Key)
        {
            case Key.Down:
                InlineList.SelectedIndex = Math.Min(count - 1, InlineList.SelectedIndex + 1);
                e.Handled = true;
                break;
            case Key.Up:
                InlineList.SelectedIndex = Math.Max(0, InlineList.SelectedIndex - 1);
                e.Handled = true;
                break;
            case Key.Enter or Key.Tab:
                if ((InlineList.SelectedItem as string ?? First()) is { } name) Accept(name);
                e.Handled = true;
                break;
            case Key.Escape:
                _dismissed = true;
                TagMenu.IsOpen = false;
                e.Handled = true;
                break;
        }
    }
}
