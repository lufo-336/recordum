using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

/// <summary>"Registrazione" section: come si registra questa abitudine. Per i counter e' il flag
/// ripetibile (U8, persistito su habit_config); per i journal e' il gesto primario (U7, persistito
/// in ui_prefs_json). Due tipi, due manopole, due percorsi di persistenza — ma una sola domanda.</summary>
public sealed partial class RegistrationSectionViewModel(
    IHabitConfigService configs,
    IHabitUiPrefsService uiPrefs) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Registration;

    private readonly IHabitConfigService _configs = configs;
    private readonly IHabitUiPrefsService _uiPrefs = uiPrefs;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private bool _repeatable;
    [ObservableProperty] private bool _showRepeatable;
    [ObservableProperty] private bool _showJournalGesture;
    [ObservableProperty] private JournalGestureOption? _selectedGesture;

    /// <summary>Lista fissa: non e' mai svuotata, quindi non puo' innescare la coercizione-a-null
    /// che ha causato i bug 4d01f9c / 1d96967.</summary>
    public IReadOnlyList<JournalGestureOption> GestureOptions { get; } =
    [
        new(JournalPrimaryGesture.Diary, Strings.Editor_Registration_Gesture_Diary),
        new(JournalPrimaryGesture.QuickNote, Strings.Editor_Registration_Gesture_QuickNote),
    ];

    public override bool IsApplicable(NodeType node, HabitType? habit)
        => node == NodeType.Habit && habit is HabitType.Counter or HabitType.Journal;

    public async Task LoadAsync(HabitNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _loading = true;
        try
        {
            NodeId = node.Id;
            ShowRepeatable = node.HabitType == HabitType.Counter;
            ShowJournalGesture = node.HabitType == HabitType.Journal;

            var cfg = await _configs.GetAsync(node.Id).ConfigureAwait(true);
            Repeatable = cfg?.Repeatable ?? false;

            var prefs = await _uiPrefs.GetAsync(node.Id).ConfigureAwait(true);
            var gesture = prefs.JournalGesture ?? JournalPrimaryGesture.Diary;
            SelectedGesture = GestureOptions.First(o => o.Gesture == gesture);
        }
        finally { _loading = false; }
    }

    partial void OnRepeatableChanged(bool value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.Repeatable = value).AsTask());
    }

    partial void OnSelectedGestureChanged(JournalGestureOption? value)
    {
        if (_loading || NodeId == Guid.Empty || value is null) return;
        _ = ApplyAsync(() => _uiPrefs.UpdateAsync(NodeId, p => p with { JournalGesture = value.Gesture }).AsTask());
    }
}

/// <summary>Una scelta di gesto primario journal: l'enum + la sua etichetta localizzata.</summary>
public sealed record JournalGestureOption(JournalPrimaryGesture Gesture, string Label);
