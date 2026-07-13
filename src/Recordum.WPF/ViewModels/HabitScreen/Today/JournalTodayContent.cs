using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.Today;

/// <summary>Journal "Oggi" content (spec §1): a single primary gesture — write today's diary entry or
/// start a quick note — chosen by <see cref="HabitUiPrefs.JournalGesture"/> (default Diary). The button
/// asks the host to open the matching column (Diario or Note). The full note-first + photo flow is Onda
/// 2.4; here the gesture only routes to the column.</summary>
public sealed partial class JournalTodayContent : ObservableObject, ITodayContent, ITodayContentNavigates
{
    private readonly IHabitUiPrefsService _uiPrefs;

    [ObservableProperty] private JournalPrimaryGesture _gesture = JournalPrimaryGesture.Diary;

    /// <summary>True when the primary gesture is the quick note (drives the button label/action).</summary>
    public bool IsQuickNote => Gesture == JournalPrimaryGesture.QuickNote;

    public event EventHandler<TodayContentAction>? ActionRequested;

    public JournalTodayContent(IHabitUiPrefsService uiPrefs)
    {
        ArgumentNullException.ThrowIfNull(uiPrefs);
        _uiPrefs = uiPrefs;
    }

    public async Task LoadAsync(HabitNode node, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        var prefs = await _uiPrefs.GetAsync(node.Id, ct).ConfigureAwait(true);
        Gesture = prefs.JournalGesture ?? JournalPrimaryGesture.Diary;
    }

    partial void OnGestureChanged(JournalPrimaryGesture value) => OnPropertyChanged(nameof(IsQuickNote));

    [RelayCommand]
    private void Primary()
        => ActionRequested?.Invoke(this, Gesture == JournalPrimaryGesture.QuickNote
            ? TodayContentAction.OpenQuickNote
            : TodayContentAction.OpenDiario);
}
