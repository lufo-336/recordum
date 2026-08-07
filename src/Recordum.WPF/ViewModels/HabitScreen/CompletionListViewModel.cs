using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.WPF.Formatting;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitScreen;

public sealed partial class CompletionListViewModel : ObservableObject
{
    private readonly ICompletionService _completions;
    private readonly IMetricDefinitionService _metrics;
    private readonly IReferenceDataQueryService _refData;
    private readonly IDiaryService _diary;

    public Guid? NodeId { get; private set; }
    public ObservableCollection<CompletionRowViewModel> Rows { get; } = [];
    public int Take { get; init; } = 20;

    /// <summary>When true (journal habit), rows show the paired diary note body and delete the pair
    /// via IDiaryService instead of the bare completion.</summary>
    public bool IsJournal { get; set; }

    /// <summary>The habit's persisted series names (`habit_config.series_labels_json`, U8). Set by the
    /// host before loading so a multi-series row reads back the names it was registered under
    /// ("Night"/"Nap") instead of the positional fallback (W23-VF3). Null/blank entries fall back.</summary>
    public string? SeriesLabelsJson { get; set; }

    public CompletionListViewModel(ICompletionService completions, IMetricDefinitionService metrics,
        IReferenceDataQueryService refData, IDiaryService diary)
    {
        _completions = completions;
        _metrics = metrics;
        _refData = refData;
        _diary = diary;
    }

    public async Task LoadAsync(Guid habitNodeId, CancellationToken ct = default)
    {
        NodeId = habitNodeId;
        var list = await _completions.ListByHabitAsync(habitNodeId, take: Take, ct: ct).ConfigureAwait(true);
        var lookup = await BuildLookupAsync(habitNodeId, ct).ConfigureAwait(true);
        await PopulateAsync(list, lookup, ct).ConfigureAwait(true);
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
        await PopulateAsync(list, lookup, ct).ConfigureAwait(true);
    }

    /// <summary>Loads the completions recorded on a single day. Thin wrapper over <see cref="LoadRangeAsync"/>.</summary>
    public Task LoadDayAsync(Guid habitNodeId, DateOnly date, CancellationToken ct = default)
        => LoadRangeAsync(habitNodeId, date, date, ct);

    private async Task PopulateAsync(IReadOnlyList<Completion> list, Dictionary<Guid, MetricDisplay> lookup, CancellationToken ct)
    {
        Rows.Clear();
        IReadOnlyDictionary<Guid, string> bodies = IsJournal
            ? await _diary.GetEntryBodiesByCompletionsAsync(list.Select(c => c.Id).ToList(), ct).ConfigureAwait(true)
            : new Dictionary<Guid, string>();
        foreach (var c in list.OrderByDescending(c => c.CompletedAtMs))
        {
            string? overrideNotes = IsJournal && bodies.TryGetValue(c.Id, out var body) ? body : null;
            Func<Guid, CancellationToken, Task>? del = IsJournal
                ? (id, token) => _diary.DeleteEntryAsync(id, token).AsTask()
                : null;
            Rows.Add(new CompletionRowViewModel(c, _completions, c.Values,
                onDeleted: row => Rows.Remove(row),
                fieldDisplays: BuildFieldDisplays(c.Values, lookup, ParseLabels(SeriesLabelsJson)),
                overrideNotes: overrideNotes,
                deleteOverride: del,
                canInlineEdit: !IsJournal));
        }
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

    private static List<string> BuildFieldDisplays(IReadOnlyList<CompletionValue> values,
        Dictionary<Guid, MetricDisplay> lookup, string[]? seriesLabels)
    {
        // Group by series so a multi-series registration reads "Series 1 — Count: 12 · Time: 30"
        // per row instead of a flat, ambiguous list of every series' values jumbled together.
        // One line per series either way: a lone series only drops the prefix, because there is
        // nothing to disambiguate — it used to break its fields onto a line each, so the same
        // object was laid out two different ways depending on how many series it had (W23-VF6).
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
            var line = string.Join(" · ", fields);
            result.Add(multiSeries ? $"{SeriesLabel(g.Key, seriesLabels)} — {line}" : line);
        }
        return result;
    }

    /// <summary>The series' configured name, or the positional "Series N" when it was never renamed.
    /// Mirrors CounterSeriesQuickAddViewModel.LabelFor so a row reads back what the chips wrote.</summary>
    private static string SeriesLabel(int index, string[]? labels)
    {
        if (labels is not null && index < labels.Length && !string.IsNullOrWhiteSpace(labels[index]))
            return labels[index];
#pragma warning disable CA1863 // CompositeFormat caching not worth complexity for low-frequency UI string
        return string.Format(CultureInfo.CurrentCulture, Strings.HabitBuilder_SeriesRow, index + 1);
#pragma warning restore CA1863
    }

    private static string? FormatValue(CompletionValue v, MetricDisplay m)
    {
        // Scale → label at the 1-based stored value (falls back to the bare number).
        if (m.ScaleLabels is { Count: > 0 } labels && v.ValueInteger is { } val)
            return val >= 1 && val <= labels.Count ? labels[(int)val - 1] : val.ToString(CultureInfo.CurrentCulture);
        if (v.ValueText is { } t) return t;
        // Duration metrics store ValueSeconds only; without this branch a series with just a time
        // (e.g. 1:30) rendered as a blank field in Cronologia/Records despite being saved (CR13).
        // U8: display in the metric's own unit ("7,5 ore") when the metric has a time unit chosen on
        // it — localized to match the quick-add selector; falls back to the bare h:mm rendering when
        // the metric has no unit / a non-time unit.
        if (v.ValueSeconds is { } secs)
            return Recordum.WPF.Formatting.DurationUnitDisplay.InUnit(secs, m.Unit) ?? DurationFormat.Format(secs);
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
