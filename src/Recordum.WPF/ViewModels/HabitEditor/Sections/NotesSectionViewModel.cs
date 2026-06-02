using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

/// <summary>
/// Config-backed editor section for the per-habit note appearance.
/// Currently exposes the calendar note-marker color (the colored corner on a day cell).
/// Persists via <see cref="IHabitConfigService.UpsertAsync"/> like the other config sections.
/// </summary>
public sealed partial class NotesSectionViewModel(IHabitConfigService configs) : SectionViewModelBase
{
    /// <summary>Default amber marker color used when the habit has no override.</summary>
    public const string DefaultNoteMarkerColor = "#e0b341";

    public override string Title => Strings.Editor_Section_Notes;

    private readonly IHabitConfigService _configs = configs;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private string _noteMarkerColor = DefaultNoteMarkerColor;

    public Task LoadAsync(HabitNode node, HabitConfig? cfg)
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
        return Task.CompletedTask;
    }

    public override bool IsApplicable(NodeType node, HabitType? habit) => node == NodeType.Habit;

    partial void OnNoteMarkerColorChanged(string value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.NoteMarkerColor = value).AsTask());
    }
}
