using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Presentation.ViewModels.HabitEditor;

namespace Recordum.Presentation.ViewModels.HabitBuilder;

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

/// <summary>Micro-form for a custom scale staged from inside the builder (Onda 2.5 T7). The steps are a list of
/// fields (<see cref="LabelsEditor"/>) — a scale of one step is not a scale, hence the two-label minimum.
/// <see cref="LabelsText"/> is the same list as one comma-separated line, kept for the WPF builder.</summary>
public sealed partial class NewScaleFormViewModel : ObservableObject
{
    private string _labelsText = string.Empty;

    public NewScaleFormViewModel()
    {
        LabelsEditor.LoadDraft();
        LabelsEditor.LabelsChanged += (_, _) =>
        {
            _labelsText = string.Join(", ", Labels);
            OnPropertyChanged(nameof(LabelsText));
            OnPropertyChanged(nameof(Labels));
            OnPropertyChanged(nameof(CanConfirm));
        };
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private string _name = string.Empty;

    public ScaleLabelsEditorViewModel LabelsEditor { get; } = new();

    public string LabelsText
    {
        get => _labelsText;
        set
        {
            if (!SetProperty(ref _labelsText, value)) return;
            LabelsEditor.LoadDraft(value.Split(',').Select(l => l.Trim()).Where(l => l.Length > 0).ToList());
            OnPropertyChanged(nameof(Labels));
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    /// <summary>The steps in order. Blank rows are dropped so an unfilled field while typing does not become an unnamed step.</summary>
    public IReadOnlyList<string> Labels => LabelsEditor.Labels;

    public bool CanConfirm => !string.IsNullOrWhiteSpace(Name) && Labels.Count >= 2;
}
