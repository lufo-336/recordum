using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitEditor.Sections;

/// <summary>"Registrazione" section (U8): the repeatable knob — can this counter be recorded
/// multiple times a day (named series) or once (second save sums in the day total)? Mirrors the
/// same knob exposed at creation time in the builder (Livello 1, Counter-only); this section lets
/// it be changed later. Counter-only.</summary>
public sealed partial class RegistrationSectionViewModel(IHabitConfigService configs) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Registration;

    private readonly IHabitConfigService _configs = configs;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty] private bool _repeatable;

    public override bool IsApplicable(NodeType node, HabitType? habit)
        => node == NodeType.Habit && habit == HabitType.Counter;

    public async Task LoadAsync(HabitNode node)
    {
        _loading = true;
        try
        {
            NodeId = node.Id;
            var cfg = await _configs.GetAsync(node.Id).ConfigureAwait(true);
            Repeatable = cfg?.Repeatable ?? false;
        }
        finally { _loading = false; }
    }

    partial void OnRepeatableChanged(bool value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.Repeatable = value).AsTask());
    }
}
