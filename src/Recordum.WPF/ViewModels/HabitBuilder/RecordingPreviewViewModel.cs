using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Recordum.WPF.ViewModels.HabitBuilder;

/// <summary>Anteprima read-only: righe = serie (1 se serie disattivata), celle = campi
/// (escluso il Text, che è una casella sotto). Ricostruita ad ogni cambio dal builder.</summary>
public sealed partial class RecordingPreviewViewModel : ObservableObject
{
    public ObservableCollection<PreviewRow> Rows { get; } = new();
    [ObservableProperty] private bool _hasTextField;

    public void Rebuild(IReadOnlyList<FieldBlockViewModel> fields, bool seriesEnabled, int seriesCount)
    {
        Rows.Clear();
        var cellNames = fields.Where(f => f.Nature != Core.Enums.FieldNature.Text)
                              .Select(f => f.Name).ToList();
        HasTextField = fields.Any(f => f.Nature == Core.Enums.FieldNature.Text);
        int rows = seriesEnabled ? Math.Max(1, seriesCount) : 1;
        for (int i = 0; i < rows; i++)
            Rows.Add(new PreviewRow(seriesEnabled ? $"Serie {i + 1}" : string.Empty, cellNames));
    }
}

public sealed record PreviewRow(string Label, IReadOnlyList<string> CellNames);
