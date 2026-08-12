using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.WPF.ViewModels.HabitBuilder;

/// <summary>Micro-form for a custom unit staged from inside the builder (Onda 2.5 T7). Nothing is
/// written to the database here: the confirmed unit lives in the draft and is materialised by
/// <c>ComposedHabitService</c> on Launch, or embedded in the payload when the composition is saved as
/// a template.</summary>
public sealed partial class NewUnitFormViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private string _name = string.Empty;

    [ObservableProperty] private string? _symbol;

    public bool CanConfirm => !string.IsNullOrWhiteSpace(Name);
}

/// <summary>Micro-form for a custom scale staged from inside the builder (Onda 2.5 T7). The steps are
/// typed as one comma-separated line — a scale of one step is not a scale, hence the two-label
/// minimum.</summary>
public sealed partial class NewScaleFormViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Labels))]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private string _labelsText = string.Empty;

    /// <summary>The typed line split into steps, in order. Blank entries are dropped so a trailing
    /// comma while typing does not become an unnamed step.</summary>
    public IReadOnlyList<string> Labels =>
        LabelsText.Split(',').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    public bool CanConfirm => !string.IsNullOrWhiteSpace(Name) && Labels.Count >= 2;
}
