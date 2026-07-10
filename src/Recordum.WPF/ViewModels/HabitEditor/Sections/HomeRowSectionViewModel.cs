using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.HomeRow;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

/// <summary>
/// "Home row" section (Onda 2.1): the two knobs — key figure and strip length — that drive
/// this habit's right-zone presentation on Home. Persists via <see cref="IHabitUiPrefsService"/>
/// (merged into the node's existing <see cref="HabitUiPrefs"/>). Only shown for habits, not groups.
/// The key-figure options are filtered to what the habit's type allows (<see cref="HomeRowPresentation"/>).
/// </summary>
public sealed partial class HomeRowSectionViewModel(IHabitUiPrefsService uiPrefs) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_HomeRow;

    private readonly IHabitUiPrefsService _uiPrefs = uiPrefs;
    private bool _loading;
    private HabitUiPrefs _current = new();
    internal Guid NodeId { get; private set; }

    public ObservableCollection<KeyFigureOption> KeyFigureOptions { get; } = new();
    public ObservableCollection<StripOption> StripOptions { get; } = new();

    [ObservableProperty] private KeyFigureOption? _selectedKeyFigure;
    [ObservableProperty] private StripOption? _selectedStrip;

    public override bool IsApplicable(NodeType node, HabitType? habit)
        => node == NodeType.Habit && habit is not null;

    public async Task LoadAsync(HabitNode node)
    {
        _loading = true;
        try
        {
            NodeId = node.Id;
            _current = await _uiPrefs.GetAsync(node.Id).ConfigureAwait(true);
            var type = node.HabitType ?? HabitType.Action;

            KeyFigureOptions.Clear();
            foreach (var kind in HomeRowPresentation.AllowedKeyFigures(type))
                KeyFigureOptions.Add(new KeyFigureOption(kind, KeyFigureLabel(kind)));
            var resolvedKf = HomeRowPresentation.ResolveKeyFigure(type, _current);
            SelectedKeyFigure = KeyFigureOptions.FirstOrDefault(o => o.Kind == resolvedKf);

            StripOptions.Clear();
            foreach (var strip in new[] { HomeStrip.None, HomeStrip.Days7, HomeStrip.Days14 })
                StripOptions.Add(new StripOption(strip, StripLabel(strip)));
            var resolvedStrip = HomeRowPresentation.ResolveStrip(type, _current);
            SelectedStrip = StripOptions.FirstOrDefault(o => o.Strip == resolvedStrip);
        }
        finally { _loading = false; }
    }

    partial void OnSelectedKeyFigureChanged(KeyFigureOption? value)
    {
        if (_loading || NodeId == Guid.Empty || value is null) return;
        _current = _current with { KeyFigure = HomeRowPresentation.IdFor(value.Kind) };
        _ = ApplyAsync(() => _uiPrefs.SetAsync(NodeId, _current).AsTask());
    }

    partial void OnSelectedStripChanged(StripOption? value)
    {
        if (_loading || NodeId == Guid.Empty || value is null) return;
        _current = _current with { Strip = value.Strip };
        _ = ApplyAsync(() => _uiPrefs.SetAsync(NodeId, _current).AsTask());
    }

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
