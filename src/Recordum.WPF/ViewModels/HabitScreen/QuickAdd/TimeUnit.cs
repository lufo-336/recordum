namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>Input unit for duration cells (spec U8 §3): the value is typed in this unit and
/// normalized to whole seconds at save (storage stays value_seconds, no schema change).</summary>
public enum TimeUnit { Hours, Minutes, Seconds }

/// <summary>A selectable time unit with its localized label (ComboBox item).</summary>
public sealed record TimeUnitOption(TimeUnit Unit, string Label);
