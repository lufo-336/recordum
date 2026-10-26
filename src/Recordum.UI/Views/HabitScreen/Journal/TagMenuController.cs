using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Recordum.Presentation.ViewModels.HabitScreen.Journal;

namespace Recordum.UI.Views.HabitScreen.Journal;

/// <summary>Il menu <c>#</c> di una superficie di testo (la <c>TextBox</c> della nota veloce, il <c>DiaryEditor</c> del
/// diario). Il testo e il cursore vanno al ViewModel (<c>UpdateInlineQuery</c>), che dice quali suggerimenti mostrare; il
/// Popup si ancora sotto la riga del cursore e non ruba il fuoco. Frecce, Invio, Tab ed Esc si intercettano in tunnel
/// sulla superficie, cosi' Invio con il menu aperto sceglie e non va a capo. Fuori fuoco il menu si chiude, tranne se il
/// puntatore e' sul menu (e' una scelta col mouse). La view chiama <see cref="OnTextOrCaretChanged"/> a ogni cambio.</summary>
internal sealed class TagMenuController
{
    private readonly Popup _menu;
    private readonly ListBox _list;
    private readonly Func<string> _text;
    private readonly Func<int> _caret;
    private readonly Action<string, int> _apply;
    private readonly Func<Rect?> _caretRect;
    private readonly Action _focus;
    private JournalQuickEntryViewModel? _vm;
    private string? _lastText;
    // Esc chiude il menu fino alla prossima battuta (il ViewModel continua a offrire i suggerimenti).
    private bool _dismissed;

    /// <param name="surface">Dove si scrive: riceve i tasti in tunnel e la perdita del fuoco.</param>
    /// <param name="apply">Mette testo e cursore nuovi nella superficie (dopo una scelta).</param>
    /// <param name="caretRect">Il rettangolo del cursore nelle coordinate del PlacementTarget del menu; null = nessun layout.</param>
    public TagMenuController(Popup menu, ListBox list, InputElement surface, Func<string> text, Func<int> caret,
        Action<string, int> apply, Func<Rect?> caretRect, Action focus)
    {
        _menu = menu;
        _list = list;
        _text = text;
        _caret = caret;
        _apply = apply;
        _caretRect = caretRect;
        _focus = focus;
        surface.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        surface.LostFocus += (_, _) => { if (_menu.Child is not { IsPointerOver: true }) _menu.IsOpen = false; };
        _list.Tapped += (_, _) => { if (_list.SelectedItem is string name) Accept(name); };
    }

    public JournalQuickEntryViewModel? ViewModel
    {
        get => _vm;
        set
        {
            if (_vm is not null) _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = value;
            if (_vm is not null) _vm.PropertyChanged += OnVmPropertyChanged;
            Update();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(JournalQuickEntryViewModel.InlineSuggestions)) Update();
    }

    public void OnTextOrCaretChanged()
    {
        if (_vm is null) return;
        var text = _text();
        if (text != _lastText) { _dismissed = false; _lastText = text; } // una battuta riapre il menu chiuso con Esc
        _vm.UpdateInlineQuery(text, _caret());
        Update();
    }

    private void Update()
    {
        var any = _vm is { InlineSuggestions.Count: > 0 };
        if (any && _list.SelectedIndex < 0) _list.SelectedIndex = 0;
        if (any) _menu.PlacementRect = _caretRect();
        _menu.IsOpen = any && !_dismissed;
    }

    private void Accept(string name)
    {
        if (_vm is null) return;
        var (text, caret) = _vm.AcceptInlineSuggestion(name);
        _apply(text, caret);
        _focus();
        Update();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_menu.IsOpen) return;
        var count = _vm?.InlineSuggestions.Count ?? 0;
        switch (e.Key)
        {
            case Key.Down:
                _list.SelectedIndex = Math.Min(count - 1, _list.SelectedIndex + 1);
                e.Handled = true;
                break;
            case Key.Up:
                _list.SelectedIndex = Math.Max(0, _list.SelectedIndex - 1);
                e.Handled = true;
                break;
            case Key.Enter or Key.Tab:
                if ((_list.SelectedItem as string ?? First()) is { } name) Accept(name);
                e.Handled = true;
                break;
            case Key.Escape:
                _dismissed = true;
                _menu.IsOpen = false;
                e.Handled = true;
                break;
        }
    }

    private string? First() => _vm is { InlineSuggestions.Count: > 0 } ? _vm.InlineSuggestions[0] : null;
}
