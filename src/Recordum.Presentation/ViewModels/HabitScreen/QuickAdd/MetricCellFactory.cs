using System.Text.Json;
using Recordum.Core.Models;

namespace Recordum.Presentation.ViewModels.HabitScreen.QuickAdd;

/// <summary>Builds enriched <see cref="SeriesCellViewModel"/>s from resolved units and scales.
/// Built once per load from reference data; reused for every row (Measured = 1 row, CounterSeries = N).</summary>
public sealed class MetricCellFactory
{
    private readonly Dictionary<Guid, UnitOfMeasure> _unitById;
    private readonly Dictionary<Guid, ScaleTemplate> _scaleById;

    public MetricCellFactory(IEnumerable<UnitOfMeasure> units, IEnumerable<ScaleTemplate> scales)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(scales);
        _unitById = units.ToDictionary(u => u.Id, u => u);
        _scaleById = scales.ToDictionary(s => s.Id, s => s);
    }

    public SeriesCellViewModel Create(MetricDefinition m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var unit = m.UnitOfMeasureId is { } uid && _unitById.TryGetValue(uid, out var u) ? u : null;

        ScaleInfo? scale = null;
        if (m.ScaleTemplateId is { } sid && _scaleById.TryGetValue(sid, out var tmpl))
        {
            scale = new ScaleInfo(tmpl);
        }
        return new SeriesCellViewModel(m, unit?.Code, scale, unit is null ? null : Recordum.Presentation.Templates.UnitLabel.For(unit));
    }

}
