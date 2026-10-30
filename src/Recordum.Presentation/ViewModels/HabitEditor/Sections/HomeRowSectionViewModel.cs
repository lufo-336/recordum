using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Core.Models;
using Recordum.Plugins.Contracts.Events;
using Recordum.Plugins.Contracts.Hosting;
using Recordum.Presentation.Resources;
using Recordum.Presentation.ViewModels.HabitBuilder.Preview;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

/// <summary>
/// "Home row" section (Onda 2.1): the two knobs — key figure and strip length — that drive
/// this habit's right-zone presentation on Home. Persists via <see cref="IHabitUiPrefsService"/>
/// (merged into the node's existing <see cref="HabitUiPrefs"/>). Only shown for habits, not groups.
/// The key-figure options are filtered to what the habit's type allows (<see cref="HomeRowPresentation"/>).
/// </summary>
public sealed partial class HomeRowSectionViewModel(IHabitUiPrefsService uiPrefs, IEventBus? events) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_HomeRow;

    /// <summary>L'etichetta del valore accanto al nome, poi quella della striscia.</summary>
    public override string Summary => (SelectedKeyFigure, SelectedStrip) switch
    {
        ({ } kf, { } strip) => $"{kf.Label} · {strip.Label}",
        ({ } kf, null) => kf.Label,
        _ => string.Empty,
    };

    /// <summary>La riga Home di campione con le scelte correnti; null finche' la sezione non e' caricata.</summary>
    [ObservableProperty] private HomeRowPreviewModel? _preview;

    private HabitType? _type;
    private (string Name, string Color, string? Icon) _identity = (string.Empty, "#888888", null);

    /// <summary>Nome, colore e icona dell'editor (li passa <c>HabitEditorViewModel</c>): la riga di campione li mostra.</summary>
    public void SetIdentity(string name, string color, string? icon)
    {
        _identity = (name, color, icon);
        RebuildPreview();
    }

    private void RebuildPreview()
    {
        if (_type is not { } type || SelectedKeyFigure is null || SelectedStrip is null) { Preview = null; return; }
        Preview = SamplePreviewFactory.HomeRowFor(type, SelectedKeyFigure.Kind, SelectedStrip.Strip,
            _identity.Name, _identity.Color, _identity.Icon, sample: false);   // un'abitudine vera: nessun numero inventato
    }

    private readonly IHabitUiPrefsService _uiPrefs = uiPrefs;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    public ObservableCollection<KeyFigureOption> KeyFigureOptions { get; } = new();
    public ObservableCollection<StripOption> StripOptions { get; } = new();

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private KeyFigureOption? _selectedKeyFigure;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private StripOption? _selectedStrip;

    public override bool IsApplicable(NodeType node, HabitType? habit)
        => node == NodeType.Habit && habit is not null;

    public async Task LoadAsync(HabitNode node)
    {
        _loading = true;
        try
        {
            NodeId = node.Id;
            var prefs = await _uiPrefs.GetAsync(node.Id).ConfigureAwait(true);
            var type = node.HabitType ?? HabitType.Action;
            _type = type;

            KeyFigureOptions.Clear();
            foreach (var kind in HomeRowPresentation.AllowedKeyFigures(type))
                KeyFigureOptions.Add(new KeyFigureOption(kind, KeyFigureLabel(kind)));
            var resolvedKf = HomeRowPresentation.ResolveKeyFigure(type, prefs);
            SelectedKeyFigure = KeyFigureOptions.FirstOrDefault(o => o.Kind == resolvedKf);

            StripOptions.Clear();
            foreach (var strip in new[] { HomeStrip.None, HomeStrip.Days7, HomeStrip.Days14 })
                StripOptions.Add(new StripOption(strip, StripLabel(strip)));
            var resolvedStrip = HomeRowPresentation.ResolveStrip(type, prefs);
            SelectedStrip = StripOptions.FirstOrDefault(o => o.Strip == resolvedStrip);
        }
        finally { _loading = false; }
        RebuildPreview();
    }

    partial void OnSelectedKeyFigureChanged(KeyFigureOption? value)
    {
        RebuildPreview();
        if (_loading || NodeId == Guid.Empty || value is null) return;
        _ = ApplyAsync(async () =>
        {
            await _uiPrefs.UpdateAsync(NodeId, p => p with { KeyFigure = HomeRowPresentation.IdFor(value.Kind) });
            Announce();
        });
    }

    partial void OnSelectedStripChanged(StripOption? value)
    {
        RebuildPreview();
        if (_loading || NodeId == Guid.Empty || value is null) return;
        _ = ApplyAsync(async () =>
        {
            await _uiPrefs.UpdateAsync(NodeId, p => p with { Strip = value.Strip });
            Announce();
        });
    }

    // Le preferenze non pubblicano eventi (le usa anche la persistenza delle colonne, a ogni apertura): e' questa
    // sezione a dire alla Home che la riga di QUESTA abitudine va riletta (stesso evento del nome/colore).
    private void Announce() => events?.Publish(new HabitUpdatedPayload(NodeId));

    private static string KeyFigureLabel(KeyFigureKind kind) => kind switch
    {
        KeyFigureKind.None => Strings.Editor_HomeRow_KF_None,
        KeyFigureKind.Streak => Strings.Editor_HomeRow_KF_Streak,
        KeyFigureKind.DoneToday => Strings.Editor_HomeRow_KF_DoneToday,
        KeyFigureKind.CounterTotal => Strings.Editor_HomeRow_KF_CounterTotal,
        KeyFigureKind.LiveClock => Strings.Editor_HomeRow_KF_LiveClock,
        KeyFigureKind.StateValue => Strings.Editor_HomeRow_KF_StateValue,
        KeyFigureKind.JournalToday => Strings.Editor_HomeRow_KF_JournalToday,
        KeyFigureKind.JournalEntriesWeek => Strings.Editor_HomeRow_KF_JournalEntriesWeek,
        _ => Strings.Editor_HomeRow_KF_None,
    };

    private static string StripLabel(HomeStrip strip) => strip switch
    {
        HomeStrip.None => Strings.Editor_HomeRow_Strip_None,
        HomeStrip.Days7 => Strings.Editor_HomeRow_Strip_Days7,
        HomeStrip.Days14 => Strings.Editor_HomeRow_Strip_Days14,
        _ => Strings.Editor_HomeRow_Strip_None,
    };
}

/// <summary>A selectable key-figure choice: the kind + its localized label.</summary>
public sealed record KeyFigureOption(KeyFigureKind Kind, string Label);

/// <summary>A selectable strip-length choice: the enum + its localized label.</summary>
public sealed record StripOption(HomeStrip Strip, string Label);
