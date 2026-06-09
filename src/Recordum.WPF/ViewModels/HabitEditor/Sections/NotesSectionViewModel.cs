using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;
using Recordum.WPF.Services;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

/// <summary>
/// Config-backed editor section for the per-habit note appearance.
/// Currently exposes the calendar note-marker color (the colored corner on a day cell).
/// Persists via <see cref="IHabitConfigService.UpsertAsync"/> like the other config sections.
/// </summary>
public sealed partial class NotesSectionViewModel(IHabitConfigService configs, IRecentColorStore recentColors) : SectionViewModelBase
{
    /// <summary>Default amber marker color used when the habit has no override.</summary>
    public const string DefaultNoteMarkerColor = "#e0b341";

    public override string Title => Strings.Editor_Section_Notes;

    private readonly IHabitConfigService _configs = configs;
    private readonly IRecentColorStore _recentColors = recentColors;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private string _noteMarkerColor = DefaultNoteMarkerColor;

    /// <summary>Recently picked colors, shared with the habit color pickers (bound to ColorSwatchPicker).</summary>
    public ObservableCollection<string> RecentColors { get; } = new();

    public async Task LoadAsync(HabitNode node, HabitConfig? cfg)
    {
        ArgumentNullException.ThrowIfNull(node);
        _loading = true;
        try
        {
            NodeId = node.Id;
            NoteMarkerColor = string.IsNullOrWhiteSpace(cfg?.NoteMarkerColor)
                ? DefaultNoteMarkerColor
                : cfg!.NoteMarkerColor!;
        }
        finally { _loading = false; }

        RecentColors.Clear();
        foreach (var c in await _recentColors.GetAsync().ConfigureAwait(true)) RecentColors.Add(c);
    }

    public override bool IsApplicable(NodeType node, HabitType? habit) => node == NodeType.Habit;

    partial void OnNoteMarkerColorChanged(string value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.NoteMarkerColor = value).AsTask());
        _ = _recentColors.AddAsync(value);
    }
}
