using Recordum.Core.Abstractions.Services;
using Recordum.Core.Models;

namespace Recordum.Presentation.Tests.Fixtures;

/// <summary>Il servizio delle scale in memoria: scrive nello stesso <see cref="FakeReferenceData"/> e nelle stesse metriche
/// che leggono le sezioni, cosi' un "duplica e modifica" si rilegge come in app.</summary>
public sealed class FakeScaleTemplateService(FakeReferenceData refData, FakeMetricDefinitionService metrics) : IScaleTemplateService
{
    public FakeScaleTemplateService() : this(new FakeReferenceData(), new FakeMetricDefinitionService()) { }

    public List<(Guid Id, ScaleDraft Draft)> Updates { get; } = [];
    public List<(Guid SourceId, string Name, IReadOnlyList<ScaleAnchorDraft> Anchors, Guid? MetricId)> Duplicates { get; } = [];
    public Dictionary<Guid, ScaleUsage> Usage { get; } = [];
    public Exception? NextException { get; set; }

    public ValueTask<Guid> CreateAsync(ScaleDraft draft, CancellationToken ct = default) => throw new NotSupportedException();

    public ValueTask UpdateAsync(Guid id, ScaleDraft draft, CancellationToken ct = default)
    {
        if (NextException is { } e) { NextException = null; throw e; }
        Updates.Add((id, draft));
        return ValueTask.CompletedTask;
    }

    public ValueTask<Guid> DuplicateAsync(Guid sourceId, string name, IReadOnlyList<ScaleAnchorDraft> anchors, Guid? repointMetricId, CancellationToken ct = default)
    {
        Duplicates.Add((sourceId, name, anchors, repointMetricId));
        var src = refData.Scales.Single(s => s.Id == sourceId);
        var copy = new ScaleTemplate
        {
            Id = Guid.NewGuid(), Code = src.Code + "-2", Name = name, MinValue = src.MinValue, MaxValue = src.MaxValue,
            Precision = src.Precision, IsOfficial = false,
            Anchors = anchors.Select(a => new ScaleAnchor { Id = Guid.NewGuid(), Value = a.Value, Label = a.Label }).ToList(),
        };
        refData.Scales.Add(copy);
        if (repointMetricId is { } mid && metrics.Store.TryGetValue(mid, out var m)) m.ScaleTemplateId = copy.Id;
        return new(copy.Id);
    }

    public ValueTask<ScaleUsage> GetUsageAsync(Guid id, CancellationToken ct = default) =>
        new(Usage.TryGetValue(id, out var u) ? u : new ScaleUsage(1, null, null, false));
}
