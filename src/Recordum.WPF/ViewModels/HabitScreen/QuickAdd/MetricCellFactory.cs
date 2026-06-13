using System.Text.Json;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>Builds enriched <see cref="SeriesCellViewModel"/>s from resolved units and scales.
/// Built once per load from reference data; reused for every row (Measured = 1 row, CounterSeries = N).</summary>
public sealed class MetricCellFactory
{
    private readonly Dictionary<Guid, string> _unitCodeById;
    private readonly Dictionary<Guid, ScaleTemplate> _scaleById;

    public MetricCellFactory(IEnumerable<UnitOfMeasure> units, IEnumerable<ScaleTemplate> scales)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(scales);
        _unitCodeById = units.ToDictionary(u => u.Id, u => u.Code);
        _scaleById = scales.ToDictionary(s => s.Id, s => s);
    }

    public SeriesCellViewModel Create(MetricDefinition m)
    {
        ArgumentNullException.ThrowIfNull(m);
        string? unitCode = m.UnitOfMeasureId is { } uid && _unitCodeById.TryGetValue(uid, out var c) ? c : null;

        ScaleInfo? scale = null;
        if (m.ScaleTemplateId is { } sid && _scaleById.TryGetValue(sid, out var tmpl))
        {
            // metric.LabelsJson (local override) wins over the template's labels (CSS-style specificity, spec §5)
            var labels = ParseLabels(m.LabelsJson) ?? ParseLabels(tmpl.LabelsJson) ?? [];
            scale = new ScaleInfo(tmpl.MinValue, tmpl.MaxValue, labels);
        }
        return new SeriesCellViewModel(m, unitCode, scale);
    }

    private static string[]? ParseLabels(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<string[]>(json); }
        catch (JsonException) { return null; }
    }
}
