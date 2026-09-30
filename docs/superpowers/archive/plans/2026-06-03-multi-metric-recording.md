# Multi-metric recording (B11) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generalize the recording layer so a habit composed by the builder shows and records ALL its configured metrics (measure + scale + multiple measures) in one gesture, with units and range-aware scale controls.

**Architecture:** A recording is rows × kind-aware cells. `SeriesCellViewModel` becomes kind-aware (measured+unit / scale buttons-or-slider / text). `CounterQuickAddViewModel` + `StateQuickAddViewModel` are replaced by one `MeasuredQuickAddViewModel` (1 row of cells); `ResistanceQuickAddViewModel` reuses the same enriched cells (N rows). Scope = recording only; Detail/charts stay primary-metric. Scale value encoding preserved (0-based index).

**Tech Stack:** C# / .NET 10, WPF, CommunityToolkit.Mvvm, xUnit + FluentAssertions (`[WpfFact]`).

**Spec:** `docs/superpowers/archive/specs/2026-06-03-multi-metric-recording-design.md`

---

## File Structure

- `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/MetricInputKind.cs` — **create**: enum + `Resolve`.
- `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesCellViewModel.cs` — **modify**: kind-aware (unit, scale, slider).
- `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesRowViewModel.cs` — **modify**: ctor overload taking pre-built cells.
- `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/MetricCellFactory.cs` — **create**: builds enriched cells from resolved units/scales.
- `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/MeasuredQuickAddViewModel.cs` — **create**: 1-row multi-metric recorder (replaces Counter + State).
- `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/CounterQuickAddViewModel.cs` — **delete**.
- `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/StateQuickAddViewModel.cs` — **delete**.
- `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/ResistanceQuickAddViewModel.cs` — **modify**: inject `IReferenceDataQueryService`, build cells via factory (units shown).
- `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs` — **modify**: Counter+State → Measured; switch case.
- `src/Recordum.WPF/Selectors/QuickAddTemplateSelector.cs` — **modify**: `Measured` replaces `Counter`/`State`.
- `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml` — **modify**: kind-aware cell template, Measured view, Resistance cell reuse, retire Counter/State templates, selector wiring.
- Tests under `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/`.

---

## Task 1: Kind-aware cell primitive

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/MetricInputKind.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesCellViewModel.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/SeriesRowViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/SeriesCellViewModelTests.cs` (create)

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.QuickAdd;

public class SeriesCellViewModelTests
{
    private static MetricDefinition Measured(string name = "Reps") =>
        new() { Id = Guid.NewGuid(), Name = name, Type = MetricType.Integer };

    private static MetricDefinition Scale(string name = "Mood") =>
        new() { Id = Guid.NewGuid(), Name = name, Type = MetricType.Integer, ScaleTemplateId = Guid.NewGuid() };

    [Fact]
    public void Kind_is_scale_when_metric_has_scale_template()
    {
        SeriesCellViewModel.ResolveKind(Scale()).Should().Be(MetricInputKind.Scale);
        SeriesCellViewModel.ResolveKind(Measured()).Should().Be(MetricInputKind.Measured);
        SeriesCellViewModel.ResolveKind(new MetricDefinition { Type = MetricType.Text }).Should().Be(MetricInputKind.Text);
    }

    [Fact]
    public void Measured_cell_carries_unit_label()
    {
        var cell = new SeriesCellViewModel(Measured(), unitCode: "ml");
        cell.Kind.Should().Be(MetricInputKind.Measured);
        cell.UnitLabel.Should().Be("ml");
        cell.IntegerValue = 30;
        cell.BuildValue(0)!.ValueInteger.Should().Be(30);
    }

    [Fact]
    public void Scale_cell_builds_zero_based_index_value()
    {
        var info = new ScaleInfo(1, 5, new[] { "Bad", "Meh", "Ok", "Good", "Great" });
        var cell = new SeriesCellViewModel(Scale(), scale: info);

        cell.Kind.Should().Be(MetricInputKind.Scale);
        cell.StepCount.Should().Be(5);
        cell.UseSlider.Should().BeFalse();
        cell.BuildValue(0).Should().BeNull(); // nothing selected yet

        cell.SelectedScaleIndex = 3;
        cell.BuildValue(0)!.ValueInteger.Should().Be(3); // 0-based, preserves State encoding
    }

    [Fact]
    public void Scale_with_more_than_ten_steps_uses_slider()
    {
        var info = new ScaleInfo(1, 100, Array.Empty<string>());
        var cell = new SeriesCellViewModel(Scale(), scale: info);

        cell.StepCount.Should().Be(100);
        cell.UseSlider.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~SeriesCellViewModelTests"`
Expected: FAIL — `MetricInputKind`, `ScaleInfo`, new ctor params don't exist (compile error).

- [ ] **Step 3: Create `MetricInputKind.cs`**

Just the enum (the resolver lives on the cell as a `public static`, because C# can't attach a static method to an enum). The Step 1 test already calls `SeriesCellViewModel.ResolveKind(...)`.

```csharp
namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>Recording-input kind of a metric: a scale (labelled/slider), a plain measure, or free text.</summary>
public enum MetricInputKind
{
    Measured,
    Scale,
    Text,
}
```

- [ ] **Step 4: Modify `SeriesCellViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Enums;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>Scale range + labels resolved for a scale cell. Labels may be empty (slider with bare numbers).</summary>
public sealed record ScaleInfo(int Min, int Max, IReadOnlyList<string> Labels);

public sealed partial class SeriesCellViewModel : ObservableObject
{
    public MetricDefinition Metric { get; }
    public MetricType Type => Metric.Type;
    public string Name => Metric.Name;

    public MetricInputKind Kind { get; }
    public string? UnitLabel { get; }

    // Scale-only
    public IReadOnlyList<string> ScaleLabels { get; } = [];
    public int ScaleMin { get; }
    public int StepCount { get; }
    public bool UseSlider { get; }

    [ObservableProperty] private long? _integerValue;
    [ObservableProperty] private decimal? _decimalValue;
    [ObservableProperty] private string? _textValue;
    [ObservableProperty] private int? _selectedScaleIndex; // 0-based across steps

    public static MetricInputKind ResolveKind(MetricDefinition metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        if (metric.ScaleTemplateId is not null) return MetricInputKind.Scale;
        return metric.Type == MetricType.Text ? MetricInputKind.Text : MetricInputKind.Measured;
    }

    public SeriesCellViewModel(MetricDefinition metric, string? unitCode = null, ScaleInfo? scale = null)
    {
        ArgumentNullException.ThrowIfNull(metric);
        Metric = metric;
        Kind = ResolveKind(metric);
        UnitLabel = unitCode;
        if (Kind == MetricInputKind.Scale && scale is not null)
        {
            ScaleLabels = scale.Labels;
            ScaleMin = scale.Min;
            StepCount = scale.Labels.Count > 0 ? scale.Labels.Count : Math.Max(1, scale.Max - scale.Min + 1);
            UseSlider = StepCount > 10;
        }
    }

    public CompletionValue? BuildValue(int seriesIndex)
    {
        var v = new CompletionValue { MetricDefinitionId = Metric.Id, SeriesIndex = seriesIndex };
        switch (Kind)
        {
            case MetricInputKind.Scale:
                if (SelectedScaleIndex is null) return null;
                v.ValueInteger = SelectedScaleIndex; // 0-based, preserves StateQuickAdd encoding
                return v;
            case MetricInputKind.Text:
                if (string.IsNullOrWhiteSpace(TextValue)) return null;
                v.ValueText = TextValue;
                return v;
            default: // Measured
                switch (Metric.Type)
                {
                    case MetricType.Decimal:
                        if (DecimalValue is null) return null;
                        v.ValueDecimal = DecimalValue;
                        return v;
                    case MetricType.Integer:
                    case MetricType.Seconds:
                        if (IntegerValue is null) return null;
                        v.ValueInteger = IntegerValue;
                        return v;
                    default:
                        return null;
                }
        }
    }
}
```

- [ ] **Step 5: Add cell-list ctor overload to `SeriesRowViewModel.cs`**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

public sealed partial class SeriesRowViewModel : ObservableObject
{
    public ObservableCollection<SeriesCellViewModel> Cells { get; } = [];

    public SeriesRowViewModel(IEnumerable<MetricDefinition> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        foreach (var m in metrics) Cells.Add(new SeriesCellViewModel(m));
    }

    public SeriesRowViewModel(IEnumerable<SeriesCellViewModel> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        foreach (var c in cells) Cells.Add(c);
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~SeriesCellViewModelTests"`
Expected: PASS (4 tests).

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/ tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/SeriesCellViewModelTests.cs
git commit -m "feat(wpf): kind-aware SeriesCellViewModel (unit/scale/slider) for multi-metric recording"
```

---

## Task 2: `MetricCellFactory` + `MeasuredQuickAddViewModel`

**Files:**
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/MetricCellFactory.cs`
- Create: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/MeasuredQuickAddViewModel.cs`
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/MeasuredQuickAddViewModelTests.cs` (create)

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using Recordum.Core.Enums;
using Recordum.Core.Models;
using Recordum.WPF.Tests.Fixtures;
using Recordum.WPF.ViewModels.HabitScreen.QuickAdd;
using Xunit;

namespace Recordum.WPF.Tests.ViewModels.HabitScreen.QuickAdd;

public class MeasuredQuickAddViewModelTests
{
    private static (MeasuredQuickAddViewModel Sut, FakeCompletionService Cs, FakeMetricDefinitionService Ms, FakeReferenceData Rd)
        Build(Guid nodeId)
    {
        var cs = new FakeCompletionService();
        var ms = new FakeMetricDefinitionService();
        var rd = new FakeReferenceData();
        var sut = new MeasuredQuickAddViewModel(cs, ms, rd, new FakeClock())
        {
            Node = new HabitNode { Id = nodeId, HabitType = HabitType.Counter },
        };
        return (sut, cs, ms, rd);
    }

    [WpfFact]
    public async Task Load_builds_a_cell_per_metric()
    {
        var node = Guid.NewGuid();
        var (sut, _, ms, _) = Build(node);
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Reps", Type = MetricType.Integer, SortOrder = 0 });
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Weight", Type = MetricType.Decimal, SortOrder = 1 });

        await sut.LoadMetricsAsync();

        sut.Row.Cells.Should().HaveCount(2);
        sut.Row.Cells[0].Name.Should().Be("Reps");
        sut.Row.Cells[1].Name.Should().Be("Weight");
    }

    [WpfFact]
    public async Task Save_records_a_value_per_filled_cell()
    {
        var node = Guid.NewGuid();
        var (sut, cs, ms, _) = Build(node);
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Reps", Type = MetricType.Integer, SortOrder = 0 });
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Weight", Type = MetricType.Decimal, SortOrder = 1 });
        await sut.LoadMetricsAsync();

        sut.Row.Cells[0].IntegerValue = 12;
        sut.Row.Cells[1].DecimalValue = 40.5m;
        await sut.SaveAsync();

        cs.LastValues.Should().HaveCount(2);
        cs.LastValues!.Single(v => v.ValueInteger == 12).Should().NotBeNull();
        cs.LastValues!.Single(v => v.ValueDecimal == 40.5m).Should().NotBeNull();
    }

    [WpfFact]
    public async Task Quantity_cell_resolves_unit_label()
    {
        var node = Guid.NewGuid();
        var (sut, _, ms, rd) = Build(node);
        var unitId = rd.Units[0].Id; // "l"/"Litre" seeded
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Water", Type = MetricType.Decimal, UnitOfMeasureId = unitId, SortOrder = 0 });

        await sut.LoadMetricsAsync();

        sut.Row.Cells[0].UnitLabel.Should().Be(rd.Units[0].Code);
    }

    [WpfFact]
    public async Task Scale_cell_records_selected_index_and_loads_labels()
    {
        var node = Guid.NewGuid();
        var (sut, cs, ms, rd) = Build(node);
        rd.Scales.Clear();
        var scaleId = Guid.NewGuid();
        rd.Scales.Add(new ScaleTemplate { Id = scaleId, Code = "mood-5", Name = "Mood", MinValue = 1, MaxValue = 5, LabelsJson = "[\"Bad\",\"Meh\",\"Ok\",\"Good\",\"Great\"]" });
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Mood", Type = MetricType.Integer, ScaleTemplateId = scaleId, SortOrder = 0 });
        await sut.LoadMetricsAsync();

        sut.Row.Cells[0].Kind.Should().Be(MetricInputKind.Scale);
        sut.Row.Cells[0].ScaleLabels.Should().HaveCount(5);
        sut.Row.Cells[0].SelectedScaleIndex = 3;
        await sut.SaveAsync();

        cs.LastValues.Should().ContainSingle();
        cs.LastValues![0].ValueInteger.Should().Be(3);
    }

    [WpfFact]
    public async Task Two_scales_both_record()
    {
        var node = Guid.NewGuid();
        var (sut, cs, ms, rd) = Build(node);
        rd.Scales.Clear();
        var s1 = Guid.NewGuid(); var s2 = Guid.NewGuid();
        rd.Scales.Add(new ScaleTemplate { Id = s1, Code = "a", Name = "A", MinValue = 1, MaxValue = 3, LabelsJson = "[\"x\",\"y\",\"z\"]" });
        rd.Scales.Add(new ScaleTemplate { Id = s2, Code = "b", Name = "B", MinValue = 1, MaxValue = 3, LabelsJson = "[\"x\",\"y\",\"z\"]" });
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Energy", Type = MetricType.Integer, ScaleTemplateId = s1, SortOrder = 0 });
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Focus", Type = MetricType.Integer, ScaleTemplateId = s2, SortOrder = 1 });
        await sut.LoadMetricsAsync();

        sut.Row.Cells[0].SelectedScaleIndex = 1;
        sut.Row.Cells[1].SelectedScaleIndex = 2;
        await sut.SaveAsync();

        cs.LastValues.Should().HaveCount(2);
    }

    [WpfFact]
    public async Task Save_with_no_values_sets_error_and_does_not_record()
    {
        var node = Guid.NewGuid();
        var (sut, cs, ms, _) = Build(node);
        await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Reps", Type = MetricType.Integer, SortOrder = 0 });
        await sut.LoadMetricsAsync();

        await sut.SaveAsync();

        cs.LastRecord.Should().BeNull();
        sut.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~MeasuredQuickAddViewModelTests"`
Expected: FAIL — `MeasuredQuickAddViewModel` does not exist (compile error).

- [ ] **Step 3: Create `MetricCellFactory.cs`**

```csharp
using System.Text.Json;
using Recordum.Core.Models;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>Builds enriched <see cref="SeriesCellViewModel"/>s from resolved units and scales.
/// Built once per load from reference data; reused for every row (Measured = 1 row, Resistance = N).</summary>
public sealed class MetricCellFactory
{
    private readonly IReadOnlyDictionary<Guid, string> _unitCodeById;
    private readonly IReadOnlyDictionary<Guid, ScaleTemplate> _scaleById;

    public MetricCellFactory(IEnumerable<UnitOfMeasure> units, IEnumerable<ScaleTemplate> scales)
    {
        _unitCodeById = units.ToDictionary(u => u.Id, u => u.Code);
        _scaleById = scales.ToDictionary(s => s.Id, s => s);
    }

    public SeriesCellViewModel Create(MetricDefinition m)
    {
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

    private static IReadOnlyList<string>? ParseLabels(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<string[]>(json); }
        catch (JsonException) { return null; }
    }
}
```

- [ ] **Step 4: Create `MeasuredQuickAddViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recordum.Core.Abstractions;
using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;
using Recordum.WPF.Resources;

namespace Recordum.WPF.ViewModels.HabitScreen.QuickAdd;

/// <summary>Multi-metric, single-gesture recorder used for Counter and State habits.
/// Renders one kind-aware cell per metric (measure+unit, scale buttons/slider, text) in one row.</summary>
public sealed partial class MeasuredQuickAddViewModel(
    ICompletionService completions,
    IMetricDefinitionService metrics,
    IReferenceDataQueryService refData,
    IClock clock) : QuickAddViewModelBase(completions, clock)
{
    private readonly IMetricDefinitionService _metrics = metrics;
    private readonly IReferenceDataQueryService _refData = refData;

    [ObservableProperty] private SeriesRowViewModel _row = new(Array.Empty<MetricDefinition>());
    [ObservableProperty] private string? _notes;

    public async Task LoadMetricsAsync(CancellationToken ct = default)
    {
        if (Node is null) return;
        var ordered = (await _metrics.ListByHabitAsync(Node.Id, ct).ConfigureAwait(true))
            .OrderBy(m => m.SortOrder).ToList();
        var units = await _refData.ListUnitsAsync(ct).ConfigureAwait(true);
        var scales = await _refData.ListScalesAsync(ct).ConfigureAwait(true);
        var factory = new MetricCellFactory(units, scales);
        Row = new SeriesRowViewModel(ordered.Select(factory.Create));
    }

    [RelayCommand]
    private Task Save(CancellationToken ct) => SaveAsync(ct);

    public override async Task SaveAsync(CancellationToken ct = default)
    {
        if (Node is null) return;
        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var values = Row.Cells.Select(c => c.BuildValue(0)).Where(v => v is not null).Select(v => v!).ToList();
            if (values.Count == 0)
            {
                ErrorMessage = Strings.QuickAdd_Error_ValueRequired;
                return;
            }

            var (ts, ms, retro) = ResolveCompletionTimestamp();
            var draft = new Completion
            {
                HabitNodeId = Node.Id,
                CompletedAt = ts,
                CompletedAtMs = ms,
                IsRetroactive = retro,
                Notes = Notes,
            };
            var id = await Completions.RecordAsync(draft, values, ct: ct).ConfigureAwait(true);
            OnSaved(id);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~MeasuredQuickAddViewModelTests"`
Expected: PASS (6 tests). If `QuickAdd_Error_ValueRequired` resolves to a non-empty string, the error test passes.

- [ ] **Step 6: Commit**

```bash
git add src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/MetricCellFactory.cs src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/MeasuredQuickAddViewModel.cs tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/MeasuredQuickAddViewModelTests.cs
git commit -m "feat(wpf): MeasuredQuickAddViewModel — multi-metric recorder for Counter+State"
```

---

## Task 3: Host wiring + selector + Resistance units; retire Counter/State VMs

**Files:**
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/HabitScreenViewModel.cs:153-169`
- Modify: `src/Recordum.WPF/Selectors/QuickAddTemplateSelector.cs`
- Modify: `src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/ResistanceQuickAddViewModel.cs`
- Delete: `CounterQuickAddViewModel.cs`, `StateQuickAddViewModel.cs`
- Delete: old tests `CounterQuickAddViewModelTests`/`StateQuickAddViewModelTests` if present (grep first).
- Test: `tests/Recordum.WPF.Tests/ViewModels/HabitScreen/QuickAdd/ResistanceQuickAddViewModelTests.cs` (extend or create) for unit label.

- [ ] **Step 1: Write the failing test (Resistance shows unit)**

```csharp
[WpfFact]
public async Task Resistance_cells_carry_unit_label()
{
    var node = Guid.NewGuid();
    var cs = new FakeCompletionService();
    var ms = new FakeMetricDefinitionService();
    var rd = new FakeReferenceData();
    var unitId = rd.Units[0].Id;
    await ms.CreateAsync(new MetricDefinition { HabitNodeId = node, Name = "Distance", Type = MetricType.Decimal, UnitOfMeasureId = unitId, SortOrder = 0 });
    var sut = new ResistanceQuickAddViewModel(cs, ms, rd, new FakeClock())
    {
        Node = new HabitNode { Id = node, HabitType = HabitType.Resistance },
    };

    await sut.LoadMetricsAsync();

    sut.Series.Should().ContainSingle();
    sut.Series[0].Cells[0].UnitLabel.Should().Be(rd.Units[0].Code);
}
```

(Add this to the existing Resistance test file; check its current ctor usage and update all `new ResistanceQuickAddViewModel(cs, ms, clock)` call sites to the 4-arg ctor with a `FakeReferenceData()`.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~ResistanceQuickAddViewModelTests"`
Expected: FAIL — 3-arg ctor no longer matches / `UnitLabel` null.

- [ ] **Step 3: Modify `ResistanceQuickAddViewModel.cs`**

Add `IReferenceDataQueryService refData` to the primary ctor and build cells via the factory:

```csharp
public sealed partial class ResistanceQuickAddViewModel(
    ICompletionService completions,
    IMetricDefinitionService metrics,
    IReferenceDataQueryService refData,
    IClock clock) : QuickAddViewModelBase(completions, clock)
{
    private readonly IMetricDefinitionService _metrics = metrics;
    private readonly IReferenceDataQueryService _refData = refData;
    private List<MetricDefinition> _orderedMetrics = [];
    private MetricCellFactory _factory = new(Array.Empty<UnitOfMeasure>(), Array.Empty<ScaleTemplate>());

    public ObservableCollection<SeriesRowViewModel> Series { get; } = [];

    [ObservableProperty] private string? _notes;

    public async Task LoadMetricsAsync(CancellationToken ct = default)
    {
        if (Node is null) return;
        _orderedMetrics = (await _metrics.ListByHabitAsync(Node.Id, ct).ConfigureAwait(true))
            .OrderBy(m => m.SortOrder).ToList();
        var units = await _refData.ListUnitsAsync(ct).ConfigureAwait(true);
        var scales = await _refData.ListScalesAsync(ct).ConfigureAwait(true);
        _factory = new MetricCellFactory(units, scales);
        Series.Clear();
        if (_orderedMetrics.Count > 0) Series.Add(NewRow());
    }

    private SeriesRowViewModel NewRow() => new(_orderedMetrics.Select(_factory.Create));

    [RelayCommand]
    private void AddSeries()
    {
        if (_orderedMetrics.Count == 0) return;
        Series.Add(NewRow());
    }
    // RemoveSeries, Save, SaveAsync: unchanged (keep existing bodies).
```

> Keep the existing `RemoveSeries`, `Save`, and `SaveAsync` members exactly as they are.

- [ ] **Step 4: Update host `HabitScreenViewModel.cs` (lines ~153-169)**

```csharp
QuickAddViewModelBase? vm = node.HabitType switch
{
    HabitType.Action => new ActionQuickAddViewModel(_completions, _clock),
    HabitType.Counter => new MeasuredQuickAddViewModel(_completions, _metrics, _refData, _clock),
    HabitType.Resistance => new ResistanceQuickAddViewModel(_completions, _metrics, _refData, _clock),
    HabitType.Journal => new JournalQuickAddViewModel(_completions, _clock),
    HabitType.State => new MeasuredQuickAddViewModel(_completions, _metrics, _refData, _clock),
    _ => null,
};
if (vm is null) { QuickAdd = null; return; }
vm.Node = node;
switch (vm)
{
    case MeasuredQuickAddViewModel mq: await mq.LoadMetricsAsync(ct).ConfigureAwait(true); break;
    case ResistanceQuickAddViewModel r: await r.LoadMetricsAsync(ct).ConfigureAwait(true); break;
}
QuickAdd = vm;
```

> `HabitScreenViewModel` does NOT yet have `_refData`. Add it:
> - In the ctor (`HabitScreenViewModel.cs:46-60`), add parameter `IReferenceDataQueryService refData` (after `IMetricDefinitionService metrics`), add `ArgumentNullException.ThrowIfNull(refData);`, and a `private readonly IReferenceDataQueryService _refData; _refData = refData;` (field declared near `_metrics` line 22).
> - Update these construction sites to pass a reference-data service:
>   - `tests/Recordum.WPF.Tests/Fixtures/HabitScreenViewModelFactory.cs` — add a `new FakeReferenceData()` arg.
>   - `tests/Recordum.WPF.Tests/Fixtures/MainWindowViewModelFactory.cs` — same, if it builds a HabitScreenViewModel.
>   - Any direct `new HabitScreenViewModel(` in `tests/` (grep) — add the arg.
>   - DI: `HabitScreenViewModel` is resolved via DI which already registers `IReferenceDataQueryService` (ServiceRegistrar:122), so production wiring needs no change beyond the ctor.

- [ ] **Step 5: Update `QuickAddTemplateSelector.cs`**

```csharp
public sealed class QuickAddTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Action { get; set; }
    public DataTemplate? Measured { get; set; }
    public DataTemplate? Resistance { get; set; }
    public DataTemplate? Journal { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) => item switch
    {
        ActionQuickAddViewModel => Action,
        MeasuredQuickAddViewModel => Measured,
        ResistanceQuickAddViewModel => Resistance,
        JournalQuickAddViewModel => Journal,
        _ => null,
    };
}
```

- [ ] **Step 6: Delete obsolete VMs and tests**

```bash
git rm src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/CounterQuickAddViewModel.cs src/Recordum.WPF/ViewModels/HabitScreen/QuickAdd/StateQuickAddViewModel.cs
```

Grep for `CounterQuickAddViewModel`/`StateQuickAddViewModel` across `tests/` and delete/port any tests that referenced them (their behavior is now covered by `MeasuredQuickAddViewModelTests`). Update `QuickAddViewModelTests.cs` if it constructs them.

- [ ] **Step 7: Run the full QuickAdd + host test set**

Run: `dotnet test tests/Recordum.WPF.Tests --filter "FullyQualifiedName~QuickAdd|FullyQualifiedName~HabitScreenViewModel"`
Expected: PASS. Fix any remaining call sites (compile errors) until green.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "refactor(wpf): route Counter+State to MeasuredQuickAdd; Resistance shows units; retire old VMs"
```

---

## Task 4: XAML — kind-aware cell template + Measured view + Resistance reuse

**Files:**
- Modify: `src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml`

No unit test (markup). Verified by build + visual smoke.

- [ ] **Step 1: Add a keyed kind-aware cell template** (in the same `<UserControl.Resources>` as the other QuickAdd templates, before the selector):

```xml
<DataTemplate x:Key="MetricCellTemplate" DataType="{x:Type qa:SeriesCellViewModel}">
    <StackPanel Margin="0,0,12,6" MinWidth="90">
        <StackPanel Orientation="Horizontal">
            <TextBlock Text="{Binding Name}" FontSize="{StaticResource FontSize.Small}"
                       Foreground="{DynamicResource Brush.TextMuted}"/>
            <TextBlock Text="{Binding UnitLabel}" Margin="4,0,0,0"
                       FontSize="{StaticResource FontSize.Small}"
                       Foreground="{DynamicResource Brush.TextMuted}"
                       Visibility="{Binding UnitLabel, Converter={StaticResource NullToVisibility}}"/>
        </StackPanel>
        <!-- Measured: numeric (Integer/Seconds via IntegerValue, Decimal via DecimalValue) -->
        <Grid>
            <TextBox Width="90"
                     Text="{Binding IntegerValue, UpdateSourceTrigger=PropertyChanged, TargetNullValue=''}">
                <TextBox.Style>
                    <Style TargetType="TextBox" BasedOn="{StaticResource {x:Type TextBox}}">
                        <Setter Property="Visibility" Value="Collapsed"/>
                        <Style.Triggers>
                            <MultiDataTrigger>
                                <MultiDataTrigger.Conditions>
                                    <Condition Binding="{Binding Kind}" Value="Measured"/>
                                    <Condition Binding="{Binding Type}" Value="Integer"/>
                                </MultiDataTrigger.Conditions>
                                <Setter Property="Visibility" Value="Visible"/>
                            </MultiDataTrigger>
                            <MultiDataTrigger>
                                <MultiDataTrigger.Conditions>
                                    <Condition Binding="{Binding Kind}" Value="Measured"/>
                                    <Condition Binding="{Binding Type}" Value="Seconds"/>
                                </MultiDataTrigger.Conditions>
                                <Setter Property="Visibility" Value="Visible"/>
                            </MultiDataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBox.Style>
            </TextBox>
            <TextBox Width="90"
                     Text="{Binding DecimalValue, UpdateSourceTrigger=PropertyChanged, TargetNullValue=''}">
                <TextBox.Style>
                    <Style TargetType="TextBox" BasedOn="{StaticResource {x:Type TextBox}}">
                        <Setter Property="Visibility" Value="Collapsed"/>
                        <Style.Triggers>
                            <MultiDataTrigger>
                                <MultiDataTrigger.Conditions>
                                    <Condition Binding="{Binding Kind}" Value="Measured"/>
                                    <Condition Binding="{Binding Type}" Value="Decimal"/>
                                </MultiDataTrigger.Conditions>
                                <Setter Property="Visibility" Value="Visible"/>
                            </MultiDataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBox.Style>
            </TextBox>
            <!-- Text -->
            <TextBox Width="140" Text="{Binding TextValue, UpdateSourceTrigger=PropertyChanged}">
                <TextBox.Style>
                    <Style TargetType="TextBox" BasedOn="{StaticResource {x:Type TextBox}}">
                        <Setter Property="Visibility" Value="Collapsed"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding Kind}" Value="Text">
                                <Setter Property="Visibility" Value="Visible"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBox.Style>
            </TextBox>
            <!-- Scale: labelled selector (<=10) -->
            <ComboBox MinWidth="120" ItemsSource="{Binding ScaleLabels}"
                      SelectedIndex="{Binding SelectedScaleIndex}">
                <ComboBox.Style>
                    <Style TargetType="ComboBox" BasedOn="{StaticResource {x:Type ComboBox}}">
                        <Setter Property="Visibility" Value="Collapsed"/>
                        <Style.Triggers>
                            <MultiDataTrigger>
                                <MultiDataTrigger.Conditions>
                                    <Condition Binding="{Binding Kind}" Value="Scale"/>
                                    <Condition Binding="{Binding UseSlider}" Value="False"/>
                                </MultiDataTrigger.Conditions>
                                <Setter Property="Visibility" Value="Visible"/>
                            </MultiDataTrigger>
                        </Style.Triggers>
                    </Style>
                </ComboBox.Style>
            </ComboBox>
            <!-- Scale: slider (>10) -->
            <StackPanel Orientation="Horizontal">
                <StackPanel.Style>
                    <Style TargetType="StackPanel">
                        <Setter Property="Visibility" Value="Collapsed"/>
                        <Style.Triggers>
                            <MultiDataTrigger>
                                <MultiDataTrigger.Conditions>
                                    <Condition Binding="{Binding Kind}" Value="Scale"/>
                                    <Condition Binding="{Binding UseSlider}" Value="True"/>
                                </MultiDataTrigger.Conditions>
                                <Setter Property="Visibility" Value="Visible"/>
                            </MultiDataTrigger>
                        </Style.Triggers>
                    </Style>
                </StackPanel.Style>
                <Slider Width="140" Minimum="0" Maximum="{Binding StepCount}"
                        Value="{Binding SelectedScaleIndex, TargetNullValue=0}"
                        IsSnapToTickEnabled="True" TickFrequency="1" VerticalAlignment="Center"/>
                <TextBlock Margin="8,0,0,0" VerticalAlignment="Center"
                           Foreground="{DynamicResource Brush.Text}"
                           Text="{Binding SelectedScaleIndex}"/>
            </StackPanel>
        </Grid>
    </StackPanel>
</DataTemplate>
```

> NOTE: `Slider.Maximum="{Binding StepCount}"` gives steps 0..StepCount (one extra). Set `Maximum` to `StepCount - 1` via a converter, OR bind to a new `SliderMax` property on the cell (= `StepCount - 1`). **Add `public int SliderMax => Math.Max(0, StepCount - 1);` to `SeriesCellViewModel` and bind `Maximum="{Binding SliderMax}"`.** Update Task 1's cell accordingly when implementing this task (small add, no test needed; covered by build).

- [ ] **Step 2: Replace the Counter template with the Measured template**

Replace the whole `CounterQuickAddTemplate` `DataTemplate` block (HabitScreen.xaml ~158-196) with:

```xml
<DataTemplate x:Key="MeasuredQuickAddTemplate" DataType="{x:Type qa:MeasuredQuickAddViewModel}">
    <StackPanel Margin="12" Orientation="Vertical">
        <Grid Margin="0,0,0,8">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0"
                       Text="{x:Static res:Strings.Abitudine_QuickAdd_RecordingFor}"
                       Foreground="{DynamicResource Brush.TextMuted}"
                       VerticalAlignment="Center" Margin="0,0,8,0"/>
            <TextBlock Grid.Column="1" VerticalAlignment="Center"
                       Foreground="{DynamicResource Brush.Text}"
                       Text="{Binding CompletionDate, StringFormat=d}"/>
        </Grid>
        <ItemsControl ItemsSource="{Binding Row.Cells}"
                      ItemTemplate="{StaticResource MetricCellTemplate}"
                      Margin="0,0,0,8">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate><WrapPanel Orientation="Horizontal"/></ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
        </ItemsControl>
        <TextBlock Text="{x:Static res:Strings.HabitScreen_QuickAdd_Notes}"
                   Foreground="{DynamicResource Brush.TextMuted}" Margin="0,0,0,4"/>
        <TextBox Text="{Binding Notes, UpdateSourceTrigger=PropertyChanged}"
                 AcceptsReturn="True" MinHeight="48" Margin="0,0,0,8"/>
        <Button Content="{x:Static res:Strings.HabitScreen_QuickAdd_Save}"
                Command="{Binding SaveCommand}"
                HorizontalAlignment="Left" Padding="12,6"/>
        <TextBlock Text="{Binding ErrorMessage}"
                   Foreground="Red" Margin="0,6,0,0"
                   Visibility="{Binding ErrorMessage, Converter={StaticResource NullToVisibility}}"/>
    </StackPanel>
</DataTemplate>
```

- [ ] **Step 3: Delete the `StateQuickAddTemplate` block** (HabitScreen.xaml ~273-297). Remove it entirely.

- [ ] **Step 4: Point the Resistance cell at the shared template**

In `ResistanceQuickAddTemplate`, replace the inner cell `ItemTemplate` (the `<StackPanel>` with `TextBlock` + `TextBox` bound to `IntegerValue`, ~221-228) with `ItemTemplate="{StaticResource MetricCellTemplate}"`:

```xml
<ItemsControl ItemsSource="{Binding Cells}"
              ItemTemplate="{StaticResource MetricCellTemplate}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate><StackPanel Orientation="Horizontal"/></ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
</ItemsControl>
```

- [ ] **Step 5: Update the selector wiring** (HabitScreen.xaml ~401-406):

```xml
<sel:QuickAddTemplateSelector x:Key="QuickAddSelector"
                              Action="{StaticResource ActionQuickAddTemplate}"
                              Measured="{StaticResource MeasuredQuickAddTemplate}"
                              Resistance="{StaticResource ResistanceQuickAddTemplate}"
                              Journal="{StaticResource JournalQuickAddTemplate}"/>
```

- [ ] **Step 6: Build**

Run: `dotnet build src/Recordum.WPF`
Expected: 0 errors. Fix any XAML parse errors (e.g., missing `qa:` namespace — already declared line 7).

- [ ] **Step 7: Commit**

```bash
git add src/Recordum.WPF/Views/HabitScreen/HabitScreen.xaml
git commit -m "feat(wpf): kind-aware metric cell XAML + Measured QuickAdd view; retire Counter/State templates"
```

---

## Task 5: Full suite + visual smoke

- [ ] **Step 1: Run the full WPF + Data suites**

Run: `dotnet test tests/Recordum.WPF.Tests` then `dotnet test tests/Recordum.Data.Tests`
Expected: all green. Resolve any stragglers (call sites of deleted VMs, factory mocks).

- [ ] **Step 2: Update the tracker**

Mark B11 `fixed` in `docs/phase-4-issues.md` with the commit range; note Detail/charts multi-metric remains Option 2.

- [ ] **Step 3: Commit + offer visual verification**

```bash
git add docs/phase-4-issues.md
git commit -m "docs(phase-4): B11 multi-metric recording fixed"
```

Then launch via the `run` skill and verify on screen: build Quantity+Scale → Launch → the habit's Abitudine tab shows BOTH a numeric field (with unit) and a labelled scale selector; recording saves both; a 1-100 custom scale (once it exists) shows a slider.

---

## Self-review notes

- **Spec coverage**: §4.1 MetricInputKind (T1), §4.2 cell (T1), §4.3 Measured VM (T2), §4.4 Resistance units (T3), §4.5 XAML (T4), §4.6 host (T3), §6 error handling (T2 step1 + impl), §7 testing (T1–T3). All covered.
- **Encoding**: scale → `ValueInteger = SelectedScaleIndex` (0-based), matches retired `StateQuickAddViewModel`. ✓
- **Scope**: recording only; Detail/charts untouched. ✓
- **Open implementer choices flagged inline**: (a) `ResolveKind` lives on `SeriesCellViewModel` (enum file has no method); (b) add `SliderMax = StepCount-1` for slider binding; (c) `HabitScreenViewModel` must expose `_refData` — verify ctor/factory/test call sites.
