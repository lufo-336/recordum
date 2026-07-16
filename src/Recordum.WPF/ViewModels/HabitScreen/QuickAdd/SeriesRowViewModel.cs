using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

public sealed partial class SeriesRowViewModel : ObservableObject
{
    public IReadOnlyList<SeriesCellViewModel> Cells { get; }

    public SeriesRowViewModel(IEnumerable<MetricDefinition> metrics)
        : this(metrics.Select(m => new SeriesCellViewModel(m))) { }

    public SeriesRowViewModel(IEnumerable<SeriesCellViewModel> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var list = cells.ToList();
        Cells = list;
        foreach (var c in list)
            c.PropertyChanged += (_, _) => OnPropertyChanged(nameof(IsValid));
    }

    /// <summary>Serie valida = almeno un campo compilato (spec D6). Il ✓/○ in UI e il conteggio
    /// "Registra N serie" derivano da qui.</summary>
    public bool IsValid => Cells.Any(c => c.BuildValue(0) is not null);

    /// <summary>Whether this row is shown in the repeatable counter quick-add (U8): only the row
    /// fronted by the selected chip is visible. Default true keeps Measured/Resistance rows (which
    /// never go through the chip UI) always visible.</summary>
    [ObservableProperty] private bool _isVisibleRow = true;
}
