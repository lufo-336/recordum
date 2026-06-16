using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class CompletionListViewModel : ObservableObject
{
    private readonly ICompletionService _completions;
    private readonly IMetricDefinitionService _metrics;
    private readonly IReferenceDataQueryService _refData;

    public Guid? NodeId { get; private set; }
    public ObservableCollection<CompletionRowViewModel> Rows { get; } = [];
    public int Take { get; init; } = 20;

    public CompletionListViewModel(ICompletionService completions, IMetricDefinitionService metrics, IReferenceDataQueryService refData)
    {
        _completions = completions;
        _metrics = metrics;
        _refData = refData;
    }

    public async Task LoadAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        NodeId = habitNodeId;
        var list = await _completions.ListByHabitAsync(habitNodeId, take: Take, ct: ct).ConfigureAwait(true);
        var lookup = await BuildLookupAsync(habitNodeId, ct).ConfigureAwait(true);
        Populate(list, lookup);
    }

    /// <summary>Loads the completions in [from, to] (inclusive; null bound = unbounded that side), each
    /// with readable per-field values. Powers both the day panel (from==to) and the period list.</summary>
    public async Task LoadRangeAsync(Guid habitNodeId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        NodeId = habitNodeId;
        var fromTs = from is { } f ? new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : (DateTimeOffset?)null;
        var toTs = to is { } t ? new DateTimeOffset(t.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero) : (DateTimeOffset?)null;
        var list = await _completions.ListByHabitAsync(habitNodeId, fromTs, toTs, ct: ct).ConfigureAwait(true);
        var lookup = await BuildLookupAsync(habitNodeId, ct).ConfigureAwait(true);
        Populate(list, lookup);
    }

    /// <summary>Loads the completions recorded on a single day. Thin wrapper over <see cref="LoadRangeAsync"/>.</summary>
    public Task LoadDayAsync(Guid habitNodeId, DateOnly date, CancellationToken ct = default)
        => LoadRangeAsync(habitNodeId, date, date, ct);

    private void Populate(IReadOnlyList<Completion> list, Dictionary<Guid, MetricDisplay> lookup)
    {
        Rows.Clear();
        foreach (var c in list.OrderByDescending(c => c.CompletedAtMs))
            Rows.Add(new CompletionRowViewModel(c, _completions, c.Values,
                onDeleted: row => Rows.Remove(row),
                fieldDisplays: BuildFieldDisplays(c.Values, lookup)));
    }

    private async Task<Dictionary<Guid, MetricDisplay>> BuildLookupAsync(Guid habitNodeId, CancellationToken ct)
    {
        var metrics = await _metrics.ListByHabitAsync(habitNodeId, ct).ConfigureAwait(true);
        var units = (await _refData.ListUnitsAsync(ct).ConfigureAwait(true)).ToDictionary(u => u.Id, u => u.Code);
        var scales = (await _refData.ListScalesAsync(ct).ConfigureAwait(true)).ToDictionary(s => s.Id, s => s);
        var map = new Dictionary<Guid, MetricDisplay>();
        foreach (var m in metrics)
        {
            string? unit = m.UnitOfMeasureId is { } uid && units.TryGetValue(uid, out var c) ? c : null;
            IReadOnlyList<string>? labels = null;
            if (m.ScaleTemplateId is { } sid && scales.TryGetValue(sid, out var tmpl))
                labels = ParseLabels(m.LabelsJson) ?? ParseLabels(tmpl.LabelsJson);
            map[m.Id] = new MetricDisplay(m.Name, unit, labels, m.SortOrder);
        }
        return map;
    }

    private static List<string> BuildFieldDisplays(IReadOnlyList<CompletionValue> values, Dictionary<Guid, MetricDisplay> lookup)
    {
        // Group by series so a multi-series resistance reads "Series 1 — Count: 12 · Time: 30"
        // per row instead of a flat, ambiguous list of every series' values jumbled together.
        var groups = values.GroupBy(v => v.SeriesIndex).OrderBy(g => g.Key).ToList();
        bool multiSeries = groups.Count > 1;
        var result = new List<string>();
        foreach (var g in groups)
        {
            var fields = new List<string>();
            // Within a series, order fields by the metric's SortOrder for a stable, sensible layout.
            foreach (var v in g.OrderBy(v => lookup.TryGetValue(v.MetricDefinitionId, out var md) ? md.SortOrder : int.MaxValue))
            {
                if (!lookup.TryGetValue(v.MetricDefinitionId, out var m)) continue;
                string? val = FormatValue(v, m);
                if (val is null) continue;
                fields.Add($"{m.Name}: {val}");
            }
            if (fields.Count == 0) continue;
            if (multiSeries)
            {
#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI string
                var label = string.Format(CultureInfo.CurrentCulture, Strings.HabitBuilder_SeriesRow, g.Key + 1);
#pragma warning restore CA1863
                result.Add($"{label} — {string.Join(" · ", fields)}");
            }
            else
            {
                result.AddRange(fields);
            }
        }
        return result;
    }

    private static string? FormatValue(CompletionValue v, MetricDisplay m)
    {
        // Scale → label at the 0-based stored index (falls back to the bare number).
        if (m.ScaleLabels is { Count: > 0 } labels && v.ValueInteger is { } idx)
            return idx >= 0 && idx < labels.Count ? labels[(int)idx] : idx.ToString(CultureInfo.CurrentCulture);
        if (v.ValueText is { } t) return t;
        var unit = m.Unit is { } u ? " " + u : string.Empty;
        if (v.ValueDecimal is { } d) return d.ToString(CultureInfo.CurrentCulture) + unit;
        if (v.ValueInteger is { } i) return i.ToString(CultureInfo.CurrentCulture) + unit;
        return null;
    }

    private static string[]? ParseLabels(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<string[]>(json); }
        catch (JsonException) { return null; }
    }

    private sealed record MetricDisplay(string Name, string? Unit, IReadOnlyList<string>? ScaleLabels, int SortOrder);
}
