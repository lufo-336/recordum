using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.WPF.ViewModels.HabitBuilder;

/// <summary>Anteprima read-only: righe = serie (1 riga base se no contatore), celle = campi
/// (escluso il Text, che è una casella sotto). Ricostruita ad ogni cambio dal builder.</summary>
public sealed partial class RecordingPreviewViewModel : ObservableObject
{
    public ObservableCollection<PreviewRow> Rows { get; } = new();
    [ObservableProperty] private bool _hasTextField;
    [ObservableProperty] private bool _isTapCounter;
    [ObservableProperty] private bool _hasAddSeries;

    public void Rebuild(IReadOnlyList<FieldBlockViewModel> fields, bool countsOccurrences, bool hasAddSeries)
    {
        Rows.Clear();
        IsTapCounter = countsOccurrences;
        HasAddSeries = hasAddSeries;
        HasTextField = fields.Any(f => f.Nature == Core.Enums.FieldNature.Text);
        if (countsOccurrences) return; // anteprima = mock bottone +1, niente righe
        var cellNames = fields.Where(f => f.Nature != Core.Enums.FieldNature.Text)
                              .Select(f => f.Name).ToList();
        Rows.Add(new PreviewRow(string.Empty, cellNames));
    }
}

public sealed record PreviewRow(string Label, IReadOnlyList<string> CellNames);
