using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitBuilder;

/// <summary>One row of the Sessione segment list — a named leg of the session (Work/Rest/Cooldown…),
/// offered by <see cref="SessionBrickViewModel"/>.</summary>
public sealed partial class SegmentRowViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isRest;
}

/// <summary>Mattone Sessione (Onda 2.5 T5): integrated timer + named segments of the draft, applied to
/// the <see cref="HabitConfig"/> (timer flags) and <see cref="ComposedHabitRequest"/> (segments) by the
/// host. Vista sul draft, non chiama servizi (spec §1: host-agnostico) — mirror of the reader,
/// <c>HabitTemplateService.cs:116</c> (<c>TimerMode.Session</c> implies <c>SessionBaseTimer</c>).</summary>
public sealed partial class SessionBrickViewModel : ObservableObject
{
    [ObservableProperty] private bool _hasSession;

    /// <summary>Impostato dall'host in <c>Recompute()</c>: true quando <c>DerivedType == Counter &&
    /// !CountsOccurrences</c> (la sessione richiede campi da registrare per segmento — Workout log e'
    /// il precedente, un tap counter nudo non ha nulla da segmentare). Non e' un input utente —
    /// quando l'host lo spegne, forziamo <see cref="HasSession"/> a false cosi' il draft non porta mai
    /// segmenti orfani se la composizione smette di supportarli (stesso pattern del Goal brick).</summary>
    [ObservableProperty] private bool _isAvailable;

    public ObservableCollection<SegmentRowViewModel> Segments { get; } = new();

    public event EventHandler? Changed;

    public SessionBrickViewModel() => Segments.CollectionChanged += OnSegmentsCollectionChanged;

    partial void OnHasSessionChanged(bool value)
    {
        if (value) SeedDefaults();
        else ClearSegments();
        Raise();
    }

    partial void OnIsAvailableChanged(bool value)
    {
        if (!value) HasSession = false; // il setter sopra rilancia Changed se il valore cambia davvero
    }

    private void SeedDefaults()
    {
        Segments.Add(new SegmentRowViewModel { Name = Strings.HabitBuilder_Segment_Work, IsRest = false });
        Segments.Add(new SegmentRowViewModel { Name = Strings.HabitBuilder_Segment_Rest, IsRest = true });
    }

    // Never Clear() a collection that is bound to a live ItemsControl's ItemsSource (4d01f9c
    // precedent: clearing a bound ItemsSource can coerce a two-way-bound value on a sub-control to
    // null, and repopulating afterwards does NOT undo that write — it already happened at Clear()
    // time). Removing one row at a time turns the emptying into a sequence of isolated Remove events
    // instead of a single Reset, so it stays safe even if a future row template grows a two-way bound
    // sub-control (e.g. a per-segment color picker) — no need to also prove the panel's Visibility
    // binding has already collapsed the view before this runs.
    private void ClearSegments()
    {
        while (Segments.Count > 0) Segments.RemoveAt(Segments.Count - 1);
    }

    [RelayCommand]
    private void AddSegment() => Segments.Add(new SegmentRowViewModel());

    [RelayCommand]
    private void RemoveSegment(SegmentRowViewModel? row)
    {
        if (row is null) return;
        Segments.Remove(row);
    }

    private void OnSegmentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (SegmentRowViewModel row in e.OldItems)
                row.PropertyChanged -= OnRowChanged;
        if (e.NewItems is not null)
            foreach (SegmentRowViewModel row in e.NewItems)
                row.PropertyChanged += OnRowChanged;
        Raise();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e) => Raise();

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Off leaves the three fields at their HabitConfig defaults (false/null/false) — mirrors
    /// GoalBrick.Build returning null rather than writing a half-composed goal.</summary>
    public void ApplyTo(HabitConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!HasSession) return;
        config.HasIntegratedTimer = true;
        config.TimerMode = TimerMode.Session;
        config.SessionBaseTimer = true;
    }

    /// <summary>Rows with no name are dropped — an unnamed segment has nothing to record against.
    /// SortOrder is the row's position among the surviving (named) rows, not its raw list index.</summary>
    public IReadOnlyList<SegmentDefinition> BuildSegments(Guid nodeId) =>
        Segments
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .Select((s, i) => new SegmentDefinition
            {
                Id = Guid.NewGuid(),
                HabitNodeId = nodeId,
                Name = s.Name,
                IsRest = s.IsRest,
                SortOrder = i,
            })
            .ToList();
}
