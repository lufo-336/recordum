using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitEditor.Sections;

public sealed partial class StreakSectionViewModel(IHabitConfigService configs) : SectionViewModelBase
{
    public override string Title => Strings.Editor_Section_Streak;

    // GIO-21: la parte avanzata della serie era la sola tolleranza, che si salva ma nessun calcolo legge. Nascosta
    // finché non funziona (ROADMAP, passo 7); StreakAdvancedView resta per quel giorno.
    public override bool HasAdvanced => false;

    public override string Summary
    {
        get
        {
            var mode = StreakMode switch
            {
                StreakMode.Weekly => Strings.HabitBuilder_StreakMode_Weekly,
                StreakMode.Occurrences => Strings.HabitBuilder_StreakMode_Occurrences,
                StreakMode.Both => Strings.HabitBuilder_StreakMode_Both,
                _ => Strings.HabitBuilder_StreakMode_Daily,
            };
            var recovery = StreakRecovery switch
            {
                StreakRecovery.GraceDay => Strings.HabitBuilder_Recovery_GraceDay,
                StreakRecovery.Manual => Strings.HabitBuilder_Recovery_Manual,
                StreakRecovery.Retroactive => Strings.HabitBuilder_Recovery_Retroactive,
                _ => null,
            };
            return recovery is null ? mode : $"{mode} · {recovery}";
        }
    }

    private const int MaxToleranceMinutes = 1440;

    private readonly IHabitConfigService _configs = configs;
    private bool _loading;
    internal Guid NodeId { get; private set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private StreakMode _streakMode = StreakMode.Daily;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private StreakRecovery _streakRecovery = StreakRecovery.None;
    [ObservableProperty] private int? _streakToleranceMinutes;

    public Task LoadAsync(HabitNode node, HabitConfig? cfg)
    {
        _loading = true;
        try
        {
            NodeId = node.Id;
            StreakMode = cfg?.StreakMode ?? StreakMode.Daily;
            StreakRecovery = cfg?.StreakRecovery ?? StreakRecovery.None;
            StreakToleranceMinutes = cfg?.StreakToleranceMinutes;
        }
        finally { _loading = false; }
        return Task.CompletedTask;
    }

    // Abstinence uses a dedicated streak (clean time since last lapse); mode/recovery don't apply.
    public override bool IsApplicable(NodeType node, HabitType? habit)
        => node == NodeType.Habit && habit != HabitType.Resistance;

    partial void OnStreakModeChanged(StreakMode value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.StreakMode = value).AsTask());
    }

    partial void OnStreakRecoveryChanged(StreakRecovery value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.StreakRecovery = value).AsTask());
    }

    partial void OnStreakToleranceMinutesChanged(int? value)
    {
        if (_loading || NodeId == Guid.Empty) return;
        if (value is { } v && (v <= 0 || v > MaxToleranceMinutes))
        {
            ErrorMessage = Strings.Editor_Error_ToleranceRange;
            Status = EditorStatus.Error;
            return;
        }
        _ = ApplyAsync(() => _configs.UpsertAsync(NodeId, c => c.StreakToleranceMinutes = value).AsTask());
    }
}
