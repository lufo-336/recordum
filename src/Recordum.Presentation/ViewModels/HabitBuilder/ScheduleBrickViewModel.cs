using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.Presentation.Formatting;
using Recordum.Presentation.Resources;

namespace Recordum.Presentation.ViewModels.HabitBuilder;

/// <summary>One day-of-week toggle (Mon..Sun) offered by <see cref="ScheduleBrickViewModel"/> when
/// <see cref="ScheduleBrickViewModel.ScheduleType"/> is SpecificDays.</summary>
public sealed partial class ScheduleDayToggle : ObservableObject
{
    public required string DayKey { get; init; }          // "Mon".."Sun" — contratto ScheduleDaysJson
    public required string Label { get; init; }           // localizzata (Strings.Day_Mon…)
    [ObservableProperty] private bool _isSelected;
}

/// <summary>Mattone Programma (Onda 2.5 §2.1): schedule + streak + recovery del draft. Vista sul
/// draft, non chiama servizi (spec §1: host-agnostico). Collassato mostra Summary.</summary>
public sealed partial class ScheduleBrickViewModel : ObservableObject
{
    [ObservableProperty] private ScheduleType _scheduleType = ScheduleType.Daily;
    [ObservableProperty] private int? _weeklyTarget;
    [ObservableProperty] private int? _everyXDays;
    [ObservableProperty] private StreakMode _streakMode = StreakMode.Daily;
    [ObservableProperty] private StreakRecovery _streakRecovery = StreakRecovery.GraceDay;
    [ObservableProperty] private bool _isExpanded;

    public ObservableCollection<ScheduleDayToggle> Days { get; }
    public event EventHandler? Changed;
    public string Summary => BuildSummary();

    public ScheduleBrickViewModel()
    {
        Days = new(new[]
            {
                ("Mon", Strings.Day_Mon), ("Tue", Strings.Day_Tue), ("Wed", Strings.Day_Wed),
                ("Thu", Strings.Day_Thu), ("Fri", Strings.Day_Fri), ("Sat", Strings.Day_Sat), ("Sun", Strings.Day_Sun),
            }
            .Select(d => new ScheduleDayToggle { DayKey = d.Item1, Label = d.Item2 }));
        foreach (var d in Days) d.PropertyChanged += (_, _) => Raise();
    }

    partial void OnScheduleTypeChanged(ScheduleType value) => Raise();
    partial void OnWeeklyTargetChanged(int? value) => Raise();
    partial void OnEveryXDaysChanged(int? value) => Raise();
    partial void OnStreakModeChanged(StreakMode value) => Raise();
    partial void OnStreakRecoveryChanged(StreakRecovery value) => Raise();

    private void Raise()
    {
        OnPropertyChanged(nameof(Summary));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>False when the chosen schedule is missing the number (or the days) it needs to mean
    /// anything (review 2.5, F2/F3). Three different failures hide behind this one flag: WeeklyX and
    /// EveryXDays without a positive value are rejected later by
    /// <c>HabitConfigService.ValidateCrossFields</c> — but only later, because
    /// <c>ComposedHabitService</c> writes the config straight to the DbSet, so the builder would
    /// happily create a habit that every subsequent edit (and any template instantiation) throws on;
    /// SpecificDays with no day ticked fails more quietly still, since
    /// <c>ScheduleEvaluator.MatchesDayMask</c> returns false for every date and the habit simply never
    /// comes due. The builder is the only writer that had no guard.</summary>
    public bool IsComplete => ScheduleType switch
    {
        ScheduleType.WeeklyX => WeeklyTarget is > 0,
        ScheduleType.EveryXDays => EveryXDays is > 0,
        ScheduleType.SpecificDays => Days.Any(d => d.IsSelected),
        _ => true,
    };

    public void ApplyTo(HabitConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.ScheduleType = ScheduleType;
        var selected = Days.Where(d => d.IsSelected).Select(d => d.DayKey).ToArray();
        config.ScheduleDaysJson = ScheduleType == ScheduleType.SpecificDays && selected.Length > 0
            ? JsonSerializer.Serialize(selected)
            : null;
        config.ScheduleWeeklyTarget = ScheduleType == ScheduleType.WeeklyX ? WeeklyTarget : null;
        config.ScheduleEveryXDays = ScheduleType == ScheduleType.EveryXDays ? EveryXDays : null;
        config.StreakMode = StreakMode;
        config.StreakRecovery = StreakRecovery;
    }

    private string BuildSummary()
    {
        var sb = new StringBuilder();
        sb.Append(ScheduleType switch
        {
            ScheduleType.SpecificDays =>
                $"{Strings.HabitBuilder_Schedule_SpecificDays} ({Days.Count(d => d.IsSelected)})",
            ScheduleType.WeeklyX =>
                $"{Strings.HabitBuilder_Schedule_WeeklyX} ({WeeklyTarget?.ToString(CultureInfo.InvariantCulture) ?? "-"})",
            ScheduleType.EveryXDays =>
                $"{Strings.HabitBuilder_Schedule_EveryXDays} ({EveryXDays?.ToString(CultureInfo.InvariantCulture) ?? "-"})",
            _ => Strings.HabitBuilder_Schedule_Daily,
        });

        // Recovery/streak mode default (GraceDay + Daily, the seed shape) stays quiet — only a
        // change from the default is worth surfacing in the collapsed summary. Labels come from
        // StreakLabels (F0-11): never surface a raw enum name, even in a computed summary string —
        // and never a second, hand-duplicated switch that could drift from the ComboBoxes' converters.
        if (StreakRecovery != StreakRecovery.GraceDay)
            sb.Append(" · ").Append(Strings.HabitBuilder_Schedule_Recovery).Append(": ").Append(StreakLabels.For(StreakRecovery));
        if (StreakMode != StreakMode.Daily)
            sb.Append(" · ").Append(Strings.HabitBuilder_Schedule_StreakMode).Append(": ").Append(StreakLabels.For(StreakMode));

        return sb.ToString();
    }
}
