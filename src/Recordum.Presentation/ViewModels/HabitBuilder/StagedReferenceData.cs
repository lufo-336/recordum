using CommunityToolkit.Mvvm.ComponentModel;

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
